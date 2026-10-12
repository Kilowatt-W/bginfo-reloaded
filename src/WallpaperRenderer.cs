using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>
/// Zeichnet die gesammelten Systeminfos als Panel (Position/Farben/Schrift aus der Konfiguration) auf
/// ein Hintergrundbild - per GDI+ (System.Drawing.Common, MIT). Das Tool laeuft ohnehin nur unter
/// Windows, daher ist die Windows-Bindung von System.Drawing hier kein Nachteil, und es gibt keine
/// lizenzpflichtige Fremdbibliothek mehr im Rendering.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WallpaperRenderer
{
    private const int SectionSpacerHeight = 16;
    private const long JpegQuality = 92;

    /// <summary>
    /// Rendert das Bild fuer einen Monitor. Eine leere Zeilenliste ergibt nur den (angepassten)
    /// Hintergrund ohne Panel - fuer Monitore, auf denen die Info nicht erscheinen soll.
    /// Gibt die Lage des Panels im Bild zurueck (leer, wenn keins gezeichnet wurde).
    /// </summary>
    public static Rectangle Render(IReadOnlyList<InfoLine> lines, string outputImagePath, string? baseImagePath, AppConfig config, MonitorTarget target)
    {
        var panel = config.Panel;
        var fallbackBackground = ParseColor(config.Wallpaper.BackgroundColor, "wallpaper.backgroundColor");
        var panelColor = ParseColor(panel.Background, "panel.background");
        var headerColor = ParseColor(config.Colors.Header, "colors.header");
        var labelColor = ParseColor(config.Colors.Label, "colors.label");
        var valueColor = ParseColor(config.Colors.Value, "colors.value");

        var toScreen = config.Wallpaper.Fit != WallpaperFit.None;
        using Bitmap image = toScreen
            ? FitToScreen(LoadBaseImage(baseImagePath, fallbackBackground, target), config.Wallpaper.Fit, fallbackBackground, target)
            : LoadBaseImage(baseImagePath, fallbackBackground, target);

        if (lines.Count == 0)
        {
            Save(image, outputImagePath);
            return Rectangle.Empty;
        }

        // Nur in Bildschirmaufloesung entspricht ein Bildpixel einem Bildschirmpixel - nur dann ist DPI-Skalierung sinnvoll.
        var baseScale = toScreen && panel.ScaleWithDpi ? target.Dpi / 96f : 1f;
        // Der Abstand zum Rand haengt nur an der Windows-Skalierung, nicht an einer relativen Panelbreite.
        var margin = (int)Math.Round(panel.Margin * baseScale);

        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // Graustufen-Antialiasing statt ClearType: ClearType-Farbsaeume saehen auf einem halbtransparenten
        // Panel ueber einem Foto unsauber aus und passen nur zu einer bestimmten Subpixel-Anordnung.
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // GenericTypographic: ohne den GDI+-Standardrand links/rechts, damit Messung und Spalten exakt passen.
        // Zu lange Texte enden mit "…" statt ueber die Spalte bzw. das Panel hinauszulaufen.
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        format.Trimming = StringTrimming.EllipsisCharacter;

        float MeasureWidth(string text, Font font) =>
            string.IsNullOrEmpty(text) ? 0 : graphics.MeasureString(text, font, PointF.Empty, format).Width;

        // Flaeche, in der das Panel platziert wird: das ganze Bild, auf Wunsch ohne die von der Taskleiste
        // belegten Raender. Diese sind in Bildschirmpixeln angegeben - bei fit = None (Windows skaliert
        // das Bild erst spaeter) deshalb auf die Bildgroesse umrechnen.
        var area = new Rectangle(0, 0, image.Width, image.Height);
        if (panel.AvoidTaskbar)
        {
            var factorX = (float)image.Width / target.Width;
            var factorY = (float)image.Height / target.Height;
            var reserved = target.Reserved;
            area = Rectangle.FromLTRB(
                (int)Math.Ceiling(reserved.Left * factorX), (int)Math.Ceiling(reserved.Top * factorY),
                image.Width - (int)Math.Ceiling(reserved.Right * factorX), image.Height - (int)Math.Ceiling(reserved.Bottom * factorY));
        }
        var maxWidth = Math.Max(1, area.Width - margin * 2);
        var maxHeight = Math.Max(1, area.Height - margin * 2);

        var valueLines = lines.Where(l => l.Kind == LineKind.Value).ToList();
        var sectionLines = lines.Where(l => l.Kind == LineKind.Section).ToList();
        // Anteil der Bezeichnungsspalte an der Innenbreite (0 = nicht vorgegeben).
        var labelShare = panel.LabelShare <= 0 ? 0f : Math.Clamp(panel.LabelShare, 10, 90) / 100f;

        // Alle Masse des Panels fuer einen Skalierungsfaktor. "NaturalWidth" ist die Breite, bei der
        // jede Bezeichnung und jeder Wert vollstaendig Platz hat.
        PanelLayout Measure(float scale)
        {
            var fontSize = Math.Max(4f, config.Font.Size * scale);
            var headerSize = Math.Max(4f, config.Font.HeaderSize * scale);
            var layout = new PanelLayout
            {
                Scale = scale,
                HeaderFont = CreateFontSafe(config.Font.Family, headerSize, FontStyle.Bold),
                LabelFont = CreateFontSafe(config.Font.Family, fontSize, FontStyle.Regular),
                ValueFont = CreateFontSafe(config.Font.Family, fontSize, FontStyle.Regular),
                Padding = (int)Math.Round(panel.Padding * scale),
                Gap = (int)Math.Round(Math.Max(0, panel.ColumnGap) * scale),
                ValueLineHeight = (int)Math.Round(fontSize * 1.67f),
                SectionHeaderHeight = (int)Math.Round(headerSize * 1.9f),
                SpacerHeight = (int)Math.Round(SectionSpacerHeight * scale)
            };
            layout.MaxLabel = valueLines.Select(l => MeasureWidth(l.Label, layout.LabelFont)).DefaultIfEmpty(0).Max();
            layout.MaxValue = valueLines.Select(l => MeasureWidth(l.Value, layout.ValueFont)).DefaultIfEmpty(0).Max();
            var headerWidth = sectionLines.Select(l => MeasureWidth(l.Label, layout.HeaderFont)).DefaultIfEmpty(0).Max();

            float inner;
            if (labelShare > 0)
            {
                inner = Math.Max((layout.MaxLabel + layout.Gap) / labelShare, layout.MaxValue / (1 - labelShare));
            }
            else if (panel.LabelWidth > 0)
            {
                inner = (float)Math.Round(panel.LabelWidth * scale) + layout.MaxValue;
            }
            else
            {
                inner = layout.MaxLabel + layout.Gap + layout.MaxValue;
            }
            layout.NaturalWidth = (int)Math.Ceiling(Math.Max(inner, headerWidth)) + layout.Padding * 2;
            layout.Height = layout.Padding * 2 + lines.Sum(layout.LineHeight);
            return layout;
        }

        // Relative Panelbreite: 1/3 bis 1/8 der Bildbreite. Der Inhalt wird so skaliert, dass er diese
        // Breite gerade fuellt (und in der Hoehe noch passt). Textbreiten wachsen nicht exakt linear mit
        // der Schriftgroesse, deshalb wird nach dem ersten Schritt noch bis zu zweimal nachverkleinert.
        var fraction = panel.ScreenFraction <= 0 ? 0 : Math.Clamp(panel.ScreenFraction, 3, 8);
        var targetWidth = fraction > 0 ? Math.Min(image.Width / fraction, maxWidth) : 0;
        var layout = Measure(baseScale);
        try
        {
            if (fraction > 0)
            {
                for (var pass = 0; pass < 3; pass++)
                {
                    var factor = Math.Min((float)targetWidth / layout.NaturalWidth, (float)maxHeight / layout.Height);
                    if (Math.Abs(factor - 1f) < 0.005f || (pass > 0 && factor >= 1f))
                    {
                        break;
                    }
                    var scale = layout.Scale * factor;
                    layout.Dispose();
                    layout = Measure(scale);
                }
            }

            int Scaled(int pixels) => (int)Math.Round(pixels * layout.Scale);
            var padding = layout.Padding;

            var panelWidth = fraction > 0 ? targetWidth : panel.Width > 0 ? Scaled(panel.Width) : layout.NaturalWidth;
            panelWidth = Math.Min(panelWidth, maxWidth);
            var innerWidth = Math.Max(1, panelWidth - padding * 2);

            int labelColumnWidth;
            if (labelShare > 0)
            {
                labelColumnWidth = (int)Math.Round(innerWidth * labelShare);
            }
            else if (panel.LabelWidth > 0)
            {
                labelColumnWidth = Scaled(panel.LabelWidth);
            }
            else
            {
                labelColumnWidth = (int)Math.Ceiling(layout.MaxLabel) + layout.Gap;
                // Reicht die (feste) Breite nicht fuer beide Spalten, darf die automatische
                // Bezeichnungsspalte hoechstens 45 % beanspruchen - sonst bliebe fuer die Werte kaum Platz.
                if (labelColumnWidth + layout.MaxValue > innerWidth)
                {
                    labelColumnWidth = Math.Min(labelColumnWidth, (int)Math.Round(innerWidth * 0.45));
                }
            }
            labelColumnWidth = Math.Min(labelColumnWidth, innerWidth);
            // Zwischen Bezeichnung und Wert bleibt etwas Luft, auch wenn eine Bezeichnung gekuerzt wird.
            var labelSpace = labelColumnWidth - Math.Min(layout.Gap, Scaled(8));
            var valueSpace = innerWidth - labelColumnWidth;

            // Zu lange Texte: abschneiden mit "…" (Standard) oder in der Spalte umbrechen. Umbrochen wird
            // nicht bei relativer Panelbreite - dort ist der Inhalt schon so skaliert, dass er passt.
            var wrap = panel.TextOverflow == TextOverflow.Wrap && fraction == 0;
            using var wrapFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
            wrapFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            var textFormat = wrap ? wrapFormat : format;

            int TextLines(string text, Font font, float width)
            {
                if (!wrap || string.IsNullOrWhiteSpace(text) || width <= 0)
                {
                    return 1;
                }
                graphics.MeasureString(text, font, new SizeF(width + 2, 100000f), wrapFormat, out _, out var filled);
                return Math.Max(1, filled);
            }

            // Hoehe jeder Zeile: die erste Textzeile mit dem ueblichen Zeilenabstand, jede weitere
            // (umbrochene) mit der Zeilenhoehe der Schrift.
            var rowHeights = lines.Select(line =>
            {
                if (line.Kind == LineKind.Section)
                {
                    var extra = string.IsNullOrEmpty(line.Label) ? 0 : TextLines(line.Label, layout.HeaderFont, innerWidth) - 1;
                    return layout.LineHeight(line) + extra * (int)Math.Ceiling(layout.HeaderFont.GetHeight(graphics));
                }
                var more = Math.Max(TextLines(line.Label, layout.LabelFont, labelSpace), TextLines(line.Value, layout.ValueFont, valueSpace)) - 1;
                return layout.LineHeight(line) + more * (int)Math.Ceiling(layout.ValueFont.GetHeight(graphics));
            }).ToList();
            var panelHeight = Math.Min(padding * 2 + rowHeights.Sum(), maxHeight);

            var (panelX, panelY) = GetPanelOrigin(panel.Position, margin, area.Width, area.Height, panelWidth, panelHeight);
            panelX += area.Left;
            panelY += area.Top;

            var radius = Math.Min(Scaled(panel.CornerRadius), Math.Min(panelWidth, panelHeight) / 2);
            using (var panelPath = RoundedRectangle(panelX, panelY, panelWidth, panelHeight, radius))
            {
                var shadowSize = Scaled(panel.ShadowSize);
                if (shadowSize > 0)
                {
                    DrawShadow(graphics, panelPath, panelX, panelY, panelWidth, panelHeight, radius, shadowSize,
                        ParseColor(panel.ShadowColor, "panel.shadowColor"));
                }

                using var panelBrush = new SolidBrush(panelColor);
                graphics.FillPath(panelBrush, panelPath);
            }

            using var headerBrush = new SolidBrush(headerColor);
            using var labelBrush = new SolidBrush(labelColor);
            using var valueBrush = new SolidBrush(valueColor);

            // Zeichnen in ein Rechteck fester Breite: was nicht hineinpasst, wird mit "…" gekuerzt.
            // 2 Pixel Zugabe, damit ein exakt passender Text nicht durch Rundung gekuerzt wird.
            void DrawTextIfAny(string text, Font font, Brush brush, float x, float y, float width, int height)
            {
                if (!string.IsNullOrWhiteSpace(text) && width > 0)
                {
                    graphics.DrawString(text, font, brush, new RectangleF(x, y, width + 2, height), textFormat);
                }
            }

            // Lage jeder gezeichneten Zeile - danach richten sich die Rahmen um Abschnitte und Zeilen.
            var drawnRows = new List<(InfoLine Line, int Top, int Height)>();

            var y = panelY + padding;
            for (var row = 0; row < lines.Count; row++)
            {
                var line = lines[row];
                var height = rowHeights[row];
                if (y + height > panelY + panelHeight)
                {
                    break; // mehr Zeilen, als auf den Bildschirm passen
                }
                drawnRows.Add((line, y, height));

                if (line.Kind == LineKind.Section)
                {
                    DrawTextIfAny(line.Label, layout.HeaderFont, headerBrush, panelX + padding, y, innerWidth, height);
                }
                else
                {
                    DrawTextIfAny(line.Label, layout.LabelFont, labelBrush, panelX + padding, y, labelSpace, height);
                    DrawTextIfAny(line.Value, layout.ValueFont, valueBrush, panelX + padding + labelColumnWidth, y, valueSpace, height);
                }
                y += height;
            }

            DrawBorders(graphics, config.Border, drawnRows, new Rectangle(panelX, panelY, panelWidth, panelHeight), radius,
                padding, labelColumnWidth, Math.Min(layout.Gap, Scaled(8)), layout.ValueLineHeight, layout.Scale);

            Save(image, outputImagePath);
            return new Rectangle(panelX, panelY, panelWidth, panelHeight);
        }
        finally
        {
            layout.Dispose();
        }
    }

    /// <summary>
    /// Zeichnet den Rahmen aus "border": um das ganze Panel, je Abschnitt (Ueberschrift mit ihren
    /// Zeilen) oder als Gitter um jede Zeile. Die Linien liegen im vorhandenen Innenabstand und
    /// Zeilenabstand - Groesse und Lage des Panels aendern sich durch den Rahmen nicht.
    /// </summary>
    private static void DrawBorders(Graphics graphics, BorderConfig border, List<(InfoLine Line, int Top, int Height)> rows, Rectangle panel,
        int radius, int padding, int labelColumnWidth, int labelGap, int valueLineHeight, float scale)
    {
        if (border.Width <= 0)
        {
            return;
        }

        var width = Math.Max(1, (int)Math.Round(border.Width * scale));
        using var pen = new Pen(ParseColor(border.Color, "border.color"), width)
        {
            DashStyle = border.Style switch
            {
                BorderLine.Dotted => DashStyle.Dot,
                BorderLine.Dashed => DashStyle.Dash,
                _ => DashStyle.Solid
            }
        };

        if (border.Scope == BorderScope.Panel)
        {
            // Die Linie liegt ganz innerhalb der Panelflaeche: um die halbe Strichstaerke nach innen.
            var half = width / 2;
            using var path = RoundedRectangle(panel.X + half, panel.Y + half, panel.Width - width, panel.Height - width, Math.Max(0, radius - half));
            graphics.DrawPath(pen, path);
            return;
        }

        // Kasten und Gitter ragen seitlich um den halben Innenabstand ueber den Text hinaus. Der Text
        // steht oben in seiner Zeile, der Zeilenabstand darunter - die Linien liegen deshalb um ein
        // Fuenftel der Zeilenhoehe hoeher, dann sitzt der Text mittig zwischen ihnen.
        var side = Math.Max(1, padding / 2);
        var lift = Math.Max(1, valueLineHeight / 5);
        var left = panel.X + padding - side;
        var right = panel.Right - padding + side;
        var corner = Math.Min(radius / 2, side);

        if (border.Scope == BorderScope.Sections)
        {
            // Ein Abschnitt reicht von einer Ueberschrift (oder der ersten Zeile) bis vor den naechsten
            // Leerraum bzw. die naechste Ueberschrift.
            var start = -1;
            for (var i = 0; i <= rows.Count; i++)
            {
                var isSection = i < rows.Count && rows[i].Line.Kind == LineKind.Section;
                var isSpacer = isSection && string.IsNullOrEmpty(rows[i].Line.Label);
                if (start >= 0 && (i == rows.Count || isSection))
                {
                    var top = rows[start].Top - lift;
                    var bottom = rows[i - 1].Top + rows[i - 1].Height - lift;
                    using var box = RoundedRectangle(left, top, right - left, bottom - top, corner);
                    graphics.DrawPath(pen, box);
                    start = -1;
                }
                if (i < rows.Count && !isSpacer && start < 0)
                {
                    start = i;
                }
            }
            return;
        }

        // Rows: jede Wertzeile bekommt Linien oben und unten, aufeinanderfolgende Zeilen teilen sie
        // sich; dazu die Seiten und eine Trennlinie zwischen Bezeichnung und Wert.
        var divider = panel.X + padding + labelColumnWidth - labelGap / 2;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Line.Kind != LineKind.Value)
            {
                continue;
            }
            var top = rows[i].Top - lift;
            var bottom = rows[i].Top + rows[i].Height - lift;
            var firstOfRun = i == 0 || rows[i - 1].Line.Kind != LineKind.Value;
            if (firstOfRun)
            {
                graphics.DrawLine(pen, left, top, right, top);
            }
            graphics.DrawLine(pen, left, bottom, right, bottom);
            graphics.DrawLine(pen, left, top, left, bottom);
            graphics.DrawLine(pen, right, top, right, bottom);
            if (divider > left && divider < right)
            {
                graphics.DrawLine(pen, divider, top, divider, bottom);
            }
        }
    }

    /// <summary>Schriften und Masse des Panels fuer einen bestimmten Skalierungsfaktor.</summary>
    private sealed class PanelLayout : IDisposable
    {
        public float Scale;
        public Font HeaderFont = null!;
        public Font LabelFont = null!;
        public Font ValueFont = null!;
        public int Padding;
        public int Gap;
        public int ValueLineHeight;
        public int SectionHeaderHeight;
        public int SpacerHeight;
        public float MaxLabel;
        public float MaxValue;
        public int NaturalWidth;
        public int Height;

        public int LineHeight(InfoLine line) => line.Kind == LineKind.Section
            ? (string.IsNullOrEmpty(line.Label) ? SpacerHeight : SectionHeaderHeight)
            : ValueLineHeight;

        public void Dispose()
        {
            HeaderFont.Dispose();
            LabelFont.Dispose();
            ValueFont.Dispose();
        }
    }

    /// <summary>Groesse eines Bildes in Pixeln, ohne es ganz zu laden; null, wenn es nicht lesbar ist.</summary>
    public static Size? TryGetImageSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            return image.Size;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Wuerde bei dieser Anpassung auf diesem Monitor ein Teil des Bildes fehlen? Das ist der Fall, wenn
    /// das Bild ein anderes Seitenverhaeltnis hat als der Monitor und weder eingepasst noch gedehnt
    /// wird - bei None entscheidet dann Windows, was abgeschnitten wird (auch vom Panel).
    /// </summary>
    public static bool CutsOffImage(WallpaperFit fit, Size image, MonitorTarget monitor)
    {
        if (fit is WallpaperFit.Fit or WallpaperFit.Stretch || (image.Width == monitor.Width && image.Height == monitor.Height))
        {
            return false;
        }
        if (fit is WallpaperFit.Center or WallpaperFit.Tile)
        {
            return true;
        }
        var aspectRatio = (double)image.Width / image.Height / ((double)monitor.Width / monitor.Height);
        return Math.Abs(aspectRatio - 1) > 0.01;
    }

    private static GraphicsPath RoundedRectangle(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(new Rectangle(x, y, width, height));
            return path;
        }

        var diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Weicher Schatten: GDI+ kennt keinen Weichzeichner, deshalb viele nach aussen wachsende Formen mit
    /// geringer Deckkraft uebereinander - am Panelrand addieren sie sich zur Schattenfarbe, nach aussen
    /// laufen sie aus. Ein Schlagschatten: die Form des Panels, schraeg nach rechts unten versetzt -
    /// der Versatz ist groesser als der weiche Rand, deshalb bleibt oben und links nichts sichtbar.
    /// Die Panelflaeche selbst wird ausgespart, sonst wuerde der Schatten durch den halbtransparenten
    /// Hintergrund scheinen und ihn abdunkeln.
    /// </summary>
    private static void DrawShadow(Graphics graphics, GraphicsPath panelPath, int x, int y, int width, int height, int radius, int size, Color color)
    {
        var blur = Math.Max(1, size * 2 / 5);
        var offset = size - blur;
        var layerAlpha = 1 - Math.Pow(1 - color.A / 255.0, 1.0 / (blur + 1));
        using var brush = new SolidBrush(Color.FromArgb(Math.Clamp((int)Math.Round(layerAlpha * 255), 1, 255), color));

        var state = graphics.Save();
        graphics.SetClip(panelPath, CombineMode.Exclude);
        for (var grow = blur; grow >= 0; grow--)
        {
            using var layer = RoundedRectangle(x - grow + offset, y - grow + offset, width + grow * 2, height + grow * 2, radius + grow);
            graphics.FillPath(brush, layer);
        }
        graphics.Restore(state);
    }

    private static void Save(Bitmap image, string outputImagePath)
    {
        var directory = Path.GetDirectoryName(outputImagePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (Path.GetExtension(outputImagePath).ToLowerInvariant())
        {
            case ".png":
                image.Save(outputImagePath, ImageFormat.Png);
                break;
            case ".bmp":
                image.Save(outputImagePath, ImageFormat.Bmp);
                break;
            default:
                var jpegCodec = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                using (var parameters = new EncoderParameters(1))
                {
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
                    image.Save(outputImagePath, jpegCodec, parameters);
                }
                break;
        }
    }

    private static (int X, int Y) GetPanelOrigin(PanelPosition position, int margin, int imageWidth, int imageHeight, int panelWidth, int panelHeight)
    {
        var left = margin;
        var center = (imageWidth - panelWidth) / 2;
        var right = imageWidth - panelWidth - margin;
        var top = margin;
        var middle = (imageHeight - panelHeight) / 2;
        var bottom = imageHeight - panelHeight - margin;

        var (x, y) = position switch
        {
            PanelPosition.TopLeft => (left, top),
            PanelPosition.TopCenter => (center, top),
            PanelPosition.MiddleLeft => (left, middle),
            PanelPosition.Center => (center, middle),
            PanelPosition.MiddleRight => (right, middle),
            PanelPosition.BottomLeft => (left, bottom),
            PanelPosition.BottomCenter => (center, bottom),
            PanelPosition.BottomRight => (right, bottom),
            _ => (right, top)
        };
        return (Math.Max(0, x), Math.Max(0, y));
    }

    /// <summary>#RRGGBB oder #RRGGBBAA (das fuehrende # ist optional).</summary>
    public static Color ParseColor(string hex, string settingName)
    {
        var digits = (hex ?? "").Trim().TrimStart('#');
        if ((digits.Length == 6 || digits.Length == 8) &&
            uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            if (digits.Length == 6)
            {
                value = (value << 8) | 0xFF;
            }
            return Color.FromArgb((int)(value & 0xFF), (int)(value >> 24), (int)((value >> 16) & 0xFF), (int)((value >> 8) & 0xFF));
        }
        throw new InvalidOperationException(
            $"Konfiguration: '{hex}' bei {settingName} ist keine gueltige Farbe (erwartet #RRGGBB oder #RRGGBBAA).");
    }

    private static Bitmap LoadBaseImage(string? baseImagePath, Color fallbackBackground, MonitorTarget monitor)
    {
        if (baseImagePath != null && File.Exists(baseImagePath))
        {
            try
            {
                // Ueber einen Stream laden und in eine eigene 32-Bit-Bitmap kopieren: so bleibt die
                // Quelldatei nicht gesperrt, und indizierte Formate (GIF, 8-Bit-PNG) werden zeichenbar.
                using var stream = File.OpenRead(baseImagePath);
                using var source = Image.FromStream(stream);
                var copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(copy);
                g.DrawImage(source, 0, 0, source.Width, source.Height);
                return copy;
            }
            catch
            {
                // Kaputtes/nicht unterstuetztes Bildformat - dann eben synthetischer Hintergrund.
            }
        }

        var image = new Bitmap(monitor.Width, monitor.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image))
        {
            g.Clear(fallbackBackground);
        }
        return image;
    }

    /// <summary>
    /// Bringt das Bild auf die Aufloesung des Monitors - wie die Windows-Einstellungen "Ausfuellen"
    /// (Fill: mittig beschnitten), "Anpassen" (Fit: ganzes Bild, Raender in der Hintergrundfarbe),
    /// "Dehnen" (Stretch), "Zentriert" (Center) und "Nebeneinander" (Tile). Uebernimmt das uebergebene Bild.
    /// </summary>
    private static Bitmap FitToScreen(Bitmap source, WallpaperFit fit, Color background, MonitorTarget monitor)
    {
        var width = monitor.Width;
        var height = monitor.Height;
        if (source.Width == width && source.Height == height)
        {
            return source;
        }

        using (source)
        {
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(result);
            g.Clear(background);

            if (fit == WallpaperFit.Tile)
            {
                // Quelle auf 96 DPI normieren, sonst kachelt der TextureBrush in der DPI-Groesse der Datei.
                source.SetResolution(result.HorizontalResolution, result.VerticalResolution);
                using var brush = new TextureBrush(source, WrapMode.Tile);
                g.FillRectangle(brush, 0, 0, width, height);
                return result;
            }

            var factorX = (float)width / source.Width;
            var factorY = (float)height / source.Height;
            if (fit == WallpaperFit.Fill)
            {
                factorX = factorY = Math.Max(factorX, factorY);
            }
            else if (fit == WallpaperFit.Fit)
            {
                factorX = factorY = Math.Min(factorX, factorY);
            }
            else if (fit == WallpaperFit.Center)
            {
                factorX = factorY = 1;
            }
            var scaledWidth = source.Width * factorX;
            var scaledHeight = source.Height * factorY;

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            // TileFlipXY verhindert den hellen/dunklen Saum, den GDI+ beim Skalieren sonst am Bildrand erzeugt.
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            var corners = new PointF[]
            {
                new((width - scaledWidth) / 2, (height - scaledHeight) / 2),
                new((width + scaledWidth) / 2, (height - scaledHeight) / 2),
                new((width - scaledWidth) / 2, (height + scaledHeight) / 2)
            };
            g.DrawImage(source, corners, new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel, attributes);
            return result;
        }
    }

    private static readonly string[] FallbackFontFamilies = { "Consolas", "Segoe UI", "Tahoma", "Arial", "Verdana" };

    /// <summary>
    /// GDI+ ersetzt eine unbekannte Schrift stillschweigend durch "Microsoft Sans Serif" bzw. wirft bei
    /// ganz fehlenden Schriften eine wenig hilfreiche ArgumentException. Deshalb wird hier ausdruecklich
    /// gegen die installierten Familien geprueft: gewuenschte Schrift, dann die Fallback-Kette, dann die
    /// erste verfuegbare Systemschrift, sonst eine klare Fehlermeldung.
    /// </summary>
    private static Font CreateFontSafe(string familyName, float size, FontStyle style)
    {
        using var installed = new InstalledFontCollection();
        var families = installed.Families;

        foreach (var candidate in new[] { familyName }.Concat(FallbackFontFamilies).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var family = families.FirstOrDefault(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (family != null && TryCreateFont(family, size, style) is { } font)
            {
                return font;
            }
        }

        foreach (var family in families)
        {
            if (TryCreateFont(family, size, style) is { } font)
            {
                return font;
            }
        }

        throw new InvalidOperationException(
            "Auf diesem System ist keine einzige Systemschrift registriert - " +
            "das ist z.B. auf sehr schlanken Windows Server Core-Installationen ohne 'Desktop Experience' " +
            "moeglich. Bitte das Feature 'Desktop Experience' bzw. mindestens ein Sprachpaket/Schriftpaket installieren.");
    }

    private static Font? TryCreateFont(FontFamily family, float size, FontStyle style)
    {
        // Nicht jede Familie hat jeden Schnitt (manche z.B. nur Bold) - gewuenschten Stil, dann Regular, dann irgendeinen.
        foreach (var candidateStyle in new[] { style, FontStyle.Regular, FontStyle.Bold, FontStyle.Italic }.Distinct())
        {
            if (!family.IsStyleAvailable(candidateStyle))
            {
                continue;
            }
            try
            {
                // Groesse in Pixeln, damit das Ergebnis nicht von der DPI-Angabe des Hintergrundbilds abhaengt.
                return new Font(family, size, candidateStyle, GraphicsUnit.Pixel);
            }
            catch
            {
                // naechster Stil
            }
        }
        return null;
    }
}
