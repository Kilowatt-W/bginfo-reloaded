using System.Text.Json.Nodes;

namespace BgInfoReloaded.Editor;

/// <summary>
/// Profile derselben Datei: oben in der Leiste wird gewaehlt, welches bearbeitet wird. Alle Eingabefelder
/// haengen weiter an <see cref="_config"/> - im Standardprofil ist das die Datei selbst
/// (<see cref="_root"/>), in einem Profil die daraus zusammengesetzte Konfiguration. Beim Wechseln und
/// Speichern wandern davon nur die Abweichungen vom Standardprofil zurueck in die Datei.
/// </summary>
public sealed partial class MainForm
{
    private const string StandardProfileName = "Standardprofil";
    private const string ItemsClipboardFormat = "BGInfo-Reloaded.Items";

    // Die geoeffnete Datei mit allen Profilen; _config ist entweder sie selbst oder das gewaehlte Profil.
    private AppConfig _root = null!;
    // Name des bearbeiteten Profils - null = Standardprofil.
    private string? _profile;
    private readonly ToolStripComboBox _profileBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false };
    private bool _fillingProfiles;
    private readonly Label _profileInfo = new() { AutoSize = true, Margin = new Padding(3, 5, 12, 5) };
    private readonly FlowLayoutPanel _profileStrip = new()
    {
        Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Visible = false,
        BackColor = Color.FromArgb(226, 238, 252), Padding = new Padding(8, 2, 8, 2)
    };

    /// <summary>Profilauswahl und -verwaltung in der Werkzeugleiste, dazu die Zeile darunter, die im Profil die Abweichungen nennt.</summary>
    private void AddProfileTools(ToolStrip bar, int iconSize)
    {
        _profileBox.Width = LogicalToDeviceUnits(170);
        _profileBox.ToolTipText = "Welches Profil bearbeitet wird. Das Standardprofil ist die Grundlage; ein Profil speichert nur,\r\nwas davon abweicht. Welcher Rechner welches Profil bekommt, steht im Reiter „Rechner“.";
        _profileBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_fillingProfiles)
            {
                SwitchProfile(_profileBox.SelectedIndex <= 0 ? null : (string)_profileBox.SelectedItem!);
            }
        };
        bar.Items.Add(new ToolStripLabel("Profil"));
        bar.Items.Add(_profileBox);

        var menu = new ToolStripDropDownButton("Profile")
        {
            Image = GlyphIcon('', iconSize),
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = "Profile anlegen, umbenennen, löschen, Abweichungen ansehen – und andere Konfigurationsdateien als Profile übernehmen",
            Margin = new Padding(1, 1, 4, 1)
        };
        menu.DropDownItems.Add("Neues Profil…", null, (_, _) => NewProfile());
        var rename = menu.DropDownItems.Add("Profil umbenennen…", null, (_, _) => RenameProfile());
        var delete = menu.DropDownItems.Add("Profil löschen", null, (_, _) => DeleteProfile());
        var differences = menu.DropDownItems.Add("Abweichungen vom Standardprofil…", null, (_, _) => ShowProfileDifferences());
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add("Konfigurationsdateien als Profile übernehmen…", null, (_, _) => MergeConfigFiles());
        menu.DropDownOpening += (_, _) => rename.Enabled = delete.Enabled = differences.Enabled = _profile != null;
        bar.Items.Add(menu);

        var show = new LinkLabel { Text = "Abweichungen anzeigen…", AutoSize = true, Margin = new Padding(3, 5, 12, 5) };
        show.LinkClicked += (_, _) => ShowProfileDifferences();
        var reset = new LinkLabel { Text = "Alles wie im Standardprofil", AutoSize = true, Margin = new Padding(3, 5, 12, 5) };
        reset.LinkClicked += (_, _) =>
        {
            if (MessageBox.Show(this, $"Alle Abweichungen des Profils „{_profile}“ verwerfen? Es gilt dann überall das Standardprofil.",
                    AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ResetToStandard(ProfileDifferences().Where(d => d.Differs).Select(d => d.Path));
            }
        };
        _tips.SetToolTip(reset, "Alle Abweichungen dieses Profils verwerfen");
        _profileInfo.Font = new Font(Font, FontStyle.Bold);
        _profileStrip.Controls.Add(_profileInfo);
        _profileStrip.Controls.Add(show);
        _profileStrip.Controls.Add(reset);
    }

    /// <summary>Neue Datei im Editor: Standardprofil waehlen.</summary>
    private void SetRoot(AppConfig root)
    {
        _root = _config = root;
        _profile = null;
        RefreshProfiles();
    }

    /// <summary>Haelt die Aenderungen am gerade bearbeiteten Profil als Abweichungen in der Datei fest.</summary>
    private void CommitProfile()
    {
        if (_profile != null)
        {
            _root.StoreProfile(_profile, _config);
        }
    }

    private void SwitchProfile(string? name)
    {
        name = _root.FindProfile(name);
        if (string.Equals(name, _profile, StringComparison.Ordinal) && (_profile == null) == ReferenceEquals(_config, _root))
        {
            return;
        }
        CommitProfile();
        _profile = name;
        _config = name == null ? _root : _root.WithProfile(name) ?? _root;
        if (ReferenceEquals(_config, _root))
        {
            _profile = null;
        }
        RefreshProfiles();
        ShowConfig();
        _status.Text = _profile == null
            ? "Standardprofil: die Grundlage für alle Profile."
            : $"Profil „{_profile}“: Änderungen gelten nur hier; alles Übrige kommt aus dem Standardprofil.";
    }

    /// <summary>Nach einer Aenderung am Standardprofil, waehrend ein Profil gewaehlt ist: das Profil neu zusammensetzen, damit es sie erbt.</summary>
    private void RebuildProfile()
    {
        if (_profile != null)
        {
            _config = _root.WithProfile(_profile) ?? _root;
            ShowConfig();
        }
    }

    private void RefreshProfiles()
    {
        _fillingProfiles = true;
        _profileBox.Items.Clear();
        _profileBox.Items.Add(StandardProfileName);
        foreach (var name in _root.Profiles?.Keys ?? Enumerable.Empty<string>())
        {
            _profileBox.Items.Add(name);
        }
        _profileBox.SelectedIndex = _profile == null ? 0 : Math.Max(0, _profileBox.Items.IndexOf(_profile));
        _fillingProfiles = false;
        UpdateProfileStrip();
    }

    /// <summary>Eine Einstellung mit ihrem Wert im Standardprofil und im gewaehlten Profil.</summary>
    private sealed record ProfileDifference(string Path, string Caption, JsonNode? Standard, JsonNode? Profile)
    {
        public bool Differs => !JsonNode.DeepEquals(Standard, Profile);
    }

    /// <summary>
    /// Alle Einstellungen des gewaehlten Profils neben denen des Standardprofils - eine Ebene tief wie
    /// <see cref="AppConfig.Diff"/> ("panel.position", "fields.name", "items" als Ganzes).
    /// </summary>
    private List<ProfileDifference> ProfileDifferences()
    {
        var result = new List<ProfileDifference>();
        if (_profile == null)
        {
            return result;
        }
        static Dictionary<string, JsonNode?> Flatten(JsonObject node)
        {
            var flat = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in node)
            {
                if (key is "configFormat" or "profiles" or "hosts")
                {
                    continue;
                }
                if (value is JsonObject section)
                {
                    foreach (var (child, inner) in section)
                    {
                        flat[$"{key}.{child}"] = inner;
                    }
                }
                else
                {
                    flat[key] = value;
                }
            }
            return flat;
        }
        var standard = Flatten(_root.ToNode());
        var profile = Flatten(_config.ToNode());
        foreach (var path in standard.Keys.Concat(profile.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            standard.TryGetValue(path, out var a);
            profile.TryGetValue(path, out var b);
            var caption = path == "items" ? "Zeilen des Panels"
                : path.StartsWith("fields.", StringComparison.OrdinalIgnoreCase) ? $"Feld {{{path[7..]}}}"
                : path;
            result.Add(new ProfileDifference(path, caption, a, b));
        }
        return result;
    }

    private static string ShowNode(string path, JsonNode? node)
    {
        if (node == null)
        {
            return "—";
        }
        if (node is JsonArray list)
        {
            return path == "items" ? $"{list.Count} Zeilen" : list.Count <= 3 ? node.ToJsonString() : $"{list.Count} Einträge";
        }
        var text = node is JsonValue value && value.TryGetValue<string>(out var s) ? s : node.ToJsonString();
        return text.Length > 90 ? text[..87] + "…" : text;
    }

    private static string DescribeNode(string path, JsonNode? node)
    {
        if (path == "items" && node is JsonArray rows)
        {
            var items = AppConfig.ItemsFromJson(rows.ToJsonString()) ?? new();
            return string.Join(Environment.NewLine, items.Take(30).Select(item => DescribeContent(item).Trim()))
                + (items.Count > 30 ? Environment.NewLine + "…" : "");
        }
        var text = node?.ToJsonString() ?? "(nicht gesetzt)";
        return text.Length > 1500 ? text[..1500] + "…" : text;
    }

    private void UpdateProfileStrip()
    {
        RefreshProfileList();
        _profileStrip.Visible = _profile != null;
        if (_profile == null)
        {
            return;
        }
        var differing = ProfileDifferences().Where(d => d.Differs).ToList();
        _profileInfo.Text = differing.Count == 0
            ? $"Profil „{_profile}“ – noch keine Abweichungen: alles wie im Standardprofil."
            : $"Profil „{_profile}“ – {differing.Count} Abweichung{(differing.Count == 1 ? "" : "en")} vom Standardprofil: "
              + string.Join(", ", differing.Take(4).Select(d => d.Caption)) + (differing.Count > 4 ? ", …" : "");
    }

    /// <summary>Uebersicht: jede Einstellung mit dem Wert im Standardprofil und in diesem Profil; markierte zurueck auf den Standard.</summary>
    private void ShowProfileDifferences()
    {
        if (_profile == null)
        {
            MessageBox.Show(this, "Das Standardprofil ist die Grundlage aller Profile – Abweichungen gibt es nur in einem Profil.\r\nOben in der Leiste ein Profil wählen.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new Form
        {
            Text = $"Profil „{_profile}“ und Standardprofil",
            FormBorderStyle = FormBorderStyle.Sizable, MinimizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent,
            Size = new Size(LogicalToDeviceUnits(1000), LogicalToDeviceUnits(560)), Padding = new Padding(10), Font = Font
        };
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, CheckBoxes = true, ShowItemToolTips = true, HideSelection = false };
        list.Columns.Add("Einstellung", LogicalToDeviceUnits(230));
        list.Columns.Add("Standardprofil", LogicalToDeviceUnits(330));
        list.Columns.Add($"Profil „{_profile}“", LogicalToDeviceUnits(330));
        var showAll = new CheckBox { Text = "Auch Einstellungen zeigen, die aus dem Standardprofil kommen", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 6, 0, 0) };
        void Fill()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var difference in ProfileDifferences().Where(d => d.Differs || showAll.Checked)
                         .OrderBy(d => !d.Differs).ThenBy(d => d.Path, StringComparer.OrdinalIgnoreCase))
            {
                var row = new ListViewItem(new[]
                {
                    difference.Caption,
                    ShowNode(difference.Path, difference.Standard),
                    difference.Differs ? ShowNode(difference.Path, difference.Profile) : "= Standardprofil"
                })
                {
                    Tag = difference.Path,
                    ToolTipText = $"Standardprofil:{Environment.NewLine}{DescribeNode(difference.Path, difference.Standard)}"
                        + (difference.Differs ? $"{Environment.NewLine}{Environment.NewLine}Profil „{_profile}“:{Environment.NewLine}{DescribeNode(difference.Path, difference.Profile)}" : "")
                };
                if (!difference.Differs)
                {
                    row.ForeColor = SystemColors.GrayText;
                }
                list.Items.Add(row);
            }
            list.EndUpdate();
        }
        showAll.CheckedChanged += (_, _) => Fill();
        list.ItemCheck += (_, e) =>
        {
            // Nur Abweichungen lassen sich zuruecksetzen.
            if (list.Items[e.Index].ForeColor == SystemColors.GrayText)
            {
                e.NewValue = CheckState.Unchecked;
            }
        };
        var hint = new Label
        {
            Dock = DockStyle.Top, AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 0, 0, 6),
            Text = "Oben stehen die Abweichungen dieses Profils, daneben der Wert des Standardprofils. Markierte Einstellungen lassen sich auf das Standardprofil\r\n" +
                   "zurücksetzen – sie folgen danach wieder dessen Änderungen. Die Maus über einer Zeile zeigt beide Werte vollständig."
        };
        var useStandard = new Button { Text = "Markierte: Standardprofil verwenden", AutoSize = true };
        useStandard.Click += (_, _) =>
        {
            var paths = list.CheckedItems.Cast<ListViewItem>().Select(row => (string)row.Tag!).ToList();
            if (paths.Count == 0)
            {
                MessageBox.Show(dialog, "Zuerst die Kästchen der Einstellungen anhaken, die wieder aus dem Standardprofil kommen sollen.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ResetToStandard(paths);
            Fill();
        };
        var close = new Button { Text = "Schließen", DialogResult = DialogResult.Cancel, AutoSize = true };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        footer.Controls.Add(close);
        footer.Controls.Add(useStandard);
        dialog.Controls.Add(list);
        dialog.Controls.Add(showAll);
        dialog.Controls.Add(hint);
        dialog.Controls.Add(footer);
        dialog.CancelButton = close;
        Fill();
        dialog.ShowDialog(this);
    }

    /// <summary>Nimmt Einstellungen aus dem Profil heraus - sie kommen danach wieder aus dem Standardprofil.</summary>
    private void ResetToStandard(IEnumerable<string> paths)
    {
        if (_profile == null)
        {
            return;
        }
        CommitProfile();
        var key = _root.FindProfile(_profile)!;
        var overlay = _root.Profiles![key];
        var count = 0;
        foreach (var path in paths)
        {
            var parts = path.Split('.', 2);
            var section = overlay.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, parts[0], StringComparison.OrdinalIgnoreCase));
            if (section == null)
            {
                continue;
            }
            if (parts.Length == 1 || overlay[section] is not JsonObject block)
            {
                overlay.Remove(section);
            }
            else
            {
                var child = block.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, parts[1], StringComparison.OrdinalIgnoreCase));
                if (child != null)
                {
                    block.Remove(child);
                }
                if (block.Count == 0)
                {
                    overlay.Remove(section);
                }
            }
            count++;
        }
        _config = _root.WithProfile(key) ?? _root;
        ShowConfig();
        Changed();
        _status.Text = $"{count} Einstellung(en) kommen wieder aus dem Standardprofil.";
    }

    /// <summary>Kleiner Dialog fuer einen Namen; optional mit einem Kaestchen darunter.</summary>
    private (string Name, bool Checked)? AskProfileName(string title, string initial, string? checkText = null)
    {
        using var dialog = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Font = Font
        };
        var name = new TextBox { Width = LogicalToDeviceUnits(300), Text = initial };
        var check = new CheckBox { Text = checkText ?? "", AutoSize = true, Visible = checkText != null, Margin = new Padding(3, 8, 3, 3) };
        var accept = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "Name des Profils", AutoSize = true });
        layout.Controls.Add(name);
        layout.Controls.Add(check);
        var footer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        footer.Controls.Add(cancel);
        footer.Controls.Add(accept);
        layout.Controls.Add(footer);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;
        while (dialog.ShowDialog(this) == DialogResult.OK)
        {
            if (CheckProfileName(dialog, name.Text.Trim(), initial))
            {
                return (name.Text.Trim(), check.Checked);
            }
        }
        return null;
    }

    /// <summary>Ist der Name als Profilname zu gebrauchen (nicht leer, nicht "Standardprofil", noch frei)? Sonst Hinweis.</summary>
    private bool CheckProfileName(IWin32Window owner, string text, string? keep)
    {
        if (text.Length == 0 || string.Equals(text, StandardProfileName, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(owner, $"Bitte einen Namen angeben (nicht „{StandardProfileName}“).", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        var clash = _root.FindProfile(text);
        if (clash != null && !string.Equals(clash, keep, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(owner, $"Ein Profil „{clash}“ gibt es schon.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        return true;
    }

    /// <summary>Bereiche einer Konfiguration, die sich beim Uebernehmen einzeln an- und abwaehlen lassen (Schluessel der obersten Ebene).</summary>
    private static readonly (string Caption, string[] Keys)[] ImportParts =
    {
        ("Zeilen des Panels", new[] { "items" }),
        ("Eigene Felder samt Bezeichnungen", new[] { "fields" }),
        ("Aussehen: Panel, Schrift, Farben, Rahmen", new[] { "panel", "font", "colors", "border" }),
        ("Datums- und Zahlenformat", new[] { "format" }),
        ("Hintergrund und Monitore", new[] { "wallpaper", "monitors" }),
        ("Zentrale Datei (include)", new[] { "include" }),
        ("Update-Einstellungen", new[] { "update" }),
        ("Version und Beschreibung der Datei", new[] { "version", "description" })
    };

    /// <summary>Ein eigenes Feld, das beim Uebernehmen ins Standardprofil wandern soll.</summary>
    private sealed record CentralField(string Name, string Value, string? Label);

    /// <summary>
    /// Fragt beim Uebernehmen, welche Bereiche ins Profil sollen - mit der Zahl der Werte, die je Bereich
    /// vom Standardprofil abweichen. Optional mit Namensfeld. Dazu die eigenen Felder der Dateien, die
    /// noch nicht so im Standardprofil stehen: angehakte wandern dorthin und gelten fuer alle Profile.
    /// </summary>
    private (string? Name, HashSet<string> Keys, List<CentralField> Central)? AskImportParts(string title, string intro, string? initialName, IReadOnlyList<AppConfig> imported)
    {
        var overlays = imported.Select(config => AppConfig.Diff(config.ToNode(), _root.ToNode())).ToList();
        using var dialog = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Font = Font
        };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = intro, AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(620), 0), Margin = new Padding(3, 3, 3, 10) });
        TextBox? name = null;
        if (initialName != null)
        {
            layout.Controls.Add(new Label { Text = "Name des Profils", AutoSize = true });
            name = new TextBox { Width = LogicalToDeviceUnits(300), Text = initialName, Margin = new Padding(3, 3, 3, 10) };
            layout.Controls.Add(name);
        }
        layout.Controls.Add(new Label { Text = "Übernehmen – nicht angehakte Bereiche kommen aus dem Standardprofil:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });

        // Alle Schluessel, die in einer der Dateien abweichen; Unbekanntes landet unter "Sonstiges".
        var known = ImportParts.SelectMany(p => p.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = overlays.SelectMany(o => o.Select(pair => pair.Key)).Where(k => !known.Contains(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var parts = ImportParts.Concat(others.Length > 0 ? new[] { ("Sonstiges", others) } : Array.Empty<(string, string[])>()).ToList();
        var boxes = new List<(CheckBox Box, string[] Keys)>();
        foreach (var (caption, keys) in parts)
        {
            var details = overlays.SelectMany(o => o.Where(pair => keys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value is JsonObject block && !pair.Key.Equals("items", StringComparison.OrdinalIgnoreCase)
                    ? block.Select(child => $"{pair.Key}.{child.Key}")
                    : new[] { pair.Key })).Distinct().ToList();
            var box = new CheckBox
            {
                AutoSize = true, Checked = details.Count > 0, Enabled = details.Count > 0, Margin = new Padding(12, 3, 3, 3),
                Text = details.Count == 0 ? $"{caption}   (wie im Standardprofil)"
                    : keys.SequenceEqual(new[] { "items" }) ? $"{caption}   (eigene Zeilen)"
                    : $"{caption}   ({details.Count} {(details.Count == 1 ? "Wert weicht" : "Werte weichen")} ab)"
            };
            if (details.Count > 0)
            {
                _tips.SetToolTip(box, string.Join(Environment.NewLine, details.Take(40)) + (details.Count > 40 ? Environment.NewLine + "…" : ""));
            }
            boxes.Add((box, keys));
            layout.Controls.Add(box);
        }

        // Eigene Felder, die es im Standardprofil noch nicht (oder anders) gibt - bei gleichem Namen in
        // mehreren Dateien zaehlt die erste.
        var candidates = new List<(CentralField Field, bool IsNew)>();
        foreach (var config in imported)
        {
            foreach (var (fieldName, value) in config.Fields)
            {
                config.Fields.Labels.TryGetValue(fieldName, out var label);
                _root.Fields.Labels.TryGetValue(fieldName, out var rootLabel);
                var exists = _root.Fields.TryGetValue(fieldName, out var rootValue);
                if ((exists && rootValue == value && (rootLabel ?? "") == (label ?? ""))
                    || candidates.Any(c => string.Equals(c.Field.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                candidates.Add((new CentralField(fieldName, value, label), !exists));
            }
        }
        var central = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Width = LogicalToDeviceUnits(620), Margin = new Padding(12, 3, 3, 3) };
        if (candidates.Count > 0)
        {
            layout.Controls.Add(new Label
            {
                Text = "Eigene Felder ins Standardprofil übernehmen – sie gelten dann für alle Profile (vererbt):",
                AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 12, 3, 3)
            });
            layout.Controls.Add(new Label
            {
                Text = "Neue Felder sind vorausgewählt. Ein Feld, das das Standardprofil schon mit anderem Wert hat, ändert dort den Wert für alle Profile, die ihn nicht selbst festlegen.\r\n" +
                       "Nicht angehakte Felder bleiben – wenn oben „Eigene Felder“ angehakt ist – nur in diesem Profil.",
                AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(LogicalToDeviceUnits(620), 0), Margin = new Padding(12, 0, 3, 3)
            });
            foreach (var (field, isNew) in candidates)
            {
                _root.Fields.TryGetValue(field.Name, out var old);
                var text = $"{{{field.Name}}} = {field.Value}" + (string.IsNullOrEmpty(field.Label) ? "" : $"   – „{field.Label}“")
                    + (isNew ? "   (neu)" : $"   (ersetzt im Standardprofil: {old})");
                central.Items.Add(text, isNew);
            }
            central.Height = Math.Min(candidates.Count, 8) * central.ItemHeight + LogicalToDeviceUnits(6);
            layout.Controls.Add(central);
        }
        var accept = new Button { Text = "Übernehmen", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
        var footer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        footer.Controls.Add(cancel);
        footer.Controls.Add(accept);
        layout.Controls.Add(footer);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;
        while (dialog.ShowDialog(this) == DialogResult.OK)
        {
            if (name != null && !CheckProfileName(dialog, name.Text.Trim(), null))
            {
                continue;
            }
            var chosen = boxes.Where(b => b.Box.Checked).SelectMany(b => b.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toCentral = central.CheckedIndices.Cast<int>().Select(i => candidates[i].Field).ToList();
            return (name?.Text.Trim(), chosen, toCentral);
        }
        return null;
    }

    /// <summary>
    /// Traegt Felder ins Standardprofil ein - vor dem Berechnen der Profile, damit diese sie erben statt
    /// sie selbst zu enthalten. Danach das bearbeitete Profil neu zusammensetzen (<see cref="RebuildProfile"/>).
    /// </summary>
    private void AddCentralFields(IEnumerable<CentralField> fields)
    {
        foreach (var field in fields)
        {
            _root.Fields[field.Name] = field.Value;
            if (string.IsNullOrEmpty(field.Label))
            {
                _root.Fields.Labels.Remove(field.Name);
            }
            else
            {
                _root.Fields.Labels[field.Name] = field.Label;
            }
        }
    }

    /// <summary>Nach <see cref="AddCentralFields"/>: Feldtabelle und Platzhalter zeigen die neuen Felder.</summary>
    private void ShowCentralFields(List<CentralField> fields)
    {
        if (fields.Count == 0)
        {
            return;
        }
        if (_profile != null)
        {
            RebuildProfile(); // das bearbeitete Profil erbt sie
        }
        else
        {
            ShowConfig();
        }
    }

    /// <summary>Die Abweichungen einer uebernommenen Datei, beschraenkt auf die gewaehlten Bereiche.</summary>
    private JsonObject ImportOverlay(AppConfig imported, ISet<string> keys)
    {
        var overlay = AppConfig.Diff(imported.ToNode(), _root.ToNode());
        foreach (var key in overlay.Select(pair => pair.Key).Where(k => !keys.Contains(k)).ToList())
        {
            overlay.Remove(key);
        }
        return overlay;
    }

    private void NewProfile()
    {
        var answer = AskProfileName("Neues Profil", "", _profile != null ? $"Abweichungen von „{_profile}“ übernehmen (sonst wie das Standardprofil)" : null);
        if (answer is not { } chosen)
        {
            return;
        }
        CommitProfile();
        _root.Profiles ??= new();
        _root.Profiles[chosen.Name] = chosen.Checked && _root.FindProfile(_profile) is { } source
            ? _root.Profiles[source].DeepClone().AsObject()
            : new JsonObject();
        Changed();
        SwitchProfile(chosen.Name);
        _status.Text = $"Profil „{chosen.Name}“ angelegt. Im Reiter „Rechner“ einer Gruppe zuordnen – sonst bekommt es kein Rechner.";
    }

    private void RenameProfile()
    {
        if (_profile == null || AskProfileName("Profil umbenennen", _profile) is not { } chosen || chosen.Name == _profile)
        {
            return;
        }
        CommitProfile();
        var old = _profile;
        // Neu aufbauen statt umhaengen, damit die Reihenfolge bleibt.
        _root.Profiles = _root.Profiles!.ToDictionary(p => p.Key == old ? chosen.Name : p.Key, p => p.Value);
        foreach (var group in _root.Hosts.Groups.Where(g => string.Equals(g.Profile, old, StringComparison.OrdinalIgnoreCase)))
        {
            group.Profile = chosen.Name;
        }
        _profile = chosen.Name;
        _config.ActiveProfile = chosen.Name;
        RefreshProfiles();
        RefreshGroups();
        Changed();
        _status.Text = $"Profil „{old}“ heißt jetzt „{chosen.Name}“ – auch in den Gruppen.";
    }

    private void DeleteProfile()
    {
        if (_profile == null)
        {
            return;
        }
        var users = _root.Hosts.Groups.Where(g => string.Equals(g.Profile, _profile, StringComparison.OrdinalIgnoreCase)).ToList();
        var question = $"Profil „{_profile}“ löschen?"
            + (users.Count > 0 ? $"\r\n\r\nDiese Gruppen verweisen darauf und bekommen danach das Standardprofil:\r\n{string.Join(", ", users.Select(g => g.Name))}" : "");
        if (MessageBox.Show(this, question, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        var name = _profile;
        _root.Profiles!.Remove(_root.FindProfile(name)!);
        if (_root.Profiles.Count == 0)
        {
            _root.Profiles = null;
        }
        foreach (var group in users)
        {
            group.Profile = null;
        }
        _profile = null;
        _config = _root;
        RefreshProfiles();
        ShowConfig();
        Changed();
        _status.Text = $"Profil „{name}“ gelöscht.";
    }

    /// <summary>
    /// Andere Konfigurationsdateien (z.B. die bisherigen Dateien der Gruppen) als Profile in diese Datei
    /// holen. Gespeichert werden nur ihre Abweichungen vom Standardprofil; Gruppen, die auf eine der
    /// Dateien verwiesen haben, zeigen danach auf das Profil. Die Dateien selbst bleiben unangetastet.
    /// </summary>
    private void MergeConfigFiles()
    {
        using var open = new OpenFileDialog
        {
            Title = "Konfigurationsdateien als Profile übernehmen", Multiselect = true,
            Filter = "BgInfo-Konfiguration (*.json)|*.json|Alle Dateien|*.*", InitialDirectory = ConfigDirectory
        };
        if (open.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        CommitProfile();
        var plan = new List<(string File, string Name, AppConfig Config, List<HostGroup> Groups)>();
        var problems = new List<string>();
        var taken = new HashSet<string>(_root.Profiles?.Keys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var file in open.FileNames)
        {
            if (IsOpenFile(file))
            {
                problems.Add($"{Path.GetFileName(file)}: das ist die geöffnete Datei selbst");
                continue;
            }
            try
            {
                var config = LoadForImport(file);
                var name = Path.GetFileNameWithoutExtension(file);
                foreach (var prefix in new[] { "BGInfo-Reloaded-", "BGInfo-Reloaded_", "BGInfo-", "BGInfo_" })
                {
                    if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name[prefix.Length..];
                        break;
                    }
                }
                var unique = UniqueProfileName(name, taken);
                taken.Add(unique);
                var groups = _root.Hosts.Groups.Where(g => g.UsesOwnConfig && PointsTo(g.Config!, file)).ToList();
                plan.Add((file, unique, config, groups));
            }
            catch (Exception ex)
            {
                problems.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        if (plan.Count == 0)
        {
            MessageBox.Show(this, "Keine Datei übernommen." + (problems.Count > 0 ? "\r\n\r\n" + string.Join("\r\n", problems) : ""), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var summary = string.Join("\r\n", plan.Select(p => $"• {Path.GetFileName(p.File)}  →  Profil „{p.Name}“"
            + (p.Groups.Count > 0 ? $"  (Gruppe {string.Join(", ", p.Groups.Select(g => g.Name))} wird umgestellt)" : "")));
        var intro = $"Als Profile übernehmen – gespeichert werden nur die Abweichungen vom Standardprofil:\r\n\r\n{summary}"
            + (problems.Count > 0 ? "\r\n\r\nNicht übernommen:\r\n" + string.Join("\r\n", problems) : "")
            + "\r\n\r\nRechnergruppen in diesen Dateien werden nicht übernommen; die Dateien selbst bleiben unverändert. Die Auswahl unten gilt für alle Dateien.";
        if (AskImportParts("Konfigurationsdateien als Profile übernehmen", intro, null, plan.Select(p => p.Config).ToList()) is not { } chosen)
        {
            return;
        }
        AddCentralFields(chosen.Central);
        _root.Profiles ??= new();
        foreach (var (_, name, config, groups) in plan)
        {
            _root.Profiles[name] = ImportOverlay(config, chosen.Keys);
            foreach (var group in groups)
            {
                group.Config = null;
                group.Profile = name;
                group.Mode = HostGroupMode.ThisConfig; // siehe EditGroup: aeltere Versionen nehmen dann das Standardprofil
            }
        }
        ShowCentralFields(chosen.Central);
        RefreshProfiles();
        RefreshGroups();
        Changed();
        _status.Text = $"{plan.Count} Datei(en) als Profile übernommen: {string.Join(", ", plan.Select(p => p.Name))}"
            + (chosen.Central.Count > 0 ? $"; ins Standardprofil: {string.Join(", ", chosen.Central.Select(c => $"{{{c.Name}}}"))}" : "") + ". Speichern nicht vergessen.";
    }

    private bool IsOpenFile(string file) =>
        _path != null && string.Equals(Path.GetFullPath(file), Path.GetFullPath(_path), StringComparison.OrdinalIgnoreCase);

    /// <summary>Liest eine Konfigurationsdatei zum Uebernehmen als Profil - mit Pfaden relativ zur geoeffneten Datei.</summary>
    private AppConfig LoadForImport(string file)
    {
        var config = AppConfig.FromJson(File.ReadAllText(file), file);
        RebasePaths(config, Path.GetDirectoryName(Path.GetFullPath(file))!);
        return config;
    }

    private static string UniqueProfileName(string name, ICollection<string> taken)
    {
        var unique = name;
        for (var n = 2; taken.Contains(unique) || string.Equals(unique, StandardProfileName, StringComparison.OrdinalIgnoreCase); n++)
        {
            unique = $"{name} {n}";
        }
        return unique;
    }

    /// <summary>
    /// Knopf im Reiter "Rechner": die Datei der markierten Gruppe als Profil in diese Datei holen und
    /// die Gruppe (und jede andere, die auf dieselbe Datei zeigt) auf das Profil umstellen.
    /// </summary>
    private void ImportGroupFile()
    {
        if (SelectedGroup is not { UsesOwnConfig: true } group)
        {
            _status.Text = "Die markierte Gruppe verweist auf keine Konfigurationsdatei – zuerst eine solche Gruppe markieren.";
            return;
        }
        var source = Environment.ExpandEnvironmentVariables(group.Config!.Trim());
        if (AppConfig.IsUrl(source))
        {
            MessageBox.Show(this, $"Die Konfiguration dieser Gruppe ist eine URL und lässt sich hier nicht übernehmen:\r\n{source}\r\n\r\nDatei herunterladen und über „Profile“ → „Konfigurationsdateien als Profile übernehmen…“ holen.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var file = Path.IsPathRooted(source) ? source : Path.GetFullPath(Path.Combine(ConfigDirectory, source));
        if (!File.Exists(file))
        {
            MessageBox.Show(this, $"Die Datei der Gruppe gibt es nicht:\r\n{file}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (IsOpenFile(file))
        {
            MessageBox.Show(this, "Die Gruppe verweist auf die geöffnete Datei selbst – stattdessen „Standardprofil“ als Wirkung wählen.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        AppConfig imported;
        try
        {
            imported = LoadForImport(file);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Die Datei ließ sich nicht lesen:\r\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        CommitProfile();
        var taken = new HashSet<string>(_root.Profiles?.Keys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var groups = _root.Hosts.Groups.Where(g => g.UsesOwnConfig && PointsTo(g.Config!, file)).ToList();
        var intro = $"Die Einstellungen aus {file} werden als Profil in diese Datei geholt – gespeichert wird nur, was vom Standardprofil abweicht. "
            + $"Danach bekommt die Gruppe {string.Join(", ", groups.Select(g => $"„{g.Name}“"))} das Profil statt der Datei; die Datei selbst bleibt unverändert.";
        if (AskImportParts($"„{Path.GetFileName(file)}“ als Profil übernehmen", intro, UniqueProfileName(group.Name, taken), new[] { imported })
                is not { Name: { } name } chosen)
        {
            return;
        }
        AddCentralFields(chosen.Central);
        _root.Profiles ??= new();
        _root.Profiles[name] = ImportOverlay(imported, chosen.Keys);
        foreach (var other in groups)
        {
            other.Config = null;
            other.Profile = name;
            other.Mode = HostGroupMode.ThisConfig; // siehe EditGroup: aeltere Versionen nehmen dann das Standardprofil
        }
        ShowCentralFields(chosen.Central);
        RefreshProfiles();
        RefreshGroups();
        Changed();
        var count = _root.Profiles[name].Sum(pair => pair.Value is JsonObject block ? block.Count : 1);
        _status.Text = $"„{Path.GetFileName(file)}“ ist jetzt Profil „{name}“ ({count} Abweichungen vom Standardprofil); umgestellt: "
            + string.Join(", ", groups.Select(g => g.Name))
            + (chosen.Central.Count > 0 ? $"; ins Standardprofil: {string.Join(", ", chosen.Central.Select(c => $"{{{c.Name}}}"))}" : "")
            + ". Speichern nicht vergessen – die Datei selbst bleibt unverändert.";
    }

    // ------------------------------------------------------------------ Liste der Profile (Reiter "Rechner")

    private readonly ListView _profileList = new() { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, ShowItemToolTips = true };

    private void BuildProfileList(TableLayoutPanel table)
    {
        Heading(table, "Profile dieser Datei");
        Row(table, "", Hint("Ein Profil enthält nur, was vom Standardprofil abweicht – alles andere kommt aus dem Standardprofil.\r\n" +
            "Doppelklick oder „Bearbeiten“ wählt das Profil oben in der Leiste; welche Rechner es bekommen, legen die Gruppen oben fest."));
        _profileList.Columns.Add("Profil", LogicalToDeviceUnits(170));
        _profileList.Columns.Add("Abweichungen vom Standardprofil", LogicalToDeviceUnits(380));
        _profileList.Columns.Add("Bekommen", LogicalToDeviceUnits(300));
        _profileList.Size = new Size(LogicalToDeviceUnits(860), LogicalToDeviceUnits(150));
        _profileList.DoubleClick += (_, _) => EditListedProfile();

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(_profileList.Width, 0), Margin = Padding.Empty };
        void ProfileButton(string text, string tip, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 6) };
            button.Click += (_, _) => action();
            _tips.SetToolTip(button, tip);
            buttons.Controls.Add(button);
        }
        ProfileButton("Bearbeiten", "Das markierte Profil oben in der Leiste wählen (auch per Doppelklick)", EditListedProfile);
        ProfileButton("Neues Profil…", "Ein neues Profil anlegen – zunächst wie das Standardprofil", NewProfile);
        ProfileButton("Umbenennen…", "Das markierte Profil umbenennen; Gruppen ziehen mit", () => WithListedProfile(RenameProfile));
        ProfileButton("Löschen", "Das markierte Profil löschen; seine Gruppen bekommen danach das Standardprofil", () => WithListedProfile(DeleteProfile));
        ProfileButton("Abweichungen anzeigen…", "Das markierte Profil dem Standardprofil gegenüberstellen", () => WithListedProfile(ShowProfileDifferences));
        ProfileButton("Dateien als Profile übernehmen…", "Andere Konfigurationsdateien als Profile in diese Datei holen", MergeConfigFiles);
        Row(table, "", _profileList);
        Row(table, "", buttons);
    }

    /// <summary>Name des markierten Profils; "" = Standardprofil, null = nichts markiert.</summary>
    private string? ListedProfile => _profileList.SelectedItems.Count > 0 ? _profileList.SelectedItems[0].Tag as string : null;

    private void EditListedProfile()
    {
        if (ListedProfile is { } name)
        {
            SwitchProfile(name.Length == 0 ? null : name);
        }
    }

    private void WithListedProfile(Action action)
    {
        if (ListedProfile is not { Length: > 0 } name)
        {
            _status.Text = "Zuerst ein Profil in der Liste markieren (nicht das Standardprofil).";
            return;
        }
        SwitchProfile(name);
        action();
    }

    private void RefreshProfileList()
    {
        if (_root == null)
        {
            return;
        }
        var selected = ListedProfile;
        string Users(Func<HostGroup, bool> uses, bool standard)
        {
            var names = _root.Hosts.Groups.Where(g => !g.Excludes && !g.UsesOwnConfig && uses(g)).Select(g => g.Name).ToList();
            if (standard)
            {
                names.Add("Rechner ohne Gruppe");
            }
            return names.Count == 0 ? "(keine Gruppe – bekommt kein Rechner)" : string.Join(", ", names);
        }
        _profileList.BeginUpdate();
        _profileList.Items.Clear();
        _profileList.Items.Add(new ListViewItem(new[]
        {
            StandardProfileName, "(Grundlage aller Profile)",
            Users(g => !g.UsesProfile || _root.FindProfile(g.Profile) == null, standard: true)
        }) { Tag = "" });
        foreach (var (name, overlay) in _root.Profiles ?? new())
        {
            // Das gerade bearbeitete Profil live, die anderen aus der Datei.
            var captions = string.Equals(name, _profile, StringComparison.OrdinalIgnoreCase)
                ? ProfileDifferences().Where(d => d.Differs).Select(d => d.Caption).ToList()
                : overlay.SelectMany(pair => pair.Value is JsonObject block && pair.Key != "items"
                    ? block.Where(child => child.Value != null || !pair.Key.Equals("fields", StringComparison.OrdinalIgnoreCase))
                        .Select(child => pair.Key.Equals("fields", StringComparison.OrdinalIgnoreCase) ? $"Feld {{{child.Key}}}" : $"{pair.Key}.{child.Key}")
                    : new[] { pair.Key.Equals("items", StringComparison.OrdinalIgnoreCase) ? "Zeilen des Panels" : pair.Key }).ToList();
            var row = new ListViewItem(new[]
            {
                name,
                captions.Count == 0 ? "keine – wie das Standardprofil" : $"{captions.Count}: " + string.Join(", ", captions.Take(5)) + (captions.Count > 5 ? ", …" : ""),
                Users(g => g.UsesProfile && string.Equals(g.Profile, name, StringComparison.OrdinalIgnoreCase), standard: false)
            }) { Tag = name, ToolTipText = string.Join(Environment.NewLine, captions.Take(40)) };
            _profileList.Items.Add(row);
        }
        foreach (ListViewItem item in _profileList.Items)
        {
            var tag = (string)item.Tag!;
            if (tag.Length == 0 ? _profile == null : string.Equals(tag, _profile, StringComparison.OrdinalIgnoreCase))
            {
                item.BackColor = Color.FromArgb(226, 238, 252); // wird gerade bearbeitet
            }
            item.Selected = tag == selected;
        }
        _profileList.EndUpdate();
    }

    /// <summary>Verweist der Eintrag "config" einer Gruppe auf diese Datei?</summary>
    private bool PointsTo(string config, string file)
    {
        var source = Environment.ExpandEnvironmentVariables(config.Trim());
        if (AppConfig.IsUrl(source))
        {
            return false;
        }
        try
        {
            var full = Path.IsPathRooted(source) ? source : Path.Combine(ConfigDirectory, source);
            return string.Equals(Path.GetFullPath(full), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>Relative Pfade einer uebernommenen Datei galten fuer deren Ordner - auf den Ordner dieser Datei umschreiben.</summary>
    private void RebasePaths(AppConfig config, string fromDirectory)
    {
        string? Rebase(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || AppConfig.IsUrl(path) || path.Contains('%') || Path.IsPathRooted(path))
            {
                return path;
            }
            return ShortenPath(Path.GetFullPath(Path.Combine(fromDirectory, path)), ConfigDirectory);
        }
        config.Wallpaper.Path = Rebase(config.Wallpaper.Path);
        if (config.Wallpaper.ByResolution is { } map)
        {
            foreach (var key in map.Keys.ToList())
            {
                map[key] = Rebase(map[key])!;
            }
        }
        config.Include = Rebase(config.Include);
        config.Update.Path = Rebase(config.Update.Path);
    }

    // ------------------------------------------------------------------ Kopieren zwischen Profilen

    /// <summary>Die markierten Zeilen in Listenreihenfolge.</summary>
    private List<int> SelectedItemIndices() =>
        _items.SelectedIndices.Cast<int>().Where(i => i < _config.Items.Count).OrderBy(i => i).ToList();

    /// <summary>Die markierten Zeilen samt dem Inhalt markierter Blockanfaenge (bis zum Blockende) - als Indizes.</summary>
    private List<int> SelectedItemsWithBlocks()
    {
        var result = new SortedSet<int>();
        foreach (var index in SelectedItemIndices())
        {
            result.Add(index);
            if (_config.Items[index].Block == null)
            {
                continue;
            }
            for (int i = index + 1, depth = 1; i < _config.Items.Count; i++)
            {
                result.Add(i);
                depth += _config.Items[i].Block != null ? 1 : _config.Items[i].EndBlock ? -1 : 0;
                if (depth == 0)
                {
                    break;
                }
            }
        }
        return result.ToList();
    }

    /// <summary>
    /// Kopien der markierten Zeilen (Bloecke vollstaendig). Ohne die Markierung "fuer alle Profile" - als
    /// Kopie sind sie gewoehnliche Zeilen, sonst stuenden sie doppelt da.
    /// </summary>
    private List<ItemConfig> ItemsToCopy()
    {
        var copies = AppConfig.ItemsFromJson(AppConfig.ItemsToJson(SelectedItemsWithBlocks().Select(i => _config.Items[i]))) ?? new();
        copies.ForEach(item => item.AllProfiles = null);
        return copies;
    }

    private void CopyItems()
    {
        var items = ItemsToCopy();
        if (items.Count == 0)
        {
            return;
        }
        var json = AppConfig.ItemsToJson(items);
        var data = new DataObject();
        data.SetData(ItemsClipboardFormat, json);
        data.SetText(json);
        Clipboard.SetDataObject(data, copy: true);
        _status.Text = $"{items.Count} Zeile(n) kopiert – mit Strg+V in einem anderen Profil oder Editorfenster einfügen.";
    }

    private void PasteItems()
    {
        var json = Clipboard.GetData(ItemsClipboardFormat) as string ?? (Clipboard.ContainsText() ? Clipboard.GetText() : null);
        var items = json == null ? null : AppConfig.ItemsFromJson(json);
        if (items is not { Count: > 0 })
        {
            _status.Text = "In der Zwischenablage stehen keine Zeilen des Panels.";
            return;
        }
        items.ForEach(item => item.AllProfiles = null);
        var index = SelectedItemIndices().DefaultIfEmpty(-1).Max() + 1;
        _config.Items.InsertRange(index, items);
        RefreshItems(index);
        Changed();
        _status.Text = $"{items.Count} Zeile(n) eingefügt.";
    }

    /// <summary>Kopiert die markierten Zeilen in ein oder mehrere andere Profile - an den Anfang oder ans Ende.</summary>
    private void CopyItemsToProfiles()
    {
        var items = ItemsToCopy();
        if (items.Count == 0)
        {
            _status.Text = "Zuerst Zeilen markieren (mehrere mit Strg bzw. Umschalt).";
            return;
        }
        if (AskTargetProfiles($"{items.Count} Zeile(n) in Profile kopieren", withPosition: true) is not { } choice)
        {
            return;
        }
        foreach (var target in choice.Targets)
        {
            var copies = AppConfig.ItemsFromJson(AppConfig.ItemsToJson(items))!;
            ChangeProfile(target, config =>
            {
                if (choice.AtStart)
                {
                    // Hinter die gemeinsamen Zeilen "oben", sonst landeten die Kopien davor.
                    var start = config.Items.TakeWhile(item => item.AllProfiles == SharedPosition.Top).Count();
                    config.Items.InsertRange(start, copies);
                }
                else
                {
                    var end = config.Items.Count - Enumerable.Reverse(config.Items).TakeWhile(item => item.AllProfiles == SharedPosition.Bottom).Count();
                    config.Items.InsertRange(end, copies);
                }
            });
        }
        _status.Text = $"{items.Count} Zeile(n) {(choice.AtStart ? "an den Anfang" : "ans Ende")} kopiert: {string.Join(", ", choice.Targets.Select(DescribeProfile))}.";
    }

    /// <summary>Kopiert Felder in ein oder mehrere andere Profile.</summary>
    private void CopyFieldsToProfiles(List<string> names)
    {
        if (names.Count == 0)
        {
            _status.Text = "Zuerst Felder in der Tabelle markieren.";
            return;
        }
        if (AskTargetProfiles($"{names.Count} Feld(er) in Profile kopieren", withPosition: false) is not { } choice)
        {
            return;
        }
        foreach (var target in choice.Targets)
        {
            CopyFieldsToProfile(target, names);
        }
    }

    /// <summary>Auswahl der Zielprofile (Standardprofil und alle ausser dem bearbeiteten), fuer Zeilen auch Anfang oder Ende.</summary>
    private (List<string?> Targets, bool AtStart)? AskTargetProfiles(string title, bool withPosition)
    {
        var candidates = new List<string?> { null };
        candidates.AddRange(_root.Profiles?.Keys ?? Enumerable.Empty<string>());
        candidates.RemoveAll(t => string.Equals(t, _profile, StringComparison.OrdinalIgnoreCase));
        if (candidates.Count == 0)
        {
            _status.Text = "Es gibt kein weiteres Profil.";
            return null;
        }
        using var dialog = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Font = Font
        };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "Kopieren nach:", AutoSize = true });
        var list = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Width = LogicalToDeviceUnits(320) };
        foreach (var candidate in candidates)
        {
            list.Items.Add(candidate ?? StandardProfileName, candidates.Count == 1);
        }
        list.Height = Math.Min(candidates.Count, 10) * list.ItemHeight + LogicalToDeviceUnits(6);
        layout.Controls.Add(list);
        var position = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = LogicalToDeviceUnits(320), Visible = withPosition, Margin = new Padding(3, 10, 3, 3) };
        position.Items.AddRange(new object[] { "Ans Ende der Zeilen (vor den gemeinsamen unten)", "An den Anfang (nach den gemeinsamen oben)" });
        position.SelectedIndex = 0;
        layout.Controls.Add(position);
        var accept = new Button { Text = "Kopieren", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
        var footer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        footer.Controls.Add(cancel);
        footer.Controls.Add(accept);
        layout.Controls.Add(footer);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;
        while (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var targets = list.CheckedIndices.Cast<int>().Select(i => candidates[i]).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show(dialog, "Bitte mindestens ein Profil anhaken.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                continue;
            }
            return (targets, position.SelectedIndex == 1);
        }
        return null;
    }

    // ------------------------------------------------------------------ Zeilen fuer alle Profile

    /// <summary>Im Profil: eine Zeile, die aus dem Standardprofil "fuer alle Profile" kommt - nur dort zu aendern.</summary>
    private bool IsLockedItem(int index) =>
        _profile != null && index >= 0 && index < _config.Items.Count && _config.Items[index].AllProfiles != null;

    private string SharedSuffix(ItemConfig item) => item.AllProfiles switch
    {
        null => "",
        _ when _profile != null => "      [aus dem Standardprofil]",
        SharedPosition.Top => "      [für alle Profile: oben]",
        _ => "      [für alle Profile: unten]"
    };

    /// <summary>Zeilenliste selbst zeichnen: im Profil die gemeinsamen Zeilen grau.</summary>
    private void SetUpSharedItems()
    {
        _items.DrawMode = DrawMode.OwnerDrawFixed;
        _items.ItemHeight = Math.Max(_items.ItemHeight, _items.Font.Height + LogicalToDeviceUnits(2));
        _items.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || e.Index >= _items.Items.Count)
            {
                return;
            }
            e.DrawBackground();
            var selected = (e.State & DrawItemState.Selected) != 0;
            var color = selected ? SystemColors.HighlightText : IsLockedItem(e.Index) ? SystemColors.GrayText : _items.ForeColor;
            TextRenderer.DrawText(e.Graphics, _items.Items[e.Index]?.ToString(), e.Font ?? _items.Font, e.Bounds, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            e.DrawFocusRectangle();
        };
    }

    /// <summary>Im Profil: gemeinsame Zeilen sperren und sagen, warum.</summary>
    private void ShowSharedState(ItemConfig? item)
    {
        if (!IsLockedItem(_items.SelectedIndex))
        {
            return;
        }
        foreach (var control in new Control[] { _itemLabel, _itemValue, _itemSplit, _itemBar, _itemHide, _itemSeparator, _itemOnlyOn, _itemNotOn, _itemWhen })
        {
            control.Enabled = false;
        }
        _status.Text = $"Diese Zeile steht im Standardprofil „für alle Profile ({(item!.AllProfiles == SharedPosition.Top ? "oben" : "unten")})“ – ändern lässt sie sich nur dort.";
    }

    /// <summary>Markierte Zeilen des Standardprofils fuer alle Profile oben bzw. unten setzen - oder die Markierung entfernen.</summary>
    private void SetShared(SharedPosition? position)
    {
        if (_profile != null)
        {
            _status.Text = "Zeilen für alle Profile werden im Standardprofil festgelegt – oben in der Leiste „Standardprofil“ wählen.";
            return;
        }
        var indices = SelectedItemsWithBlocks();
        if (indices.Count == 0)
        {
            _status.Text = "Zuerst Zeilen markieren (mehrere mit Strg bzw. Umschalt).";
            return;
        }
        foreach (var index in indices)
        {
            _config.Items[index].AllProfiles = position;
        }
        RefreshItems(indices[0]);
        Changed();
        _status.Text = position == null
            ? $"{indices.Count} Zeile(n) gelten nicht mehr für alle Profile."
            : $"{indices.Count} Zeile(n) stehen jetzt in jedem Profil mit eigenen Zeilen {(position == SharedPosition.Top ? "oben" : "unten")}.";
    }

    /// <summary>Uebertraegt Felder (Name, Value, Bezeichnung) in ein anderes Profil; bei gleichem Namen wird gefragt.</summary>
    private void CopyFieldsToProfile(string? target, List<string> names)
    {
        if (names.Count == 0)
        {
            _status.Text = "Zuerst Felder in der Tabelle markieren.";
            return;
        }
        var targetFields = (target == null ? _root : _root.WithProfile(target) ?? _root).Fields;
        var clashes = names.Where(n => targetFields.TryGetValue(n, out var value) && value != _config.Fields[n]).ToList();
        var replace = true;
        if (clashes.Count > 0)
        {
            var answer = MessageBox.Show(this,
                $"In {DescribeProfile(target)} gibt es diese Felder schon mit anderem Wert: {string.Join(", ", clashes)}\r\n\r\nJa\t\t= ersetzen\r\nNein\t\t= überspringen\r\nAbbrechen\t= nichts kopieren",
                AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
            {
                return;
            }
            replace = answer == DialogResult.Yes;
        }
        var copied = 0;
        ChangeProfile(target, config =>
        {
            foreach (var name in names)
            {
                if (!replace && clashes.Contains(name))
                {
                    continue;
                }
                config.Fields[name] = _config.Fields[name];
                if (_config.Fields.Labels.TryGetValue(name, out var label))
                {
                    config.Fields.Labels[name] = label;
                }
                else
                {
                    config.Fields.Labels.Remove(name);
                }
                copied++;
            }
        });
        _status.Text = $"{copied} Feld(er) nach {DescribeProfile(target)} kopiert.";
    }

    private static string DescribeProfile(string? name) => name == null ? "das Standardprofil" : $"Profil „{name}“";

    /// <summary>Aendert ein anderes Profil (oder das Standardprofil) als das gerade bearbeitete.</summary>
    private void ChangeProfile(string? target, Action<AppConfig> change)
    {
        CommitProfile();
        if (target == null)
        {
            change(_root);
            // Das bearbeitete Profil erbt die Aenderung, soweit es dort nicht abweicht.
            RebuildProfile();
        }
        else
        {
            var key = _root.FindProfile(target)!;
            var config = _root.WithProfile(key)!;
            change(config);
            _root.StoreProfile(key, config);
        }
        Changed();
    }

}
