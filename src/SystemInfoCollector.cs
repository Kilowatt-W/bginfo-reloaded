using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>
/// Art einer Zeile im Info-Panel: Section = Zwischenüberschrift, Value = "Label: Wert"-Zeile.
/// </summary>
public enum LineKind
{
    Section,
    Value
}

/// <param name="Bar">Balken unter bzw. statt des Werts: Anteil 0 bis 1 (belegt), null = kein Balken.</param>
public sealed record InfoLine(LineKind Kind, string Label, string Value = "", double? Bar = null, bool Separator = false);

/// <summary>
/// Baut aus den "items" der Konfiguration die fertigen Panel-Zeilen. Die eigentlichen Werte kommen
/// aus dem <see cref="ValueResolver"/> - bewusst robust: jede Einzelabfrage ist gegen Fehler
/// abgesichert, damit ein einzelner WMI-Ausfall (z.B. in einer Sandbox/VM ohne bestimmte Provider)
/// nicht das ganze Programm zum Absturz bringt. Im Zweifel steht dann eben "n/a" auf dem Wallpaper
/// statt eines Stacktrace.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemInfoCollector
{
    public const string DefaultDriveLabel = "Laufwerk {drive}";
    public const string DefaultDriveValue = "{used} GB / {total} GB belegt ({free} GB frei)";

    public static List<InfoLine> Collect(AppConfig config)
    {
        var resolver = new ValueResolver(config);
        var hostNames = new[] { Environment.MachineName, resolver.Expand("{fqdn}") };
        var lines = new List<InfoLine>();

        // Die Zeilen der zentralen Datei an die Stelle ihres Platzhalter-Eintrags setzen; ein Filter
        // auf diesem Eintrag gilt fuer den ganzen Block.
        var items = config.Items.SelectMany(item =>
            !item.IncludeItems ? new[] { item }
            : Applies(item, resolver, hostNames) ? config.IncludedItems : Enumerable.Empty<ItemConfig>());

        // Offene Bloecke: je Ebene, ob ihr Inhalt erscheint. Ein Block in einem ausgeblendeten Block
        // bleibt ausgeblendet, egal was sein eigener Filter sagt.
        var blocks = new Stack<bool>();

        foreach (var item in items)
        {
            if (item.EndBlock)
            {
                blocks.TryPop(out _);
                continue;
            }
            var visible = blocks.Count == 0 || blocks.Peek();
            if (item.Block != null)
            {
                blocks.Push(visible && Applies(item, resolver, hostNames));
                continue;
            }
            if (!visible || !Applies(item, resolver, hostNames))
            {
                continue;
            }

            if (item.IsGap)
            {
                // Ein Trennstrich ist ein Leerraum mit Linie - fuer Hoehe und Rahmen gilt dasselbe.
                lines.Add(new InfoLine(LineKind.Section, "", Separator: item.Separator));
            }
            else if (item.Section != null)
            {
                lines.Add(new InfoLine(LineKind.Section, resolver.Expand(item.Section)));
            }
            else if (item.Drives)
            {
                lines.AddRange(SafeGet(() => GetFixedDrives(item, resolver), new List<InfoLine>()));
            }
            else
            {
                var value = resolver.Expand(item.Value);
                if (item.HideIfEmpty && (string.IsNullOrWhiteSpace(value) || value == ValueResolver.NotAvailable))
                {
                    continue;
                }
                AddSplit(lines, resolver.Expand(item.Label), value, item.Split, ParseBar(resolver.Expand(item.Bar)));
            }
        }

        return lines;
    }

    /// <summary>
    /// Teilt einen Wert an den Trennzeichen aus "split" (mehrere mit | getrennt); Leerraum um die
    /// Teile und leere Teile entfallen. Ohne Angabe oder ohne Treffer: der Wert unveraendert.
    /// </summary>
    public static IReadOnlyList<string> SplitValue(string value, string? split)
    {
        if (string.IsNullOrEmpty(split) || string.IsNullOrEmpty(value))
        {
            return new[] { value };
        }
        var separators = split.Split('|').Where(separator => separator.Length > 0).ToArray();
        if (separators.Length == 0)
        {
            return new[] { value };
        }
        var parts = value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts : new[] { value };
    }

    /// <summary>Eine Zeile je Teil: der erste mit der Bezeichnung, die weiteren darunter ohne. Ein Balken haengt an der letzten.</summary>
    private static void AddSplit(List<InfoLine> lines, string label, string value, string? split, double? bar = null)
    {
        var parts = SplitValue(value, split);
        for (var i = 0; i < parts.Count; i++)
        {
            lines.Add(new InfoLine(LineKind.Value, i == 0 ? label : "", parts[i], i == parts.Count - 1 ? bar : null));
        }
    }

    /// <summary>
    /// Liest aus einem aufgeloesten Text den Prozentwert fuer einen Balken ("45", "45 %", "45,3 %") und
    /// liefert den Anteil 0 bis 1. Null, wenn kein Balken gewuenscht ist oder keine Zahl darin steht
    /// (z.B. "n/a" auf einem Rechner ohne Akku) - dann erscheint die Zeile ohne Balken.
    /// </summary>
    public static double? ParseBar(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var match = System.Text.RegularExpressions.Regex.Match(text, @"-?\d+(?:[.,]\d+)?");
        if (!match.Success || !double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var percent))
        {
            return null;
        }
        return Math.Clamp(percent, 0, 100) / 100.0;
    }

    /// <summary>Treffen alle Filter des Eintrags (onlyOn, notOn, when) auf diesen Rechner zu?</summary>
    public static bool Applies(ItemConfig item, ValueResolver resolver, IReadOnlyCollection<string> hostNames)
    {
        if (item.OnlyOn is { Count: > 0 } only && !only.Any(pattern => HostsConfig.MatchesMachine(pattern, hostNames)))
        {
            return false;
        }
        if (item.NotOn is { Count: > 0 } not && not.Any(pattern => HostsConfig.MatchesMachine(pattern, hostNames)))
        {
            return false;
        }
        return string.IsNullOrWhiteSpace(item.When) || EvaluateCondition(item.When, resolver);
    }

    /// <summary>
    /// "links = rechts" bzw. "links != rechts" (rechts mit * und ?, Gross-/Kleinschreibung egal);
    /// ohne Vergleich: erfuellt, wenn der Text einen Wert liefert. Der Vergleichsoperator wird nur
    /// ausserhalb von {...} gesucht - in WMI-Bedingungen steht selbst ein "=".
    /// </summary>
    public static bool EvaluateCondition(string condition, ValueResolver resolver)
    {
        var depth = 0;
        for (var i = 0; i < condition.Length; i++)
        {
            var c = condition[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }
            else if (depth == 0 && c == '=')
            {
                var negate = i > 0 && condition[i - 1] == '!';
                var left = resolver.Expand(condition[..(negate ? i - 1 : i)]).Trim();
                var right = resolver.Expand(condition[(i + 1)..].TrimStart('=')).Trim();
                return (right.Length == 0 ? left.Length == 0 : HostsConfig.Matches(right, left)) != negate;
            }
        }

        var value = resolver.Expand(condition).Trim();
        return value.Length > 0 && value != ValueResolver.NotAvailable;
    }

    private static List<InfoLine> GetFixedDrives(ItemConfig item, ValueResolver resolver)
    {
        var result = new List<InfoLine>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            AddSplit(result,
                resolver.Expand(FormatDrive(drive, item.Label ?? DefaultDriveLabel)),
                resolver.Expand(FormatDrive(drive, item.Value ?? DefaultDriveValue)),
                item.Split,
                string.IsNullOrWhiteSpace(item.Bar) ? null : ParseBar(resolver.Expand(FormatDrive(drive, item.Bar))));
        }
        return result;
    }

    /// <summary>Ersetzt die laufwerksbezogenen Platzhalter ({drive}, {used}, ...) in einem Text.</summary>
    public static string FormatDrive(DriveInfo drive, string template)
    {
        var usedGb = (drive.TotalSize - drive.AvailableFreeSpace) / 1024.0 / 1024.0 / 1024.0;
        var totalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
        var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

        return template
            .Replace("{drive}", drive.Name.TrimEnd('\\'))
            .Replace("{volume}", drive.VolumeLabel)
            .Replace("{used}", ValueFormat.FormatNumber(usedGb))
            .Replace("{total}", ValueFormat.FormatNumber(totalGb))
            .Replace("{free}", ValueFormat.FormatNumber(freeGb))
            .Replace("{percent}", $"{(totalGb > 0 ? usedGb / totalGb * 100 : 0):0}");
    }

    private static T SafeGet<T>(Func<T> getter, T fallback)
    {
        try
        {
            return getter();
        }
        catch
        {
            return fallback;
        }
    }
}
