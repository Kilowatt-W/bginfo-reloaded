using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BgInfoReloaded;

/// <summary>
/// Schreibt die Konfiguration als kommentiertes JSON: fester Kopf, eine Erlaeuterung hinter jeder
/// Einstellung, jede Panel-Zeile auf einer eigenen Textzeile. Die Serialisierung von System.Text.Json
/// kann keine Kommentare erhalten - deshalb werden die Standardkommentare bei jedem Speichern neu
/// erzeugt. Eigene Kommentare gehen dabei verloren; fuer eigene Anmerkungen gibt es "description".
/// </summary>
internal static class ConfigWriter
{
    private static readonly string[] Header =
    {
        "Konfiguration für BGInfo-Reloaded. Als \"BGInfo-Reloaded.json\" neben die exe legen, nach",
        "%ProgramData%\\BGInfo-Reloaded\\ kopieren oder zentral bereitstellen und per",
        "  BGInfo-Reloaded.exe --config \\\\fileserver.example.com\\share\\BGInfo-Reloaded.json   (bzw. https://...)",
        "oder über die Umgebungsvariable BGINFORELOADED_CONFIG referenzieren.",
        "Kommentare und abschließende Kommas sind erlaubt. Alles Weggelassene behält den Standardwert.",
        "Die Kommentare schreibt der Editor bei jedem Speichern neu - eigene Anmerkungen gehören in \"description\"."
    };

    /// <summary>Erlaeuterung hinter einer Einstellung, Schluessel = Pfad ("panel.margin").</summary>
    private static readonly Dictionary<string, string> Trailing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["version"] = "frei wählbare Version dieser Datei, als {configversion} im Panel nutzbar",
        ["description"] = "eigene Anmerkungen zu dieser Datei (bleiben beim Speichern erhalten)",
        ["include"] = "zentrale Datei (Pfad, UNC, URL) mit gemeinsam genutzten \"fields\" und \"items\"",
        ["panel.position"] = "TopLeft, TopCenter, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight",
        ["panel.margin"] = "Abstand zum Rand in Pixeln",
        ["panel.padding"] = "Innenabstand des Panels",
        ["panel.width"] = "Panelbreite, 0 = automatisch an den längsten Eintrag anpassen",
        ["panel.screenFraction"] = "Panelbreite als Bruchteil der Bildschirmbreite: 3 bis 8 = 1/3 bis 1/8, Inhalt wird passend skaliert; 0 = aus",
        ["panel.labelWidth"] = "Breite der Bezeichnungsspalte, 0 = automatisch",
        ["panel.labelShare"] = "Anteil der Bezeichnungsspalte an der Panelbreite in Prozent (10 bis 90), 0 = automatisch",
        ["panel.columnGap"] = "Abstand zwischen Bezeichnung und Wert bei automatischer Spaltenbreite",
        ["panel.textOverflow"] = "zu lange Texte: Truncate = mit … abschneiden, Wrap = in der Spalte umbrechen (nicht bei screenFraction)",
        ["panel.background"] = "#RRGGBBAA - AA ist die Deckkraft",
        ["panel.cornerRadius"] = "abgerundete Ecken des Panel-Hintergrunds, 0 = eckig",
        ["panel.shadowSize"] = "Schlagschatten: so weit schaut er rechts unten hervor, 0 = ohne",
        ["panel.shadowColor"] = "Schattenfarbe (#RRGGBBAA), läuft am Rand weich aus",
        ["panel.avoidTaskbar"] = "Abstand ab dem Arbeitsbereich: Panel beginnt über/neben der Taskleiste",
        ["panel.scaleWithDpi"] = "Maße und Schrift mit der Windows-Skalierung vergrößern (nicht bei fit = None)",
        ["font.family"] = "Schriftart; fehlt sie, greift eine Ersatzschrift",
        ["font.size"] = "Schriftgröße der Zeilen in Pixeln",
        ["font.headerSize"] = "Schriftgröße der Überschriften",
        ["colors.header"] = "Überschriften",
        ["colors.label"] = "Bezeichnungen",
        ["colors.value"] = "Werte",
        ["wallpaper.path"] = "festes Hintergrundbild; relativ zum Ordner dieser Datei; weglassen = Wallpaper des Benutzers",
        ["wallpaper.originalPath"] = "nur zur Information: von hier wurde das Bild beim ersten Start übernommen",
        ["wallpaper.fit"] = "Fill, Fit, Stretch, Center, Tile oder None (Originalgröße, Windows skaliert Bild samt Panel)",
        ["wallpaper.resolutionVariants"] = "je Monitor zuerst bild_3440x1440.jpg suchen, sonst das Bild ohne Zusatz",
        ["wallpaper.backgroundColor"] = "Farbe für Ränder und falls kein Bild vorhanden ist",
        ["monitors.mode"] = "PerMonitor = je Monitor ein eigenes Bild, Single = ein Bild für alle",
        ["monitors.panel"] = "All = Panel auf jedem Monitor, Primary = nur auf dem primären",
        ["hosts.include"] = "Whitelist: wenn nicht leer, Panel nur auf passenden Rechnern - Namen mit * und ?, IP (10.1.1.5), Netz (10.1.0.0/16), Bereich (10.1.1.10-50)",
        ["hosts.exclude"] = "Blacklist: auf passenden Rechnern kein Panel - hat Vorrang; Einträge wie bei include",
        ["hosts.includeFile"] = "Textdatei (Pfad, UNC, URL) mit weiteren Whitelist-Einträgen, ein Eintrag je Zeile; wird bei jedem Lauf gelesen",
        ["hosts.excludeFile"] = "Textdatei (Pfad, UNC, URL) mit weiteren Blacklist-Einträgen, ein Eintrag je Zeile; wird bei jedem Lauf gelesen",
        ["hosts.onExcluded"] = "BackgroundOnly = Hintergrund ohne Panel setzen, Skip = Wallpaper nicht anfassen",
        ["format.date"] = "reines Datum (z. B. BIOS-Datum): .NET-Format, etwa dd.MM.yyyy",
        ["format.dateTime"] = "Datum mit Uhrzeit (letzter Boot, {now}, Dateidatum), etwa dd.MM.yyyy HH:mm",
        ["format.decimals"] = "höchstens so viele Nachkommastellen bei Größen und Geschwindigkeiten (GB, Gbit/s)",
        ["format.culture"] = "Schreibweise von Trennzeichen, Monats- und Tagesnamen: de-DE, en-US ...; system = wie Windows; weglassen = neutral (Punkt, englische Namen)",
        ["update.path"] = "Ordner (lokal oder UNC) mit der aktuellen BGInfo-Reloaded.exe für das Selbst-Update; relativ zum Ordner dieser Datei",
        ["update.auto"] = "true = bei jedem Lauf prüfen und neuere Version von selbst holen, false = nur mit --update",
        ["update.program"] = "BGInfo-Reloaded.exe aktualisieren (bei höherer Version im Ordner)",
        ["update.editor"] = "BGInfo-Reloaded-Editor.exe mit aktualisieren - nur wo der Editor schon neben der exe liegt",
        ["update.config"] = "BGInfo-Reloaded.json aus dem Ordner neben die exe holen, wenn dort neuer (vorherige bleibt als .bak)",
        ["update.backgrounds"] = "background*.* und den Unterordner backgrounds\\ aus dem Ordner holen (neuere und fehlende Dateien)",
        ["update.repo"] = "GitHub-Repository (owner/name) für --update aus Releases; nur auf Aufruf, nie automatisch"
    };

    /// <summary>Kommentarzeilen vor einem Abschnitt der obersten Ebene.</summary>
    private static readonly Dictionary<string, string[]> Leading = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fields"] = new[]
        {
            "Eigene, benannte Platzhalter: einmal definieren, in beliebig vielen Zeilen als {name} benutzen."
        },
        ["items"] = new[]
        {
            "Zeilen des Panels: \"section\" (Überschrift), \"spacer\" (Leerraum), \"drives\" (eine Zeile je Festplatte),",
            "\"label\"/\"value\" (normale Zeile, \"hideIfEmpty\" lässt sie bei leerem Wert weg), \"includeItems\" (Zeilen der zentralen Datei).",
            "Filter für jeden Eintrag: \"onlyOn\" / \"notOn\" (Rechnernamen-Muster oder IP-Angaben wie bei hosts), \"when\" (Bedingung mit Platzhaltern).",
            "\"split\" teilt den Wert an Trennzeichen in mehrere Zeilen untereinander, z. B. \", \" bei mehreren Grafikkarten (mehrere mit | getrennt).",
            "Block: { \"block\": \"Name\", \"when\": \"{virtual} = nein\" } ... { \"endBlock\": true } - der Inhalt erscheint nur, wenn der Filter zutrifft.",
            "Platzhalter mit aktuellen Werten: BGInfo-Reloaded.exe --list-tags   Kürzen: {cpu,30}  {cpu,30...}  {serial,-8}"
        }
    };

    /// <summary>
    /// Fruehere Fassungen von Standardkommentaren. Wird ein Kommentartext oben geaendert, den alten
    /// hier eintragen - sonst gilt er in bestehenden Dateien als eigener Kommentar des Benutzers.
    /// </summary>
    private static readonly string[] FormerComments =
    {
        "  BGInfo-Reloaded.exe --config \\\\server\\share\\BGInfo-Reloaded.json   (bzw. https://...)",
        "Whitelist: wenn nicht leer, Panel nur auf passenden Rechnern (* und ? erlaubt)",
        "Blacklist: auf passenden Rechnern kein Panel - hat Vorrang",
        "Filter für jeden Eintrag: \"onlyOn\" / \"notOn\" (Muster für Rechnernamen), \"when\" (Bedingung mit Platzhaltern)."
    };

    private static readonly HashSet<string> StandardComments =
        // Getrimmt, weil auch der Vergleich in HasForeignComments trimmt - eine eingerueckte Kopfzeile
        // galt sonst als eigener Kommentar, und der Editor fragte bei jeder Datei nach.
        new(Header.Concat(Trailing.Values).Concat(Leading.Values.SelectMany(lines => lines)).Concat(FormerComments).Select(line => line.Trim()), StringComparer.Ordinal);

    private const string PlaceholderHeading = "Alle Platzhalter dieser Programmversion (für \"section\", \"label\", \"value\" und \"when\"):";
    private const string DrivePlaceholderHeading = "Nur in Zeilen mit \"drives\": true:";
    private const string GenericPlaceholderHeading = "Beliebige weitere Werte:";

    /// <summary>
    /// Kommentarzeilen mit allen Platzhaltern - aus denselben Listen, aus denen auch --list-tags
    /// liest. Jede Zeile beginnt mit dem Platzhalter in geschweiften Klammern; daran erkennt
    /// <see cref="HasForeignComments"/> sie als Standardkommentar.
    /// </summary>
    private static IEnumerable<string> PlaceholderLines()
    {
        static IEnumerable<string> Table(IEnumerable<(string Tag, string Description)> rows)
        {
            var list = rows.ToList();
            var width = list.Max(r => r.Tag.Length);
            return list.Select(r => $"{r.Tag.PadRight(width)}  {r.Description}");
        }

        yield return PlaceholderHeading;
        foreach (var line in Table(ValueResolver.BuiltIns.Select(b => ($"{{{b.Name}}}", b.Description))))
        {
            yield return line;
        }
        yield return DrivePlaceholderHeading;
        foreach (var line in Table(ValueResolver.DriveTags))
        {
            yield return line;
        }
        yield return GenericPlaceholderHeading;
        foreach (var line in Table(ValueResolver.GenericTags))
        {
            yield return line;
        }
    }

    public static string Write(AppConfig config, JsonSerializerOptions options, bool withPlaceholderList = false)
    {
        var compact = new JsonSerializerOptions(options) { WriteIndented = false };
        var root = JsonSerializer.SerializeToNode(config, options)!.AsObject();
        var text = new StringBuilder();

        foreach (var line in Header)
        {
            text.Append("// ").AppendLine(line);
        }
        text.AppendLine("{");

        var properties = root.ToList();
        for (var i = 0; i < properties.Count; i++)
        {
            var (name, value) = properties[i];
            var comma = i < properties.Count - 1 ? "," : "";

            if (withPlaceholderList && name.Equals("items", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in PlaceholderLines())
                {
                    text.Append(line.StartsWith('{') ? "  //   " : "  // ").AppendLine(line);
                }
                text.AppendLine("  //");
            }

            if (Leading.TryGetValue(name, out var leading))
            {
                foreach (var line in leading)
                {
                    text.Append("  // ").AppendLine(line);
                }
            }

            if (value is JsonObject section && section.Count > 0)
            {
                text.AppendLine($"  {Quote(name)}: {{");
                var children = section.ToList();
                for (var j = 0; j < children.Count; j++)
                {
                    var (childName, childValue) = children[j];
                    AppendLine(text, "    ", $"{Quote(childName)}: {Compact(childValue, compact)}{(j < children.Count - 1 ? "," : "")}", $"{name}.{childName}");
                }
                text.AppendLine($"  }}{comma}");
            }
            else if (value is JsonArray rows && name.Equals("items", StringComparison.OrdinalIgnoreCase) && rows.Count > 0)
            {
                text.AppendLine($"  {Quote(name)}: [");
                var depth = 0;
                for (var j = 0; j < rows.Count; j++)
                {
                    // Inhalt eines Blocks einruecken.
                    if (rows[j] is JsonObject end && end.ContainsKey("endBlock") && depth > 0)
                    {
                        depth--;
                    }
                    var indent = new string(' ', 4 + depth * 2);
                    if (rows[j] is JsonObject start && start.ContainsKey("block"))
                    {
                        depth++;
                    }
                    var cells = rows[j] is JsonObject row
                        ? "{ " + string.Join(", ", row.Select(cell => $"{Quote(cell.Key)}: {Compact(cell.Value, compact)}")) + " }"
                        : Compact(rows[j], compact);
                    text.AppendLine($"{indent}{cells}{(j < rows.Count - 1 ? "," : "")}");
                }
                text.AppendLine($"  ]{comma}");
            }
            else
            {
                AppendLine(text, "  ", $"{Quote(name)}: {Compact(value, compact)}{comma}", name);
            }
        }

        text.AppendLine("}");
        return text.ToString();
    }

    private static void AppendLine(StringBuilder text, string indent, string content, string path)
    {
        text.Append(indent).Append(content);
        if (Trailing.TryGetValue(path, out var comment))
        {
            text.Append("  // ").Append(comment);
        }
        text.AppendLine();
    }

    private static string Quote(string name) => JsonSerializer.Serialize(name);

    private static string Compact(JsonNode? node, JsonSerializerOptions options) =>
        node == null ? "null" : node.ToJsonString(options).Replace("\",\"", "\", \"");

    /// <summary>
    /// Enthaelt der Text Kommentare, die nicht von <see cref="Write"/> stammen? Dann wuerde ein
    /// Speichern aus dem Editor etwas vom Benutzer Geschriebenes verwerfen.
    /// </summary>
    public static bool HasForeignComments(string json)
    {
        foreach (var rawLine in json.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var inString = false;
            for (var i = 0; i < line.Length - 1; i++)
            {
                var c = line[i];
                if (c == '\\' && inString)
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = !inString;
                }
                else if (!inString && c == '/' && line[i + 1] == '*')
                {
                    return true;
                }
                else if (!inString && c == '/' && line[i + 1] == '/')
                {
                    var comment = line[(i + 2)..].Trim();
                    // Die Platzhalter-Liste der Beispielkonfiguration (--with-placeholders) zaehlt als Standard.
                    var isPlaceholderList = comment.Length == 0 || comment.StartsWith('{')
                        || comment is PlaceholderHeading or DrivePlaceholderHeading or GenericPlaceholderHeading;
                    if (!isPlaceholderList && !StandardComments.Contains(comment))
                    {
                        return true;
                    }
                    break;
                }
            }
        }
        return false;
    }
}
