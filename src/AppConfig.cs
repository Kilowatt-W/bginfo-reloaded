using System.Text.Json;
using System.Text.Json.Serialization;

namespace BgInfoReloaded;

public enum PanelPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public sealed class PanelConfig
{
    public PanelPosition Position { get; set; } = PanelPosition.TopRight;
    public int Margin { get; set; } = 40;
    public int Padding { get; set; } = 22;

    /// <summary>Panelbreite in Pixeln, 0 = automatisch aus dem laengsten Eintrag berechnen.</summary>
    public int Width { get; set; }

    /// <summary>
    /// Panelbreite als Bruchteil der Bildschirmbreite: 0 = aus, 3 bis 8 = 1/3 bis 1/8. Schrift,
    /// Abstaende, Ecken und Schatten werden dann so skaliert, dass der Inhalt diese Breite gerade
    /// fuellt - das Panel wirkt damit auf jedem Monitor gleich gross. Hat Vorrang vor Width.
    /// </summary>
    public int ScreenFraction { get; set; }

    /// <summary>Breite der Label-Spalte in Pixeln, 0 = automatisch aus dem laengsten Label berechnen.</summary>
    public int LabelWidth { get; set; }

    /// <summary>
    /// Anteil der Label-Spalte an der Innenbreite des Panels in Prozent (10 bis 90), 0 = nicht
    /// vorgegeben. Hat Vorrang vor LabelWidth. Was nicht in seine Spalte passt, wird mit "…" gekuerzt.
    /// </summary>
    public int LabelShare { get; set; }

    /// <summary>Abstand zwischen Label und Wert in Pixeln, wenn die Label-Spalte automatisch bemessen wird.</summary>
    public int ColumnGap { get; set; } = 24;
    /// <summary>
    /// Was mit Texten geschieht, die breiter sind als ihre Spalte - bei fester Panelbreite (Width),
    /// festem Spaltenanteil oder wenn das Panel an den Bildschirmrand stoesst. Bei relativer
    /// Panelbreite (ScreenFraction) wird nie umbrochen, dort passt der skalierte Inhalt.
    /// </summary>
    public TextOverflow TextOverflow { get; set; } = TextOverflow.Truncate;

    public string Background { get; set; } = "#0C0E12A5";

    /// <summary>Radius der abgerundeten Ecken des Panel-Hintergrunds in Pixeln, 0 = eckig.</summary>
    public int CornerRadius { get; set; }

    /// <summary>Wie weit der Schlagschatten rechts unten hinter dem Panel hervorschaut, in Pixeln; 0 = kein Schatten.</summary>
    public int ShadowSize { get; set; }

    /// <summary>Farbe des Schattens (#RRGGBBAA); am Rand laeuft er weich aus.</summary>
    public string ShadowColor { get; set; } = "#00000055";

    /// <summary>
    /// Abstand zum Rand ab dem Arbeitsbereich messen statt ab der Monitorkante - das Panel beginnt
    /// dann ueber (bzw. neben) der Taskleiste, statt von ihr verdeckt zu werden.
    /// </summary>
    public bool AvoidTaskbar { get; set; } = true;

    /// <summary>
    /// Alle Pixelmasse und Schriftgroessen mit der Windows-Skalierung des primaeren Monitors
    /// multiplizieren (150 % -> Faktor 1,5). Wirkt nur zusammen mit wallpaper.fit = Fill.
    /// </summary>
    public bool ScaleWithDpi { get; set; } = true;
}

public enum TextOverflow
{
    /// <summary>Text, der nicht in seine Spalte passt, endet mit "…".</summary>
    Truncate,

    /// <summary>Text wird in seiner Spalte umbrochen; die Zeile des Panels wird entsprechend hoeher.</summary>
    Wrap
}
public enum WallpaperFit
{
    /// <summary>Bild in Originalgroesse beschriften; Windows skaliert Bild samt Panel auf den Bildschirm.</summary>
    None,

    /// <summary>Bild vorab auf die Aufloesung des primaeren Monitors bringen (fuellend, mittig beschnitten).</summary>
    Fill,

    /// <summary>Ganzes Bild einpassen, nichts abschneiden; freie Raender in wallpaper.backgroundColor.</summary>
    Fit,

    /// <summary>Bild auf die Bildschirmflaeche verzerren (nichts abgeschnitten, keine Raender).</summary>
    Stretch,

    /// <summary>Bild unskaliert in die Mitte; Raender in wallpaper.backgroundColor, Ueberstand abgeschnitten.</summary>
    Center,

    /// <summary>Bild unskaliert kacheln.</summary>
    Tile
}

public enum MonitorMode
{
    /// <summary>Ein Bild fuer den primaeren Monitor; Windows zeigt dasselbe Bild auf allen Monitoren.</summary>
    Single,

    /// <summary>Je Monitor ein eigenes Bild in dessen Aufloesung und Skalierung.</summary>
    PerMonitor
}

public enum PanelMonitors
{
    /// <summary>Info-Panel auf jedem Monitor.</summary>
    All,

    /// <summary>Info-Panel nur einmal, auf dem primaeren Monitor; die uebrigen bekommen nur den Hintergrund.</summary>
    Primary
}

public enum ExcludedAction
{
    /// <summary>Hintergrund wie konfiguriert setzen, nur ohne Info-Panel (entfernt auch ein frueher gesetztes Panel).</summary>
    BackgroundOnly,

    /// <summary>Gar nichts tun - das Wallpaper des Rechners bleibt unangetastet.</summary>
    Skip
}

/// <summary>
/// White-/Blacklist von Rechnern. Ein Eintrag ist entweder
/// - ein Namensmuster mit * (beliebig viele Zeichen) und ? (genau ein Zeichen), Gross-/Kleinschreibung
///   egal, geprueft gegen Rechnername, voll qualifizierten Namen und die IP-Adressen als Text, oder
/// - eine IPv4-Angabe: einzelne Adresse (10.10.1.5), Netz (10.10.0.0/16) oder Bereich
///   (10.10.1.10-10.10.1.50, kurz 10.10.1.10-50), geprueft gegen alle IPv4-Adressen der aktiven
///   Netzwerkadapter des Rechners.
/// </summary>
public sealed class HostsConfig
{
    /// <summary>Whitelist: wenn nicht leer, erscheint das Panel nur auf Rechnern, die auf eines der Muster passen.</summary>
    public List<string> Include { get; set; } = new();

    /// <summary>Blacklist: auf passenden Rechnern erscheint das Panel nicht - hat Vorrang vor der Whitelist.</summary>
    public List<string> Exclude { get; set; } = new();

    /// <summary>
    /// Textdatei (Pfad, UNC oder http(s)-URL; relativ zum Ordner der Konfigurationsdatei) mit weiteren
    /// Eintraegen fuer die Whitelist - ein Eintrag je Zeile, wie beim Import. Sie gelten zusaetzlich zu
    /// "include" und werden bei jedem Lauf frisch gelesen: die Liste laesst sich so zentral pflegen,
    /// ohne die Konfiguration neu zu verteilen.
    /// </summary>
    public string? IncludeFile { get; set; }

    /// <summary>Wie <see cref="IncludeFile"/>, fuer die Blacklist.</summary>
    public string? ExcludeFile { get; set; }

    public ExcludedAction OnExcluded { get; set; } = ExcludedAction.BackgroundOnly;

    /// <summary>Aus IncludeFile gelesene Eintraege - nur zur Laufzeit, wird nie gespeichert.</summary>
    [JsonIgnore]
    public List<string> IncludeFromFile { get; set; } = new();

    /// <summary>Aus ExcludeFile gelesene Eintraege - nur zur Laufzeit, wird nie gespeichert.</summary>
    [JsonIgnore]
    public List<string> ExcludeFromFile { get; set; } = new();

    /// <summary>Darf das Panel auf diesem Rechner erscheinen? Wenn nicht, nennt <paramref name="reason"/> den Grund.</summary>
    public bool Allows(IReadOnlyCollection<string> hostNames, out string reason)
    {
        var addresses = LocalAddresses();
        var excludedBy = Exclude.FirstOrDefault(pattern => MatchesMachine(pattern, hostNames, addresses));
        if (excludedBy != null)
        {
            reason = $"passt auf hosts.exclude \"{excludedBy}\"";
            return false;
        }
        excludedBy = ExcludeFromFile.FirstOrDefault(pattern => MatchesMachine(pattern, hostNames, addresses));
        if (excludedBy != null)
        {
            reason = $"passt auf \"{excludedBy}\" aus hosts.excludeFile";
            return false;
        }

        var include = Include.Concat(IncludeFromFile).Where(pattern => !string.IsNullOrWhiteSpace(pattern)).ToList();
        if (include.Count > 0 && !include.Any(pattern => MatchesMachine(pattern, hostNames, addresses)))
        {
            reason = "passt auf keinen Eintrag in hosts.include" + (IncludeFromFile.Count > 0 ? " bzw. hosts.includeFile" : "");
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>Passt ein Listeneintrag (Namensmuster oder IP-Angabe) auf diesen Rechner?</summary>
    public static bool MatchesMachine(string pattern, IReadOnlyCollection<string> hostNames) =>
        MatchesMachine(pattern, hostNames, LocalAddresses());

    /// <summary>Wie oben, mit ausdruecklich uebergebenen Adressen (so auch ohne den echten Rechner pruefbar).</summary>
    public static bool MatchesMachine(string pattern, IReadOnlyCollection<string> hostNames, IReadOnlyCollection<System.Net.IPAddress> addresses)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }
        if (TryParseAddressRange(pattern, out var first, out var last))
        {
            return addresses.Any(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && ToNumber(address) is var number && number >= first && number <= last);
        }
        // Namensmuster: auch gegen die Adressen als Text, damit z.B. "10.10.*" funktioniert.
        return hostNames.Any(name => Matches(pattern, name)) || addresses.Any(address => Matches(pattern, address.ToString()));
    }

    private static readonly System.Text.RegularExpressions.Regex DottedQuad =
        new(@"^\d{1,3}(\.\d{1,3}){3}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Erkennt 10.10.1.5, 10.10.0.0/16, 10.10.1.10-10.10.1.50 und 10.10.1.10-50. Bewusst streng (vier
    /// Zahlen mit Punkten), damit ein Rechnername wie "pc-10" oder "10-20" ein Namensmuster bleibt.
    /// </summary>
    public static bool TryParseAddressRange(string entry, out uint first, out uint last)
    {
        first = last = 0;
        var text = entry.Replace(" ", "");

        var slash = text.IndexOf('/');
        if (slash > 0)
        {
            if (!TryParseQuad(text[..slash], out var network) || !int.TryParse(text[(slash + 1)..], out var bits) || bits is < 0 or > 32)
            {
                return false;
            }
            var mask = bits == 0 ? 0u : uint.MaxValue << (32 - bits);
            first = network & mask;
            last = first | ~mask;
            return true;
        }

        var dash = text.IndexOf('-');
        if (dash > 0)
        {
            if (!TryParseQuad(text[..dash], out first))
            {
                return false;
            }
            var end = text[(dash + 1)..];
            if (TryParseQuad(end, out last))
            {
                return last >= first;
            }
            // Kurzform: nur das letzte Oktett des Endes.
            if (byte.TryParse(end, out var lastOctet))
            {
                last = (first & 0xFFFFFF00u) | lastOctet;
                return last >= first;
            }
            return false;
        }

        if (TryParseQuad(text, out first))
        {
            last = first;
            return true;
        }
        return false;
    }

    private static bool TryParseQuad(string text, out uint number)
    {
        number = 0;
        if (!DottedQuad.IsMatch(text))
        {
            return false;
        }
        foreach (var part in text.Split('.'))
        {
            if (!byte.TryParse(part, out var octet))
            {
                return false;
            }
            number = (number << 8) | octet;
        }
        return true;
    }

    private static uint ToNumber(System.Net.IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static (DateTime ReadAt, List<System.Net.IPAddress> Addresses) _localAddresses;

    /// <summary>
    /// Alle Adressen der aktiven Netzwerkadapter (ohne Loopback). Kurz zwischengespeichert, weil die
    /// Zeilenfilter das fuer jede Zeile fragen; eine --watch-Instanz sieht einen Adresswechsel trotzdem.
    /// </summary>
    private static IReadOnlyCollection<System.Net.IPAddress> LocalAddresses()
    {
        if (_localAddresses.Addresses != null && (DateTime.UtcNow - _localAddresses.ReadAt).TotalSeconds < 10)
        {
            return _localAddresses.Addresses;
        }

        var result = new List<System.Net.IPAddress>();
        try
        {
            foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up
                    || adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                result.AddRange(adapter.GetIPProperties().UnicastAddresses.Select(unicast => unicast.Address));
            }
        }
        catch
        {
            // ohne Netzwerkinformationen passen eben nur Namensmuster
        }
        _localAddresses = (DateTime.UtcNow, result);
        return result;
    }

    /// <summary>
    /// Liest eine Liste aus Text: ein Eintrag je Zeile (auch durch Komma oder Semikolon getrennt),
    /// Leerzeilen und Kommentarzeilen (# oder //) werden uebergangen, Doppelte entfernt.
    /// </summary>
    public static List<string> ParseList(string text)
    {
        var entries = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//"))
            {
                continue;
            }
            entries.AddRange(line.Split(',', ';'));
        }
        return Normalize(entries);
    }

    /// <summary>
    /// Bereinigt eine Liste: Leerzeichen am Rand weg, leere Eintraege weg, Doppelte weg (Gross-/
    /// Kleinschreibung egal - der erste Eintrag bleibt in seiner Schreibweise an seiner Stelle).
    /// </summary>
    public static List<string> Normalize(IEnumerable<string> entries)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Select(e => e?.Trim() ?? "").Where(e => e.Length > 0))
        {
            if (seen.Add(entry))
            {
                result.Add(entry);
            }
        }
        return result;
    }

    public static bool Matches(string pattern, string name)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}

public sealed class MonitorsConfig
{
    public MonitorMode Mode { get; set; } = MonitorMode.PerMonitor;

    /// <summary>Wirkt nur bei Mode = PerMonitor.</summary>
    public PanelMonitors Panel { get; set; } = PanelMonitors.All;
}

public sealed class FontConfig
{
    public string Family { get; set; } = "Consolas";
    public float Size { get; set; } = 15;
    public float HeaderSize { get; set; } = 19;
}

public sealed class ColorConfig
{
    public string Header { get; set; } = "#FFD700";
    public string Label { get; set; } = "#FFFFFF";
    public string Value { get; set; } = "#82DCD2"; // Aquamarine-Ton, PowerBGInfo-Setup nachempfunden
}

/// <summary>Wie das Bild fuer einen Monitor gefunden wurde (siehe <see cref="AppConfig.ResolveWallpaperFor"/>).</summary>
public enum WallpaperChoice
{
    None,
    Override,
    ExactVariant,
    Base,
    BestFit
}

public sealed class WallpaperConfig
{
    /// <summary>
    /// Festes Hintergrundbild (z.B. Firmen-Wallpaper auf einem Share). Leer = aktuelles Wallpaper des
    /// Benutzers verwenden (ueber WallpaperState, damit nicht Text auf Text gestapelt wird).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Nur zur Information: woher das Bild in <see cref="Path"/> stammt, wenn es beim ersten Start
    /// vom gesetzten Wallpaper des Benutzers uebernommen wurde. Wird nirgends ausgewertet.
    /// </summary>
    public string? OriginalPath { get; set; }

    /// <summary>
    /// Fill (Standard): das Panel wird pixelgenau fuer die aktuelle Aufloesung gerendert - Voraussetzung
    /// dafuer, dass ein Neu-Rendern bei Aufloesungswechsel (--watch) ueberhaupt etwas aendert.
    /// </summary>
    public WallpaperFit Fit { get; set; } = WallpaperFit.Fill;

    /// <summary>
    /// Je Monitor wird zuerst eine Datei mit angehaengter Aufloesung gesucht (bild_3440x1440.jpg,
    /// auch bild-3440x1440.jpg oder bild3440x1440.jpg); fehlt sie, gilt das Bild ohne Zusatz.
    /// Standardmaessig an: wer solche Dateien neben das Bild legt, erwartet, dass sie benutzt werden,
    /// und ohne solche Dateien aendert sich nichts.
    /// </summary>
    public bool ResolutionVariants { get; set; } = true;

    /// <summary>
    /// Eigene Zuordnung je Aufloesung: "3440x1440" -> Bilddatei (relativ zum Ordner der Konfiguration).
    /// Hat Vorrang vor den Dateien mit angehaengter Aufloesung - fuer Aufloesungen, zu denen es im
    /// Ordner kein passendes Bild gibt. Null/leer = keine.
    /// </summary>
    public Dictionary<string, string>? ByResolution { get; set; }

    /// <summary>Hintergrundfarbe fuer Raender (Fit, Center) und falls gar kein Bild vorhanden/lesbar ist.</summary>
    public string BackgroundColor { get; set; } = "#101218";
}

/// <summary>
/// Ein Eintrag im Panel. Genau eine der Varianten: "section" (Ueberschrift), "spacer" (Leerraum),
/// "drives" (eine Zeile je Festplatte) oder "label"/"value" (normale Zeile mit Platzhaltern).
/// </summary>
public sealed class ItemConfig
{
    // Nur hier werden false-Werte beim Schreiben weggelassen (alle drei haben false als Standard) -
    // sonst stuende in jeder Zeile "spacer": false, "drives": false, "hideIfEmpty": false.
    public string? Section { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Spacer { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Drives { get; set; }

    public string? Label { get; set; }
    public string? Value { get; set; }

    /// <summary>Zeile weglassen, wenn der Wert leer oder "n/a" ist (z.B. Umgebungsvariable nur auf manchen Rechnern).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HideIfEmpty { get; set; }

    /// <summary>
    /// Trennzeichen, an denen der aufgeloeste Wert geteilt wird: der erste Teil steht neben der
    /// Bezeichnung, jeder weitere in einer eigenen Zeile darunter (z.B. ", " fuer mehrere
    /// Grafikkarten). Mehrere Trennzeichen mit | getrennt (", |;"). Leer = nicht teilen.
    /// </summary>
    public string? Split { get; set; }

    /// <summary>Platzhalter-Zeile: an dieser Stelle erscheinen die "items" der per "include" eingebundenen zentralen Datei.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeItems { get; set; }

    /// <summary>
    /// Beginn eines Blocks (der Text ist nur ein Name zur Orientierung, auch leer erlaubt): alle
    /// folgenden Eintraege bis zum zugehoerigen { "endBlock": true } erscheinen nur, wenn die Filter
    /// dieses Eintrags zutreffen. Bloecke duerfen verschachtelt werden.
    /// </summary>
    public string? Block { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EndBlock { get; set; }

    // Filter - gelten fuer jede Art von Eintrag. Alle angegebenen Filter muessen zutreffen.

    /// <summary>Nur auf Rechnern, deren Name auf eines der Muster passt (* und ?, wie hosts.include).</summary>
    public List<string>? OnlyOn { get; set; }

    /// <summary>Nicht auf Rechnern, deren Name auf eines der Muster passt.</summary>
    public List<string>? NotOn { get; set; }

    /// <summary>
    /// Bedingung mit Platzhaltern: "{systemtype} = Server", "{env:STANDORT} != Berlin", rechts sind * und ?
    /// erlaubt. Ohne Vergleich gilt die Bedingung als erfuellt, wenn der Text einen Wert liefert
    /// (nicht leer, nicht "n/a").
    /// </summary>
    public string? When { get; set; }

    [JsonIgnore]
    public bool HasFilter => OnlyOn is { Count: > 0 } || NotOn is { Count: > 0 } || !string.IsNullOrWhiteSpace(When);
}

/// <summary>Selbst-Update aus einem Ordner (siehe FolderUpdater).</summary>
/// <summary>Art der Rahmenlinie.</summary>
public enum BorderLine
{
    Solid,
    Dotted,
    Dashed
}

/// <summary>Worum der Rahmen gezogen wird.</summary>
public enum BorderScope
{
    /// <summary>Ein Rahmen um das ganze Panel.</summary>
    Panel,

    /// <summary>Je Abschnitt (Ueberschrift mit ihren Zeilen) ein Kasten.</summary>
    Sections,

    /// <summary>Ein Gitter um jede Zeile mit Trennlinie zwischen Bezeichnung und Wert.</summary>
    Rows
}

/// <summary>Rahmen um das Panel, die Abschnitte oder die einzelnen Zeilen. Breite 0 = kein Rahmen.</summary>
public sealed class BorderConfig
{
    public string Color { get; set; } = "#FFFFFF80";

    /// <summary>Strichstaerke in Pixeln (wird wie das Panel skaliert); 0 = kein Rahmen.</summary>
    public int Width { get; set; }

    public BorderLine Style { get; set; } = BorderLine.Solid;

    public BorderScope Scope { get; set; } = BorderScope.Panel;
}

/// <summary>Darstellung von Datums- und Zahlenwerten der Platzhalter (siehe <see cref="ValueFormat"/>).</summary>
public sealed class FormatConfig
{
    /// <summary>Reines Datum (BIOS-Datum, letztes Update), .NET-Formatangabe.</summary>
    public string Date { get; set; } = "yyyy-MM-dd";

    /// <summary>Datum mit Uhrzeit (letzter Boot, {now}, Dateidatum), .NET-Formatangabe.</summary>
    public string DateTime { get; set; } = "yyyy-MM-dd HH:mm";

    /// <summary>Hoechstzahl der Nachkommastellen bei Groessen und Geschwindigkeiten (GB, Gbit/s).</summary>
    public int Decimals { get; set; } = 1;

    /// <summary>
    /// Kultur fuer Dezimaltrennzeichen sowie Monats- und Tagesnamen ("de-DE", "en-US"); "system" =
    /// die des jeweiligen Windows; leer = neutral (Punkt, englische Namen) wie vor dieser Einstellung.
    /// </summary>
    public string? Culture { get; set; }
}

public sealed class UpdateConfig
{
    /// <summary>
    /// Ordner (lokal oder UNC-Share; relativ zum Ordner der Konfigurationsdatei), in dem die jeweils
    /// aktuelle BGInfo-Reloaded.exe liegt. Leer = kein Update aus einem Ordner.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Bei jedem normalen Lauf pruefen und eine neuere Version von selbst holen. Aus = nur auf Aufruf
    /// von --update. Wer in diesen Ordner schreiben darf, bestimmt, welches Programm auf den Rechnern
    /// laeuft - der Ordner muss entsprechend geschuetzt sein.
    /// </summary>
    public bool Auto { get; set; }

    /// <summary>BGInfo-Reloaded.exe aus dem Ordner holen, wenn dort eine hoehere Version liegt.</summary>
    public bool Program { get; set; } = true;

    /// <summary>BGInfo-Reloaded-Editor.exe mit aktualisieren - nur dort, wo der Editor schon neben der exe liegt.</summary>
    public bool Editor { get; set; } = true;

    /// <summary>
    /// BGInfo-Reloaded.json aus dem Ordner neben die exe holen, wenn sie dort neuer ist (die vorherige
    /// bleibt als .bak). Die geholte Datei gilt ab dem naechsten Lauf - sie sollte denselben
    /// "update"-Abschnitt enthalten, sonst endet das Abgleichen mit ihr.
    /// </summary>
    public bool Config { get; set; }

    /// <summary>background*.* und den Unterordner backgrounds\ aus dem Ordner neben die exe holen (neuere und fehlende Dateien).</summary>
    public bool Backgrounds { get; set; }

    /// <summary>
    /// GitHub-Repository ("owner/name") fuer --update aus Releases - der zentral verteilbare Ersatz
    /// fuer --repo. Wird nur auf Aufruf von --update benutzt, nie automatisch (GitHub begrenzt
    /// anonyme Abfragen je Internet-Anschluss).
    /// </summary>
    public string? Repo { get; set; }
}

/// <summary>Was aus einer per "include" eingebundenen Datei uebernommen wird - bewusst nur Felder und Zeilen.</summary>
public sealed class IncludeFile
{
    public Dictionary<string, string>? Fields { get; set; }
    public List<ItemConfig>? Items { get; set; }
}

public sealed class AppConfig
{
    public const string FileName = "BGInfo-Reloaded.json";
    public const string EnvironmentVariable = "BGINFORELOADED_CONFIG";

    /// <summary>Frei waehlbare Version der Konfigurationsdatei; im Panel als {configversion} nutzbar.</summary>
    public string? Version { get; set; }

    /// <summary>Eigene Anmerkungen zur Datei - der Ersatz fuer eigene Kommentare, die der Editor nicht erhalten kann.</summary>
    public string? Description { get; set; }

    public PanelConfig Panel { get; set; } = new();
    public FontConfig Font { get; set; } = new();
    public ColorConfig Colors { get; set; } = new();
    public WallpaperConfig Wallpaper { get; set; } = new();
    public MonitorsConfig Monitors { get; set; } = new();
    public HostsConfig Hosts { get; set; } = new();
    public UpdateConfig Update { get; set; } = new();
    public FormatConfig Format { get; set; } = new();
    public BorderConfig Border { get; set; } = new();

    /// <summary>
    /// Zentrale Datei (Pfad, UNC oder http(s)-URL; relativ zu <see cref="BaseDirectory"/>) mit
    /// gemeinsam genutzten "fields" und "items". Ihre Felder gelten zusaetzlich zu den eigenen (bei
    /// gleichem Namen gewinnt die eigene Definition); ihre Zeilen erscheinen dort, wo in den eigenen
    /// "items" ein Eintrag { "includeItems": true } steht.
    /// </summary>
    public string? Include { get; set; }

    /// <summary>
    /// Eigene, benannte Platzhalter: "modell": "{wmi:Win32_ComputerSystem.Model}" macht {modell}
    /// in allen Zeilen verfuegbar. Die Definition darf selbst Platzhalter und freien Text enthalten.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    public List<ItemConfig> Items { get; set; } = DefaultItems();

    /// <summary>Zeilen der eingebundenen Datei - gefuellt von <see cref="ResolveInclude"/>.</summary>
    [JsonIgnore]
    public List<ItemConfig> IncludedItems { get; private set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        // Nicht WhenWritingDefault: mehrere Einstellungen haben einen Standard ungleich 0/false
        // (scaleWithDpi = true, position = TopRight, fit = Fill, mode = PerMonitor). Wuerde false bzw.
        // der erste Enum-Wert beim Schreiben weggelassen, kaeme beim Laden wieder der Standard heraus.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string ExeDirectory =>
        // Nicht AppContext.BaseDirectory: bei Single-File mit Self-Extract zeigt das ins Temp-Entpackverzeichnis.
        Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;

    private static string CacheFile => Path.Combine(WallpaperState.StateDirectory, "config.cache.json");

    private static string IncludeCacheFile => Path.Combine(WallpaperState.StateDirectory, "include.cache.json");

    private static List<ItemConfig> DefaultItems() => new()
    {
        new() { Section = "SYSTEM" },
        new() { Label = "Hostname", Value = "{hostname}" },
        new() { Label = "FQDN", Value = "{fqdn}" },
        new() { Label = "Angemeldet als", Value = "{user}" },
        new() { Label = "IPv4", Value = "{ipv4}" },
        new() { Label = "Subnetzmaske", Value = "{subnet}" },
        new() { Label = "Betriebssystem", Value = "{os}" },
        new() { Label = "OS-Version", Value = "{osversion}" },
        new() { Label = "Build", Value = "{build}" },
        new() { Label = "Architektur", Value = "{arch}" },
        new() { Label = "Letzter Boot", Value = "{boot}" },
        new() { Label = "Laufzeit", Value = "{uptime}" },
        new() { Spacer = true },
        new() { Section = "HARDWARE" },
        new() { Label = "CPU", Value = "{cpu}" },
        new() { Label = "Logische Kerne", Value = "{cores}" },
        new() { Label = "RAM", Value = "{ram}" },
        new() { Label = "RAM-Takt", Value = "{ramspeed}" },
        new() { Label = "BIOS", Value = "{bios}" },
        new() { Spacer = true },
        new() { Section = "LAUFWERKE" },
        new() { Drives = true }
    };

    /// <summary>
    /// Ordner, auf den sich relative Pfade in der Konfiguration beziehen: der Ordner der
    /// Konfigurationsdatei, bei URL oder eingebauten Standardwerten der Ordner der exe.
    /// </summary>
    [JsonIgnore]
    public string BaseDirectory { get; set; } = ExeDirectory;

    /// <summary>
    /// wallpaper.path mit aufgeloesten Umgebungsvariablen; ein relativer Pfad (".\bild.jpg") gilt
    /// relativ zu <see cref="BaseDirectory"/> - nicht zum Arbeitsverzeichnis, das im Scheduled Task
    /// ein anderes ist. Null, wenn kein Bild konfiguriert ist.
    /// </summary>
    public string? ResolveWallpaperPath()
    {
        if (string.IsNullOrWhiteSpace(Wallpaper.Path))
        {
            return null;
        }
        var path = Environment.ExpandEnvironmentVariables(Wallpaper.Path.Trim());
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(BaseDirectory, path));
    }

    /// <summary>Kommentiertes JSON - siehe <see cref="ConfigWriter"/>.</summary>
    /// <summary>Einheitliche Schreibweise einer Aufloesung als Schluessel: "3440x1440" (auch aus "3440 × 1440"); null, wenn es keine ist.</summary>
    public static string? NormalizeResolution(string? text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text ?? "", @"^\s*(\d{2,5})\s*[xX×*]\s*(\d{2,5})\s*$");
        return match.Success ? $"{int.Parse(match.Groups[1].Value)}x{int.Parse(match.Groups[2].Value)}" : null;
    }

    /// <summary>Die eigenen Zuordnungen aus wallpaper.byResolution mit aufgeloesten Pfaden, in der Reihenfolge der Datei.</summary>
    public List<(string Resolution, string Path)> WallpaperOverrides()
    {
        var result = new List<(string, string)>();
        foreach (var (key, value) in Wallpaper.ByResolution ?? new())
        {
            if (NormalizeResolution(key) is { } resolution && !string.IsNullOrWhiteSpace(value))
            {
                var path = Environment.ExpandEnvironmentVariables(value.Trim());
                result.Add((resolution, Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(BaseDirectory, path))));
            }
        }
        return result;
    }

    /// <summary>
    /// Welches Bild ein Monitor dieser Aufloesung bekommt und warum. Reihenfolge: eigene Zuordnung
    /// (wallpaper.byResolution), Datei mit dieser Aufloesung im Namen, das Bild ohne Zusatz, die am
    /// besten passende andere Fassung. <paramref name="baseImage"/> ist das Standardbild (wallpaper.path
    /// bzw. das Wallpaper des Benutzers) oder null.
    /// </summary>
    public (string? Image, WallpaperChoice Choice) ResolveWallpaperFor(string? baseImage, int width, int height)
    {
        var key = $"{width}x{height}";
        foreach (var (resolution, path) in WallpaperOverrides())
        {
            if (resolution == key)
            {
                if (File.Exists(path))
                {
                    return (path, WallpaperChoice.Override);
                }
                Console.WriteLine($"Warnung: wallpaper.byResolution[{key}] '{path}' nicht gefunden - verwende das Standardbild.");
                break;
            }
        }

        if (baseImage == null)
        {
            return (null, WallpaperChoice.None);
        }
        if (!Wallpaper.ResolutionVariants)
        {
            return File.Exists(baseImage) ? (baseImage, WallpaperChoice.Base) : (null, WallpaperChoice.None);
        }
        var image = WallpaperState.ResolveResolutionVariant(baseImage, width, height);
        if (!File.Exists(image))
        {
            return (null, WallpaperChoice.None);
        }
        if (string.Equals(image, baseImage, StringComparison.OrdinalIgnoreCase))
        {
            return (image, WallpaperChoice.Base);
        }
        return (image, Path.GetFileNameWithoutExtension(image).EndsWith(key, StringComparison.OrdinalIgnoreCase)
            ? WallpaperChoice.ExactVariant
            : WallpaperChoice.BestFit);
    }

    /// <summary>update.path mit aufgeloesten Umgebungsvariablen, relativ zu <see cref="BaseDirectory"/>; null, wenn keiner konfiguriert ist.</summary>
    public string? ResolveUpdatePath()
    {
        if (string.IsNullOrWhiteSpace(Update.Path))
        {
            return null;
        }
        var path = Environment.ExpandEnvironmentVariables(Update.Path.Trim());
        return IsUrl(path) || Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(BaseDirectory, path));
    }

    /// <param name="withPlaceholderList">
    /// Vor den Zeilen eine Kommentarliste aller Platzhalter einfuegen (fuer die Beispielkonfiguration
    /// eines Release - so ist sie immer auf dem Stand des Programms).
    /// </param>
    public string ToJson(bool withPlaceholderList = false) => ConfigWriter.Write(this, JsonOptions, withPlaceholderList);

    /// <summary>Enthaelt der Text Kommentare, die nicht die Standardkommentare von <see cref="ToJson"/> sind?</summary>
    public static bool HasOwnComments(string json) => ConfigWriter.HasForeignComments(json);

    /// <summary>Liest eine Konfiguration aus JSON-Text, ohne Suchreihenfolge und ohne Cache (fuer den Editor).</summary>
    public static AppConfig FromJson(string json, string sourceName) => Parse(json, sourceName);

    private static string DirectoryOfSource(string source)
    {
        if (IsUrl(source))
        {
            return ExeDirectory;
        }
        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(source)) ?? ExeDirectory;
        }
        catch
        {
            return ExeDirectory;
        }
    }

    /// <summary>
    /// Laedt die Konfiguration. Reihenfolge: --config, Umgebungsvariable BGINFORELOADED_CONFIG,
    /// BGInfo-Reloaded.json im Startverzeichnis, neben der exe, unter %ProgramData%\BGInfo-Reloaded
    /// (siehe <see cref="FindDefaultFile"/>), eingebaute Standardwerte.
    /// Explizite Quellen (--config/Umgebungsvariable) duerfen ein lokaler Pfad, ein UNC-Pfad oder eine
    /// http(s)-URL sein; die zuletzt erfolgreich geladene Fassung wird lokal gecacht, damit z.B. ein
    /// Notebook ohne Netzverbindung weiter mit der zentralen Konfiguration rendert.
    /// </summary>
    public static (AppConfig Config, string Source) Load(string? explicitSource)
    {
        var (config, source) = LoadFile(explicitSource);
        if (config.ResolveInclude() is { } note)
        {
            Console.WriteLine(note);
        }
        foreach (var hostNote in config.ResolveHostFiles())
        {
            Console.WriteLine(hostNote);
        }
        return (config, source);
    }

    /// <summary>
    /// Liest die Listendateien aus hosts.includeFile / hosts.excludeFile in die Laufzeitlisten
    /// (IncludeFromFile / ExcludeFromFile). Ist eine Datei nicht erreichbar, gilt die zuletzt gelesene
    /// Fassung aus dem lokalen Cache; gibt es keinen, fehlen ihre Eintraege (mit Warnung). Liefert
    /// Hinweistexte fuer das Log. Der Editor ruft das nur fuer die Vorschau-Kopie auf.
    /// </summary>
    public List<string> ResolveHostFiles()
    {
        var notes = new List<string>();
        Hosts.IncludeFromFile = ReadHostFile(Hosts.IncludeFile, "Whitelist", "hosts.include", notes);
        Hosts.ExcludeFromFile = ReadHostFile(Hosts.ExcludeFile, "Blacklist", "hosts.exclude", notes);
        return notes;
    }

    private List<string> ReadHostFile(string? configured, string what, string cacheName, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new List<string>();
        }

        var source = Environment.ExpandEnvironmentVariables(configured.Trim());
        if (!IsUrl(source) && !Path.IsPathRooted(source))
        {
            source = Path.GetFullPath(Path.Combine(BaseDirectory, source));
        }

        // Der Cache gehoert zu genau dieser Quelle (Kennung aus dem Pfad im Dateinamen): wird spaeter
        // eine andere Datei eingetragen, darf nicht der Inhalt der alten als Ersatz dienen.
        var sourceId = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(source.ToLowerInvariant())))[..8];
        var cacheFile = Path.Combine(WallpaperState.StateDirectory, $"{cacheName}.{sourceId}.cache.txt");
        try
        {
            var text = ReadSource(source);
            TryWriteCache(cacheFile, text);
            var entries = HostsConfig.ParseList(text);
            notes.Add($"{what}-Datei: {source} ({entries.Count} Eintraege)");
            return entries;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            if (!File.Exists(cacheFile))
            {
                notes.Add($"Warnung: {what}-Datei '{source}' nicht erreichbar ({ex.Message}) und kein Cache vorhanden - ihre Eintraege fehlen.");
                return new List<string>();
            }
            var entries = HostsConfig.ParseList(File.ReadAllText(cacheFile));
            notes.Add($"Warnung: {what}-Datei '{source}' nicht erreichbar ({ex.Message}) - verwende lokalen Cache ({entries.Count} Eintraege).");
            return entries;
        }
    }

    /// <summary>
    /// Liest die per "include" genannte zentrale Datei und uebernimmt deren Felder und Zeilen in diese
    /// (Laufzeit-)Konfiguration. Ist die Datei nicht erreichbar, gilt die zuletzt gelesene Fassung aus
    /// dem lokalen Cache. Liefert einen Hinweistext fuer das Log, oder null ohne "include".
    /// Der Editor ruft das nur fuer die Vorschau-Kopie auf - nie fuer das Objekt, das gespeichert wird.
    /// </summary>
    public string? ResolveInclude()
    {
        if (string.IsNullOrWhiteSpace(Include))
        {
            return null;
        }

        var source = Environment.ExpandEnvironmentVariables(Include.Trim());
        if (!IsUrl(source) && !Path.IsPathRooted(source))
        {
            source = Path.GetFullPath(Path.Combine(BaseDirectory, source));
        }

        string json;
        string note;
        try
        {
            json = ReadSource(source);
            note = $"Zentrale Datei: {source}";
            TryWriteCache(IncludeCacheFile, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            if (!File.Exists(IncludeCacheFile))
            {
                return $"Warnung: zentrale Datei '{source}' nicht erreichbar ({ex.Message}) und kein Cache vorhanden - ihre Felder und Zeilen fehlen.";
            }
            json = File.ReadAllText(IncludeCacheFile);
            note = $"Warnung: zentrale Datei '{source}' nicht erreichbar ({ex.Message}) - verwende lokalen Cache.";
        }

        IncludeFile? central;
        try
        {
            central = JsonSerializer.Deserialize<IncludeFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Zentrale Datei '{source}' ist kein gueltiges JSON: {ex.Message}");
        }

        foreach (var (name, definition) in central?.Fields ?? new())
        {
            if (!Fields.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)))
            {
                Fields[name] = definition;
            }
        }
        // Keine Verschachtelung: ein "includeItems" in der zentralen Datei haette nichts, worauf es zeigt.
        IncludedItems = (central?.Items ?? new()).Where(item => !item.IncludeItems).ToList();
        return $"{note} ({central?.Fields?.Count ?? 0} Felder, {IncludedItems.Count} Zeilen)";
    }

    /// <summary>Wie <see cref="Load"/>, aber ohne die zentrale Datei einzumischen - so, wie die Datei selbst aussieht.</summary>
    public static (AppConfig Config, string Source) LoadWithoutInclude(string? explicitSource) => LoadFile(explicitSource);

    private static (AppConfig Config, string Source) LoadFile(string? explicitSource)
    {
        var central = explicitSource;
        if (string.IsNullOrWhiteSpace(central))
        {
            central = Environment.GetEnvironmentVariable(EnvironmentVariable);
        }

        if (!string.IsNullOrWhiteSpace(central))
        {
            central = Environment.ExpandEnvironmentVariables(central.Trim());
            string json;
            try
            {
                json = ReadSource(central);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
            {
                if (File.Exists(CacheFile))
                {
                    Console.WriteLine($"Warnung: Konfiguration '{central}' nicht erreichbar ({ex.Message}) - verwende lokalen Cache.");
                    var cached = Parse(File.ReadAllText(CacheFile), CacheFile);
                    cached.BaseDirectory = DirectoryOfSource(central);
                    return (cached, $"{CacheFile} (Cache von {central})");
                }
                Console.WriteLine($"Warnung: Konfiguration '{central}' nicht erreichbar ({ex.Message}) und kein Cache vorhanden - verwende Standardwerte.");
                return (new AppConfig(), "eingebaute Standardwerte");
            }

            var config = Parse(json, central);
            TryWriteCache(CacheFile, json);
            return (config, central);
        }

        if (FindDefaultFile() is { } found)
        {
            return (Parse(File.ReadAllText(found), found), found);
        }

        return (new AppConfig(), "eingebaute Standardwerte");
    }

    /// <summary>
    /// Die BGInfo-Reloaded.json, die ohne ausdrueckliche Angabe gilt - fuer die exe und den Editor
    /// dieselbe: zuerst im Startverzeichnis (aktuelles Arbeitsverzeichnis beim Aufruf), dann neben
    /// der exe, dann unter %ProgramData%\BGInfo-Reloaded. Null, wenn es keine gibt.
    /// </summary>
    public static string? FindDefaultFile()
    {
        var candidates = new List<string>();
        try
        {
            candidates.Add(Path.Combine(Environment.CurrentDirectory, FileName));
        }
        catch
        {
            // Arbeitsverzeichnis nicht mehr vorhanden oder nicht lesbar - dann eben ohne
        }
        candidates.Add(Path.Combine(ExeDirectory, FileName));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BGInfo-Reloaded", FileName));
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Konfiguration fuer den ersten Start: Standardwerte, dazu das gerade gesetzte Wallpaper des
    /// Benutzers - als Kopie im Unterordner "backgrounds" von <paramref name="directory"/> und als
    /// relativer wallpaper.path. <paramref name="adoptedFrom"/> ist die Herkunft des Bilds oder null,
    /// wenn nichts uebernommen wurde (kein Wallpaper gesetzt, Ordner nicht beschreibbar).
    /// </summary>
    public static AppConfig CreateInitial(string directory, out string? adoptedFrom)
    {
        var config = new AppConfig { BaseDirectory = directory };
        if (WallpaperState.AdoptCurrentWallpaper(directory, out adoptedFrom) is { } relative)
        {
            config.Wallpaper.Path = relative;
            config.Wallpaper.OriginalPath = adoptedFrom;
        }
        else
        {
            adoptedFrom = null;
        }
        return config;
    }

    /// <summary>
    /// Erster Lauf der exe ganz ohne Konfiguration (keine Angabe, keine Umgebungsvariable, keine
    /// Datei an den Standardorten): legt neben der exe eine BGInfo-Reloaded.json mit dem
    /// uebernommenen Wallpaper an. Nur wenn es ein Wallpaper gibt und der Ordner beschreibbar ist -
    /// sonst bleibt es bei den eingebauten Standardwerten. Liefert die angelegte Datei oder null.
    /// </summary>
    public static string? TryCreateInitialFile()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)) || FindDefaultFile() != null)
        {
            return null;
        }
        var config = CreateInitial(ExeDirectory, out var adoptedFrom);
        if (adoptedFrom == null)
        {
            return null;
        }
        try
        {
            var file = Path.Combine(ExeDirectory, FileName);
            File.WriteAllText(file, config.ToJson());
            Console.WriteLine($"Erster Start: gesetztes Wallpaper übernommen ({adoptedFrom} -> {config.Wallpaper.Path}), Konfiguration angelegt: {file}");
            return file;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Hinweis: Konfiguration konnte nicht neben der exe angelegt werden ({ex.Message}) - verwende Standardwerte.");
            return null;
        }
    }

    /// <summary>
    /// Was der Editor beim Start ohne Dateiangabe oeffnet: die per Umgebungsvariable genannte Datei
    /// (sofern lokal und vorhanden), sonst <see cref="FindDefaultFile"/>.
    /// </summary>
    public static string? FindFileForEditor()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            var path = Environment.ExpandEnvironmentVariables(fromEnvironment.Trim());
            if (!IsUrl(path) && File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }
        return FindDefaultFile();
    }

    public static bool IsUrl(string source) =>
        source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string ReadSource(string source)
    {
        if (!IsUrl(source))
        {
            return File.ReadAllText(source);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"BGInfo-Reloaded/{GitHubUpdater.CurrentVersion}");
        return http.GetStringAsync(source).GetAwaiter().GetResult();
    }

    private static AppConfig Parse(string json, string source)
    {
        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                ?? throw new JsonException("Datei ist leer.");
            config.BaseDirectory = DirectoryOfSource(source);
            config.Fields ??= new();
            config.Items ??= new();
            config.Format ??= new();
            config.Border ??= new();
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Konfiguration '{source}' ist kein gueltiges JSON: {ex.Message}");
        }
    }

    private static void TryWriteCache(string file, string json)
    {
        try
        {
            Directory.CreateDirectory(WallpaperState.StateDirectory);
            File.WriteAllText(file, json);
        }
        catch
        {
            // Cache ist nur Komfort - ein Schreibfehler darf den Lauf nicht verhindern.
        }
    }
}
