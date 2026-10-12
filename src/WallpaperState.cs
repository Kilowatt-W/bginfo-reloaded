using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>
/// Merkt sich den Pfad des urspruenglichen (unveraenderten) Wallpapers, damit jeder Lauf wieder von
/// diesem sauberen Original ausgeht statt Text auf Text auf Text vom letzten Lauf zu stapeln.
/// Genau das Problem, das PowerBGInfo mit seiner PowerBGInfoConfiguration.xml loest - hier eben als
/// simple Ein-Zeilen-Textdatei.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WallpaperState
{
    public static string StateDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BGInfo-Reloaded");

    public static string OutputImagePath => Path.Combine(StateDirectory, "wallpaper_bginfo.jpg");

    private static string OriginalPathFile => Path.Combine(StateDirectory, "original.txt");

    /// <summary>
    /// Liefert den Pfad zum unveraenderten Original-Wallpaper, oder null, wenn keins bekannt ist
    /// (dann rendert der Aufrufer auf eine synthetische Flaeche).
    /// </summary>
    public static string? ResolveOriginalWallpaper()
    {
        if (File.Exists(OriginalPathFile))
        {
            var stored = File.ReadAllText(OriginalPathFile).Trim();
            if (stored.Length > 0 && File.Exists(stored))
            {
                return stored;
            }
        }

        var current = WallpaperInterop.GetCurrentWallpaperPath();
        if (current != null && File.Exists(current) && !IsOwnOutput(current))
        {
            Directory.CreateDirectory(StateDirectory);

            // Windows-eigene Arbeitskopien (Themes\TranscodedWallpaper, CachedFiles) werden beim naechsten
            // Setzen ueberschrieben - dann wuerde "das Original" ploetzlich unser eigenes Bild enthalten.
            // Deshalb jetzt eine eigene Kopie sichern und diese merken.
            if (current.Contains(@"\Microsoft\Windows\Themes\", StringComparison.OrdinalIgnoreCase))
            {
                var copy = Path.Combine(StateDirectory, "original_copy.jpg");
                File.Copy(current, copy, overwrite: true);
                current = copy;
            }

            File.WriteAllText(OriginalPathFile, current);
            return current;
        }

        return null;
    }

    /// <summary>
    /// Wie <see cref="ResolveOriginalWallpaper"/>, aber ohne etwas zu schreiben: das gemerkte Original,
    /// sonst das gerade gesetzte Wallpaper (sofern es nicht unser eigenes Bild ist), sonst null.
    /// </summary>
    public static string? PeekOriginalWallpaper()
    {
        try
        {
            if (File.Exists(OriginalPathFile))
            {
                var stored = File.ReadAllText(OriginalPathFile).Trim();
                if (stored.Length > 0 && File.Exists(stored))
                {
                    return stored;
                }
            }
            var current = WallpaperInterop.GetCurrentWallpaperPath();
            return current != null && File.Exists(current) && !IsOwnOutput(current) ? current : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Unterordner neben der Konfiguration, in den ein uebernommenes Wallpaper kopiert wird.</summary>
    public const string AdoptedFolder = "backgrounds";

    /// <summary>Dateiname (ohne Endung) eines uebernommenen Wallpapers.</summary>
    public const string AdoptedName = "background_user";

    /// <summary>
    /// Erster Start: kopiert das gesetzte Wallpaper des Benutzers nach "backgrounds\background_user.*"
    /// neben die Konfiguration und liefert den relativen Pfad fuer wallpaper.path - so bleibt die
    /// Konfiguration samt Ordner verteilbar. Mit den Bildern je Aufloesung (background_1920x1080.jpg)
    /// kommt der Name nicht in Konflikt: die gehoeren zu "background", nicht zu "background_user".
    /// Vorhandene Dateien werden nie ueberschrieben. Null, wenn es kein Wallpaper gibt oder der
    /// Ordner nicht beschreibbar ist.
    /// </summary>
    public static string? AdoptCurrentWallpaper(string configDirectory, out string? source)
    {
        // Hier zaehlt, was gerade gesetzt ist; das gemerkte Original nur, wenn das gesetzte Bild
        // unser eigenes ist (eine alte original.txt kann auf ein laengst ersetztes Bild zeigen).
        var current = WallpaperInterop.GetCurrentWallpaperPath();
        source = current != null && File.Exists(current) && !IsOwnOutput(current) ? current : PeekOriginalWallpaper();
        if (source == null)
        {
            return null;
        }
        try
        {
            var folder = Path.Combine(configDirectory, AdoptedFolder);
            Directory.CreateDirectory(folder);
            // Windows-Arbeitskopien (TranscodedWallpaper) haben keine Endung - GDI+ liest nach Inhalt.
            var extension = Path.GetExtension(source);
            if (extension.Length is < 2 or > 5)
            {
                extension = ".jpg";
            }
            var target = Path.Combine(folder, AdoptedName + extension);
            for (var number = 2; File.Exists(target) && !SameContent(source, target); number++)
            {
                target = Path.Combine(folder, $"{AdoptedName}-{number}{extension}");
            }
            if (!File.Exists(target))
            {
                File.Copy(source, target);
            }
            return $@".\{AdoptedFolder}\{Path.GetFileName(target)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool SameContent(string first, string second) =>
        new FileInfo(first).Length == new FileInfo(second).Length
        && File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second));

    private const string OutputPrefix = "wallpaper_bginfo";

    /// <summary>
    /// Ausgabedatei fuer einen Monitor im Pro-Monitor-Betrieb. Der Name wechselt bei jedem Lauf: die
    /// Shell laedt ein Bild nicht neu, wenn fuer einen Monitor derselbe Pfad erneut gesetzt wird.
    /// </summary>
    public static string NewMonitorOutputPath(int monitorNumber) =>
        Path.Combine(StateDirectory, $"{OutputPrefix}_m{monitorNumber}_{DateTime.Now:HHmmssfff}.jpg");

    /// <summary>Alles, was die App selbst erzeugt hat - darf nie als "Original" gemerkt werden.</summary>
    public static bool IsOwnOutput(string path) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(StateDirectory), StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(path).StartsWith(OutputPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Entfernt Ausgabedateien frueherer Laeufe, die nicht mehr als Wallpaper gesetzt sind.</summary>
    public static void CleanupOldOutputs(IEnumerable<string> keep)
    {
        var inUse = new HashSet<string>(keep.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in Directory.GetFiles(StateDirectory, OutputPrefix + "*"))
            {
                if (!inUse.Contains(Path.GetFullPath(file)))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // noch in Benutzung - raeumt der naechste Lauf auf
                    }
                }
            }
        }
        catch
        {
            // Aufraeumen ist nur Hygiene
        }
    }

    /// <summary>Alle Dateien "bild_BxH.ext" (auch mit "-" oder ohne Trenner) neben dem Bild, mit ihrer Aufloesung.</summary>
    private static List<(string Path, int Width, int Height)> FindResolutionVariants(string imagePath)
    {
        var result = new List<(string, int, int)>();
        try
        {
            var directory = Path.GetDirectoryName(imagePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return result;
            }
            var name = Path.GetFileNameWithoutExtension(imagePath);
            var pattern = new System.Text.RegularExpressions.Regex(
                "^" + System.Text.RegularExpressions.Regex.Escape(name) + @"[_-]?(\d+)x(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var file in Directory.EnumerateFiles(directory, name + "*" + Path.GetExtension(imagePath)))
            {
                var match = pattern.Match(Path.GetFileNameWithoutExtension(file));
                if (match.Success && int.TryParse(match.Groups[1].Value, out var width) && int.TryParse(match.Groups[2].Value, out var height)
                    && width > 0 && height > 0)
                {
                    result.Add((file, width, height));
                }
            }
        }
        catch
        {
            // Ordner nicht lesbar - dann gibt es eben keine Varianten
        }
        return result;
    }

    /// <summary>
    /// Gibt es zu dem Bild wenigstens eine Datei mit angehaengter Aufloesung? Dann darf das Bild
    /// selbst fehlen - ein Ordner nur mit "bild_1920x1080.jpg", "bild_3840x2160.jpg" usw. genuegt.
    /// </summary>
    public static bool HasAnyResolutionVariant(string imagePath) => FindResolutionVariants(imagePath).Count > 0;

    /// <summary>
    /// Sucht zu einem Bild die Datei fuer eine Aufloesung, im selben Ordner und mit derselben Endung:
    /// 1. exakt passend ("bild_3440x1440.jpg", auch "bild-3440x1440.jpg" oder "bild3440x1440.jpg"),
    /// 2. sonst das Bild selbst, wenn es existiert,
    /// 3. sonst die am besten passende andere Aufloesung (naechstes Seitenverhaeltnis, dann moeglichst
    ///    nicht kleiner als der Monitor) - damit ein Satz von Bildern ohne "Standardbild" auf jedem
    ///    Monitor etwas Sinnvolles ergibt.
    /// </summary>
    public static string ResolveResolutionVariant(string imagePath, int width, int height)
    {
        var variants = FindResolutionVariants(imagePath);
        var exact = variants.FirstOrDefault(v => v.Width == width && v.Height == height);
        if (exact.Path != null)
        {
            return exact.Path;
        }
        if (File.Exists(imagePath) || variants.Count == 0)
        {
            return imagePath;
        }

        var aspect = (double)width / height;
        return variants
            .OrderBy(v => Math.Abs(Math.Log((double)v.Width / v.Height / aspect)))
            .ThenBy(v => v.Width >= width && v.Height >= height ? 0 : 1)
            .ThenBy(v => Math.Abs((long)v.Width * v.Height - (long)width * height))
            .First().Path;
    }
}
