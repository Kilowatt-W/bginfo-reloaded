using System.Collections;
using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>
/// Ausgabe von --list-tags: alle Platzhalter, die in BGInfo-Reloaded.json verwendet werden koennen, jeweils
/// mit dem Wert, den sie auf diesem Rechner gerade liefern.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TagList
{
    private const int MaxValueLength = 90;

    public static void Print(string? filter, string? configSource = null)
    {
        if (filter == null)
        {
            PrintOverview(configSource);
        }
        else if (filter.StartsWith("wmi:", StringComparison.OrdinalIgnoreCase))
        {
            var className = filter[4..].Trim();
            var rows = ValueResolver.ListWmiClass(className);
            if (rows.Count == 0)
            {
                throw new InvalidOperationException($"WMI-Klasse '{className}' liefert keine Instanz (Name pruefen, z.B. Win32_BIOS).");
            }
            PrintSection($"WMI-KLASSE {className} (erste Instanz)", rows.Select(r => (r.Tag, "", r.Value)));
        }
        else if (filter.StartsWith("reg:", StringComparison.OrdinalIgnoreCase))
        {
            var keyPath = filter[4..].Trim();
            PrintSection($"REGISTRY-SCHLUESSEL {keyPath}", ValueResolver.ListRegistryKey(keyPath).Select(r => (r.Tag, "", r.Value)));
        }
        else
        {
            throw new ArgumentException("--list-tags kennt nur: (ohne Angabe), wmi:<Klasse> oder reg:<Schluessel>");
        }
    }

    private static void PrintOverview(string? configSource)
    {
        // Mit der Konfiguration, die auch ein normaler Lauf benutzen wuerde - samt zentraler Datei.
        var (config, loadedFrom) = AppConfig.Load(configSource);
        var resolver = new ValueResolver(config);

        if (config.Fields.Count > 0)
        {
            PrintSection($"EIGENE FELDER ({loadedFrom})",
                config.Fields.Select(f => ($"{{{f.Key}}}", f.Value, resolver.Expand($"{{{f.Key}}}"))));
        }

        PrintSection("EINGEBAUTE PLATZHALTER",
            ValueResolver.BuiltIns.Select(b => ($"{{{b.Name}}}", b.Description, resolver.Expand($"{{{b.Name}}}"))));

        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).ToList();
        var driveRows = new List<(string, string, string)>();
        foreach (var (tag, description) in ValueResolver.DriveTags)
        {
            var values = drives.Select(d => SystemInfoCollector.FormatDrive(d, tag));
            driveRows.Add((tag, description, string.Join(" | ", values)));
        }
        PrintSection("LAUFWERKE (nur in Zeilen mit \"drives\": true, ein Wert je Festplatte)", driveRows);

        // Zu einigen generischen Quellen ein ausgerechnetes Beispiel; die Liste selbst steht im ValueResolver.
        var examples = new Dictionary<string, string>
        {
            ["{filever:Pfad}"] = @"{filever:%WINDIR%\explorer.exe}",
            ["{name,20}  {name,20...}  {name,-8}"] = "{os,20...}",
            ["{now:Format}"] = "{now:dd.MM.yyyy HH:mm}",
            ["{uptime:Format}"] = "{uptime:d' Tage 'h' Std.'}"
        };
        PrintSection("GENERISCHE QUELLEN (Beispiele)", ValueResolver.GenericTags.Select(t =>
            (t.Tag, t.Description, examples.TryGetValue(t.Tag, out var example) ? $"{resolver.Expand(example)}  ({example})" : "")));

        PrintSection("UMGEBUNGSVARIABLEN",
            Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                .OrderBy(e => (string)e.Key, StringComparer.OrdinalIgnoreCase)
                .Select(e => ($"{{env:{e.Key}}}", "", e.Value?.ToString() ?? "")));

        Console.WriteLine();
        Console.WriteLine("Weitere Werte nachschlagen:");
        Console.WriteLine("  BGInfo-Reloaded.exe --list-tags wmi:Win32_ComputerSystem");
        Console.WriteLine(@"  BGInfo-Reloaded.exe --list-tags ""reg:HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion""");
    }

    private static void PrintSection(string title, IEnumerable<(string Tag, string Description, string Value)> rows)
    {
        var list = rows.ToList();
        Console.WriteLine();
        Console.WriteLine(title);

        var tagWidth = Math.Min(list.Select(r => r.Tag.Length).DefaultIfEmpty(0).Max(), 60);
        var descriptionWidth = list.Select(r => r.Description.Length).DefaultIfEmpty(0).Max();
        foreach (var (tag, description, value) in list)
        {
            var text = value.ReplaceLineEndings(" ");
            if (text.Length > MaxValueLength)
            {
                text = text[..MaxValueLength] + "...";
            }
            var descriptionColumn = descriptionWidth > 0 ? description.PadRight(descriptionWidth) + "  " : "";
            Console.WriteLine($"  {tag.PadRight(tagWidth)}  {descriptionColumn}{text}".TrimEnd());
        }
    }
}
