using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>Hoehe der Balken ("bar" einer Zeile) in Pixeln; wird wie das Panel skaliert.</summary>
    public int BarHeight { get; set; } = 8;

    /// <summary>Staerke eines Trennstrichs ("separator" einer Zeile) in Pixeln; wird wie das Panel skaliert.</summary>
    public int SeparatorWidth { get; set; } = 1;

    /// <summary>
    /// Gesamt-Deckkraft des Panels in Prozent (0 bis 100): verrechnet sich mit allen eingestellten
    /// Deckkraeften (Hintergrund, Schatten, Texte, Balken, Rahmen). 100 = wie eingestellt.
    /// </summary>
    public int Opacity { get; set; } = 100;
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
/// <summary>Was fuer die Rechner einer Gruppe gilt.</summary>
public enum HostGroupMode
{
    /// <summary>Mit "config": diese Datei, mit "profile": dieses Profil, ohne beides: das Standardprofil. (Bis 2.18 hiess ohne "config" kein Panel.)</summary>
    Auto,

    /// <summary>Wie Auto ohne Datei: Profil bzw. Standardprofil. Schreibt der Editor fuer beides, damit Versionen vor 2.19 (ohne Profile) wenigstens das Standardprofil nehmen.</summary>
    ThisConfig,

    /// <summary>Kein Panel, auch wenn eine Datei oder ein Profil eingetragen ist.</summary>
    Exclude
}

/// <summary>
/// Eine Gruppe von Rechnern (Namensmuster, IP-Angaben, optional aus einer Listendatei) und was fuer
/// sie gilt: ein Profil derselben Datei, eine eigene Konfigurationsdatei, das Standardprofil oder gar kein Panel.
/// </summary>
public sealed class HostGroup
{
    public string Name { get; set; } = "";

    /// <summary>Rechnernamen mit * und ?, IP-Adressen, Netze oder Bereiche - wie frueher in hosts.include/exclude.</summary>
    public List<string> Match { get; set; } = new();

    /// <summary>Weitere Eintraege aus einer Textdatei (Pfad, UNC, URL; relativ zur Konfiguration), bei jedem Lauf frisch gelesen.</summary>
    public string? MatchFile { get; set; }

    /// <summary>Konfigurationsdatei fuer diese Gruppe (Pfad, UNC, URL; relativ zur Hauptdatei).</summary>
    public string? Config { get; set; }

    /// <summary>Profil aus "profiles" derselben Datei fuer diese Gruppe. Ohne Profil und ohne Datei gilt das Standardprofil.</summary>
    public string? Profile { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public HostGroupMode Mode { get; set; }

    /// <summary>Aus MatchFile gelesene Eintraege - nur zur Laufzeit.</summary>
    [JsonIgnore]
    public List<string> MatchFromFile { get; set; } = new();

    [JsonIgnore]
    public bool UsesOwnConfig => Mode != HostGroupMode.Exclude && !string.IsNullOrWhiteSpace(Config);

    [JsonIgnore]
    public bool UsesProfile => Mode != HostGroupMode.Exclude && string.IsNullOrWhiteSpace(Config) && !string.IsNullOrWhiteSpace(Profile);

    /// <summary>Kein Panel nur noch ausdruecklich ("mode": "Exclude"). Eine Gruppe ohne Profil und ohne Datei bekommt das Standardprofil.</summary>
    [JsonIgnore]
    public bool Excludes => Mode == HostGroupMode.Exclude;
}

public sealed class HostsConfig
{
    /// <summary>
    /// Gruppen von Rechnern, von oben nach unten geprueft - die erste, auf die der Rechner passt,
    /// entscheidet: eigene Konfigurationsdatei, diese Konfiguration oder kein Panel. Rechner ohne
    /// Gruppe bekommen diese Konfiguration.
    /// </summary>
    public List<HostGroup> Groups { get; set; } = new();

    // Frueheres Format (bis 2.14): getrennte White- und Blacklist. Wird weiter gelesen und beim Laden
    // in Gruppen umgewandelt (MigrateLegacyLists); geschrieben werden nur noch Gruppen.

    /// <summary>Frueher: Whitelist. Siehe <see cref="MigrateLegacyLists"/>.</summary>
    public List<string>? Include { get; set; }

    /// <summary>Frueher: Blacklist. Siehe <see cref="MigrateLegacyLists"/>.</summary>
    public List<string>? Exclude { get; set; }

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

    public const string BlacklistName = "Blacklist";
    public const string WhitelistName = "Whitelist";
    public const string OthersName = "Alle anderen";

    /// <summary>
    /// Wandelt das fruehere Format (include/exclude samt Listendateien) in Gruppen um und leert die
    /// alten Felder: die Blacklist wird eine Gruppe ohne Konfiguration ganz oben (sie hatte Vorrang),
    /// die Whitelist eine Gruppe "diese Konfiguration", gefolgt von "Alle anderen" ohne Panel - denn
    /// mit einer Whitelist bekam nur sie das Panel. Mehrfach aufrufen schadet nicht.
    /// </summary>
    public void MigrateLegacyLists()
    {
        Groups ??= new();
        var hasExclude = Exclude is { Count: > 0 } || !string.IsNullOrWhiteSpace(ExcludeFile);
        var hasInclude = Include is { Count: > 0 } || !string.IsNullOrWhiteSpace(IncludeFile);
        var front = new List<HostGroup>();
        if (hasExclude)
        {
            front.Add(new HostGroup { Name = BlacklistName, Match = Normalize(Exclude ?? new()), MatchFile = ExcludeFile, Mode = HostGroupMode.Exclude });
        }
        if (hasInclude)
        {
            front.Add(new HostGroup { Name = WhitelistName, Match = Normalize(Include ?? new()), MatchFile = IncludeFile, Mode = HostGroupMode.ThisConfig });
        }
        Groups.InsertRange(0, front);
        if (hasInclude)
        {
            Groups.Add(new HostGroup { Name = OthersName, Match = new() { "*" }, Mode = HostGroupMode.Exclude });
        }
        Include = Exclude = null;
        IncludeFile = ExcludeFile = null;
    }

    /// <summary>
    /// Dateien vor Format 2 (bis Version 2.18): dort hiess eine Gruppe ohne Datei "kein Panel". Seit
    /// 2.19 bekommt sie das Standardprofil - deshalb solche Gruppen beim Laden ausdruecklich auf
    /// "Exclude" stellen, damit sich am Verhalten nichts aendert.
    /// </summary>
    public void UpgradeFromFormat1()
    {
        foreach (var group in Groups ?? new())
        {
            if (group.Mode == HostGroupMode.Auto && string.IsNullOrWhiteSpace(group.Config) && string.IsNullOrWhiteSpace(group.Profile))
            {
                group.Mode = HostGroupMode.Exclude;
            }
        }
    }

    /// <summary>Die erste Gruppe, auf die dieser Rechner passt, mit dem Eintrag, der gepasst hat - oder null.</summary>
    public HostGroup? FindGroup(IReadOnlyCollection<string> hostNames, out string matchedBy)
    {
        var addresses = LocalAddresses();
        foreach (var group in Groups ?? new())
        {
            var hit = group.Match.Concat(group.MatchFromFile).FirstOrDefault(pattern => MatchesMachine(pattern, hostNames, addresses));
            if (hit != null)
            {
                matchedBy = hit;
                return group;
            }
        }
        matchedBy = "";
        return null;
    }

    /// <summary>Darf das Panel auf diesem Rechner erscheinen? Wenn nicht, nennt <paramref name="reason"/> den Grund.</summary>
    public bool Allows(IReadOnlyCollection<string> hostNames, out string reason)
    {
        var group = FindGroup(hostNames, out var matchedBy);
        if (group is { Excludes: true })
        {
            reason = $"steht in der Gruppe „{group.Name}“ ohne Panel (passt auf \"{matchedBy}\")";
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

    /// <summary>Balken ("bar" einer Zeile): der belegte Teil.</summary>
    public string BarUsed { get; set; } = "#FFD700";

    /// <summary>Balken: der freie Teil (und damit die Flaeche des ganzen Balkens).</summary>
    public string BarFree { get; set; } = "#FFFFFF30";

    /// <summary>Trennstrich ("separator" einer Zeile).</summary>
    public string Separator { get; set; } = "#FFFFFF60";
}

/// <summary>Wie das Bild fuer einen Monitor gefunden wurde (siehe <see cref="AppConfig.ResolveWallpaperFor"/>).</summary>
public enum WallpaperChoice
{
    None,
    Override,
    ExactVariant,
    Base,

    /// <summary>Kein Bild ohne Aufloesung im Namen: die Fassung mit 1920x1080 im Namen gilt als Standardbild.</summary>
    DefaultVariant,
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

    /// <summary>Wie ein Leerraum, mit einer waagrechten Linie in der Mitte (Farbe colors.separator, Staerke panel.separatorWidth).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Separator { get; set; }

    /// <summary>Leerraum oder Trennstrich - eine Zeile ohne Text.</summary>
    [JsonIgnore]
    public bool IsGap => Spacer || Separator;

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

    /// <summary>
    /// Balken fuer diese Zeile: ein Prozentwert von 0 bis 100, mit Platzhaltern - "{percent}" bei
    /// Laufwerken, "{rampercent}", "{battery}". Der belegte Teil erscheint in colors.barUsed, der Rest
    /// in colors.barFree. Mit Text im Wert steht der Balken darunter, ohne Text steht nur der Balken.
    /// </summary>
    public string? Bar { get; set; }

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
    /// GitHub-Repository fuer --update aus Releases - als Adresse (https://github.com/owner/name) oder
    /// kurz "owner/name". Vorgabe ist das Repository dieses Programms. Wird nur auf Aufruf benutzt
    /// (--update, Knopf im Editor), nie automatisch (GitHub begrenzt anonyme Abfragen je
    /// Internet-Anschluss). Leer ("") = kein Update aus GitHub.
    /// </summary>
    public string? Repo { get; set; } = DefaultRepo;

    public const string DefaultRepo = "https://github.com/Kilowatt-W/bginfo-reloaded";
}

/// <summary>
/// Eigene Felder: Name -> Definition, dazu je Feld eine optionale Bezeichnung (<see cref="Labels"/>).
/// In der JSON steht ein Feld kurz als "name": "{definition}" oder - mit Bezeichnung - als
/// "name": { "value": "{definition}", "label": "Text" }. Beide Formen werden gelesen.
/// </summary>
[JsonConverter(typeof(FieldMapConverter))]
public sealed class FieldMap : Dictionary<string, string>
{
    public FieldMap() : base(StringComparer.Ordinal)
    {
    }

    /// <summary>Bezeichnung je Feld (Name ohne Klammern) - erscheint als "Bedeutung" und als Bezeichnung einer neuen Zeile.</summary>
    public Dictionary<string, string> Labels { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? LabelOf(string name) =>
        Labels.TryGetValue(name.Trim().Trim('{', '}'), out var label) && !string.IsNullOrWhiteSpace(label) ? label : null;
}

public sealed class FieldMapConverter : JsonConverter<FieldMap>
{
    public override FieldMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var map = new FieldMap();
        if (reader.TokenType == JsonTokenType.Null)
        {
            return map;
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("\"fields\" muss ein Objekt sein: { \"name\": \"{definition}\" }.");
        }
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString() ?? "";
            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                map[name] = reader.GetString() ?? "";
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                string value = "", label = "";
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = reader.GetString() ?? "";
                    reader.Read();
                    var text = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "";
                    if (key.Equals("value", StringComparison.OrdinalIgnoreCase))
                    {
                        value = text;
                    }
                    else if (key.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        label = text;
                    }
                }
                map[name] = value;
                if (label.Length > 0)
                {
                    map.Labels[name] = label;
                }
            }
            else
            {
                reader.Skip();
            }
        }
        return map;
    }

    public override void Write(Utf8JsonWriter writer, FieldMap value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (name, definition) in value)
        {
            if (value.LabelOf(name) is { } label)
            {
                writer.WriteStartObject(name);
                writer.WriteString("value", definition);
                writer.WriteString("label", label);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteString(name, definition);
            }
        }
        writer.WriteEndObject();
    }
}

/// <summary>Was aus einer per "include" eingebundenen Datei uebernommen wird - bewusst nur Felder und Zeilen.</summary>
public sealed class IncludeFile
{
    public FieldMap? Fields { get; set; }
    public List<ItemConfig>? Items { get; set; }
}

public sealed class AppConfig
{
    public const string FileName = "BGInfo-Reloaded.json";
    public const string EnvironmentVariable = "BGINFORELOADED_CONFIG";

    /// <summary>Format der Datei: 2 = seit Version 2.19 (Profile; Gruppe ohne Angabe = Standardprofil).</summary>
    public const int CurrentConfigFormat = 2;

    /// <summary>Wird immer geschrieben - fehlt es beim Lesen, stammt die Datei aus einer aelteren Version.</summary>
    [JsonPropertyOrder(-1)]
    public int ConfigFormat { get; set; } = CurrentConfigFormat;

    /// <summary>
    /// Profile derselben Datei: je Name nur die Abweichungen vom Standardprofil (dieser Datei ohne
    /// "profiles"). Einstellungsbloecke werden Wert fuer Wert ueberlagert, Listen ersetzt; "hosts"
    /// und "profiles" gelten nur fuer die ganze Datei. Welches Profil ein Rechner bekommt, sagen die
    /// Gruppen unter "hosts" ("profile").
    /// </summary>
    [JsonPropertyOrder(1)]
    public Dictionary<string, JsonObject>? Profiles { get; set; }

    /// <summary>Name des Profils, aus dem diese Konfiguration zusammengesetzt wurde - null = Standardprofil. Nur zur Laufzeit.</summary>
    [JsonIgnore]
    public string? ActiveProfile { get; set; }

    /// <summary>Bloecke, die nur fuer die ganze Datei gelten und in einem Profil nicht vorkommen.</summary>
    private static readonly string[] FileOnlyKeys = { "configFormat", "profiles", "hosts" };

    /// <summary>Diese Konfiguration als JSON-Baum, wie sie gespeichert wuerde (ohne Kommentare).</summary>
    public JsonObject ToNode() => JsonSerializer.SerializeToNode(this, JsonOptions)!.AsObject();

    /// <summary>
    /// Die Konfiguration fuer ein Profil: diese (das Standardprofil) mit den Abweichungen des Profils
    /// ueberlagert. "hosts" bleibt die der Datei; das Ergebnis hat selbst keine Profile. Null, wenn es
    /// das Profil nicht gibt.
    /// </summary>
    public AppConfig? WithProfile(string name)
    {
        var key = FindProfile(name);
        if (key == null)
        {
            return null;
        }
        var node = ToNode();
        foreach (var fileOnly in FileOnlyKeys)
        {
            node.Remove(fileOnly);
        }
        MergeInto(node, Profiles![key]);
        var result = node.Deserialize<AppConfig>(JsonOptions)!;
        result.BaseDirectory = BaseDirectory;
        result.Hosts = Hosts;
        result.Profiles = null;
        result.ActiveProfile = key;
        result.Fields ??= new();
        result.Items ??= new();
        result.Format ??= new();
        result.Border ??= new();
        return result;
    }

    /// <summary>Der Name, unter dem das Profil gespeichert ist (Gross-/Kleinschreibung egal) - oder null.</summary>
    public string? FindProfile(string? name) =>
        name == null ? null : Profiles?.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Haelt <paramref name="effective"/> als Profil fest - gespeichert werden nur die Abweichungen von dieser Konfiguration.</summary>
    public void StoreProfile(string name, AppConfig effective)
    {
        Profiles ??= new();
        Profiles[FindProfile(name) ?? name] = Diff(effective.ToNode(), ToNode());
    }

    /// <summary>Zeilen des Panels als JSON - fuer die Zwischenablage des Editors.</summary>
    public static string ItemsToJson(IEnumerable<ItemConfig> items) => JsonSerializer.Serialize(items, JsonOptions);

    /// <summary>Gegenstueck zu <see cref="ItemsToJson"/>; null, wenn der Text keine Zeilen enthaelt.</summary>
    public static List<ItemConfig>? ItemsFromJson(string json)
    {
        try
        {
            var text = json.Trim();
            return text.StartsWith('{')
                ? new List<ItemConfig> { JsonSerializer.Deserialize<ItemConfig>(text, JsonOptions)! }
                : text.StartsWith('[') ? JsonSerializer.Deserialize<List<ItemConfig>>(text, JsonOptions) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ueberlagert <paramref name="target"/> mit <paramref name="overlay"/>: Bloecke der obersten Ebene
    /// ("panel", "fields" ...) Wert fuer Wert, deren Werte und alles andere (auch Listen) als Ganzes.
    /// </summary>
    public static void MergeInto(JsonObject target, JsonObject overlay, bool topLevel = true, bool keepExisting = false)
    {
        foreach (var (key, value) in overlay)
        {
            if (topLevel && FileOnlyKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            if (keepExisting && value == null)
            {
                continue; // Felder des Standardprofils lassen sich im Profil nicht entfernen (Dateien von 2.19.0 enthalten solche null)
            }
            var existingKey = target.Select(pair => pair.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) ?? key;
            if (topLevel && value is JsonObject inner && target[existingKey] is JsonObject current)
            {
                MergeInto(current, inner, topLevel: false, keepExisting: IsInheritedBlock(key));
            }
            else
            {
                target[existingKey] = value?.DeepClone();
            }
        }
    }

    /// <summary>
    /// Was <paramref name="effective"/> von <paramref name="baseline"/> unterscheidet - das, was in einem
    /// Profil stehen muss. Bloecke der obersten Ebene werden Wert fuer Wert verglichen (so, wie
    /// <see cref="MergeInto"/> sie ueberlagert), alles andere als Ganzes; ein Wert, der im Standard
    /// steht, im Profil aber fehlt, wird als null festgehalten - ausser bei den eigenen Feldern
    /// (<see cref="IsInheritedBlock"/>), die gelten in jedem Profil.
    /// </summary>
    public static JsonObject Diff(JsonObject effective, JsonObject baseline, bool topLevel = true, bool keepExisting = false)
    {
        var result = new JsonObject();
        foreach (var (key, value) in effective)
        {
            if (topLevel && FileOnlyKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            var other = baseline[key];
            if (topLevel && value is JsonObject inner && other is JsonObject innerBase)
            {
                var nested = Diff(inner, innerBase, topLevel: false, keepExisting: IsInheritedBlock(key));
                if (nested.Count > 0)
                {
                    result[key] = nested;
                }
            }
            else if (!JsonNode.DeepEquals(value, other))
            {
                result[key] = value?.DeepClone();
            }
        }
        foreach (var (key, _) in baseline)
        {
            if (!keepExisting && !(topLevel && FileOnlyKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) && !effective.ContainsKey(key))
            {
                result[key] = null;
            }
        }
        return result;
    }

    /// <summary>
    /// Bloecke, deren Eintraege des Standardprofils in jedem Profil gelten: ein Profil kann ihren Wert
    /// aendern und eigene hinzufuegen, aber keinen entfernen - die eigenen Felder.
    /// </summary>
    private static bool IsInheritedBlock(string key) => key.Equals("fields", StringComparison.OrdinalIgnoreCase);

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
    public FieldMap Fields { get; set; } = new();

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
        // Standardbild: die Datei ohne Aufloesung im Namen, sonst die Fassung mit 1920x1080 im Namen.
        var standard = WallpaperState.FindDefaultImage(baseImage);
        var standardChoice = string.Equals(standard, baseImage, StringComparison.OrdinalIgnoreCase) ? WallpaperChoice.Base : WallpaperChoice.DefaultVariant;
        if (!Wallpaper.ResolutionVariants)
        {
            return standard != null ? (standard, standardChoice) : (null, WallpaperChoice.None);
        }
        var image = WallpaperState.ResolveResolutionVariant(baseImage, width, height);
        if (!File.Exists(image))
        {
            return (null, WallpaperChoice.None);
        }
        if (Path.GetFileNameWithoutExtension(image).EndsWith(key, StringComparison.OrdinalIgnoreCase))
        {
            return (image, WallpaperChoice.ExactVariant);
        }
        if (string.Equals(image, standard, StringComparison.OrdinalIgnoreCase))
        {
            return (image, standardChoice);
        }
        return (image, WallpaperChoice.BestFit);
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
        // Erst die Listendateien: sie entscheiden mit, in welche Gruppe der Rechner faellt.
        foreach (var hostNote in config.ResolveHostFiles())
        {
            Console.WriteLine(hostNote);
        }
        if (config.ResolveGroupConfig() is { } groupConfig)
        {
            (config, source) = groupConfig;
        }
        else if (config.ResolveGroupProfile() is { } profiled)
        {
            config = profiled;
            source = $"{source} (Profil {profiled.ActiveProfile})";
        }
        if (config.ResolveInclude() is { } note)
        {
            Console.WriteLine(note);
        }
        return (config, source);
    }

    /// <summary>
    /// Faellt dieser Rechner in eine Gruppe mit Profil, die Konfiguration fuer dieses Profil; null,
    /// wenn das Standardprofil gilt. Ein unbekannter Profilname ergibt eine Warnung und das Standardprofil.
    /// </summary>
    public AppConfig? ResolveGroupProfile()
    {
        var hostNames = new[] { Environment.MachineName, new ValueResolver().Expand("{fqdn}") };
        var group = Hosts.FindGroup(hostNames, out var matchedBy);
        if (group is not { UsesProfile: true })
        {
            return null;
        }
        if (WithProfile(group.Profile!) is { } profiled)
        {
            Console.WriteLine($"Gruppe „{group.Name}“ (passt auf \"{matchedBy}\"): Profil {profiled.ActiveProfile}");
            return profiled;
        }
        Console.WriteLine($"Warnung: Gruppe „{group.Name}“ verweist auf das Profil '{group.Profile}', das es in dieser Datei nicht gibt - verwende das Standardprofil.");
        return null;
    }

    /// <summary>
    /// Faellt dieser Rechner in eine Gruppe mit eigener Konfigurationsdatei, wird diese geladen und
    /// gilt vollstaendig. Nicht erreichbar: zuletzt gelesene Fassung aus dem Cache, sonst bleibt es
    /// (mit Warnung) bei dieser Konfiguration. Gruppen in der Gruppendatei werden nicht ausgewertet -
    /// keine Verschachtelung, keine Schleifen. Null, wenn keine Gruppendatei gilt.
    /// </summary>
    public (AppConfig Config, string Source)? ResolveGroupConfig()
    {
        var hostNames = new[] { Environment.MachineName, new ValueResolver().Expand("{fqdn}") };
        var group = Hosts.FindGroup(hostNames, out var matchedBy);
        if (group is not { UsesOwnConfig: true })
        {
            return null;
        }

        var source = Environment.ExpandEnvironmentVariables(group.Config!.Trim());
        if (!IsUrl(source) && !Path.IsPathRooted(source))
        {
            source = Path.GetFullPath(Path.Combine(BaseDirectory, source));
        }
        var sourceId = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(source.ToLowerInvariant())))[..8];
        var cacheFile = Path.Combine(WallpaperState.StateDirectory, $"group.{sourceId}.cache.json");

        string json;
        var shownSource = source;
        try
        {
            json = ReadSource(source);
            TryWriteCache(cacheFile, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            if (!File.Exists(cacheFile))
            {
                Console.WriteLine($"Warnung: Konfiguration der Gruppe „{group.Name}“ '{source}' nicht erreichbar ({ex.Message}) und kein Cache vorhanden - verwende die Hauptkonfiguration.");
                return null;
            }
            Console.WriteLine($"Warnung: Konfiguration der Gruppe „{group.Name}“ '{source}' nicht erreichbar ({ex.Message}) - verwende lokalen Cache.");
            json = File.ReadAllText(cacheFile);
            shownSource = $"{cacheFile} (Cache von {source})";
        }

        var config = Parse(json, source);
        // Die Zuordnung hat die Hauptdatei getroffen; Listen und Gruppen der Gruppendatei gelten nicht.
        config.Hosts = new HostsConfig { OnExcluded = config.Hosts.OnExcluded };
        Console.WriteLine($"Gruppe „{group.Name}“ (passt auf \"{matchedBy}\"): verwende {source}");
        return (config, shownSource);
    }

    /// <summary>
    /// Liest die Listendateien aus hosts.includeFile / hosts.excludeFile in die Laufzeitlisten
    /// der Gruppen (MatchFromFile). Ist eine Datei nicht erreichbar, gilt die zuletzt gelesene
    /// Fassung aus dem lokalen Cache; gibt es keinen, fehlen ihre Eintraege (mit Warnung). Liefert
    /// Hinweistexte fuer das Log. Der Editor ruft das nur fuer die Vorschau-Kopie auf.
    /// </summary>
    public List<string> ResolveHostFiles()
    {
        var notes = new List<string>();
        Hosts.MigrateLegacyLists();
        foreach (var group in Hosts.Groups)
        {
            group.MatchFromFile = ReadHostFile(group.MatchFile, $"Gruppe „{group.Name}“: Listen", "hosts.group", notes);
        }
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
                if (central!.Fields!.LabelOf(name) is { } label)
                {
                    Fields.Labels[name] = label;
                }
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
            config.Hosts ??= new();
            if (!HasTopLevelKey(json, "configFormat"))
            {
                // Datei aus einer Version vor den Profilen: Gruppen ohne Datei hiessen "kein Panel".
                config.Hosts.UpgradeFromFormat1();
            }
            config.ConfigFormat = CurrentConfigFormat;
            config.Hosts.MigrateLegacyLists();
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Konfiguration '{source}' ist kein gueltiges JSON: {ex.Message}");
        }
    }

    private static bool HasTopLevelKey(string json, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Any(property => string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
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
