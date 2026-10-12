using System.Diagnostics;
using System.Drawing.Text;

namespace BgInfoReloaded.Editor;

internal static class EditorProgram
{
    [STAThread]
    private static void Main(string[] args)
    {
        // PerMonitorV2 macht zugleich den Prozess DPI-aware - Voraussetzung dafuer, dass die Vorschau
        // in derselben (echten) Aufloesung rendert wie BGInfo-Reloaded.exe.
        ValueFormat.PinInvariantCulture();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Ohne Dateiangabe dieselbe BGInfo-Reloaded.json oeffnen, die auch BGInfo-Reloaded.exe benutzen wuerde.
        Application.Run(new MainForm(args.FirstOrDefault() ?? AppConfig.FindFileForEditor()));
    }
}

/// <summary>
/// Editor fuer BGInfo-Reloaded.json, angelehnt an das Fenster des Original-BGInfo: links die Zeilen des Panels,
/// daneben die Platzhalter zum Einfuegen, rechts eine Vorschau mit dem echten Renderer.
/// Alle Eingabefelder haengen ueber kleine get/set-Funktionen direkt am <see cref="AppConfig"/>-Objekt;
/// gespeichert wird mit derselben Serialisierung, die auch --export-config benutzt.
/// </summary>
public sealed class MainForm : Form
{
    private const string AppTitle = "BGInfo-Reloaded Editor";

    private AppConfig _config = new();
    private string? _path;
    private bool _dirty;
    private bool _loading;
    private bool _fileHadComments;
    private int _previewGeneration;

    // Jedes gebundene Eingabefeld traegt hier ein, wie es seinen Wert aus _config nachlaedt.
    private readonly List<Action> _loaders = new();
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 500 };

    private readonly ListBox _items = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _labelCaption = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _valueCaption = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _itemLabel = new() { Dock = DockStyle.Fill };
    private readonly TextBox _itemValue = new() { Dock = DockStyle.Fill };
    private readonly TextBox _itemSplit = new() { Dock = DockStyle.Fill, PlaceholderText = "leer = nicht teilen; z. B.  ,   oder mehrere mit | getrennt:  ,|;" };
    private readonly TextBox _itemCurrent = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false, BackColor = SystemColors.Control };
    private readonly System.Windows.Forms.Timer _currentTimer = new() { Interval = 350 };
    private int _currentGeneration;
    private bool _fillingFieldValues;
    private readonly CheckBox _itemHide = new() { AutoSize = true, Text = "Zeile weglassen, wenn der Wert leer oder n/a ist" };
    private readonly TextBox _itemOnlyOn = new() { Dock = DockStyle.Fill, PlaceholderText = "leer = überall; Muster mit Komma trennen, z. B. SRV-*, *-DC??" };
    private readonly TextBox _itemNotOn = new() { Dock = DockStyle.Fill, PlaceholderText = "Muster mit Komma trennen, z. B. KIOSK-*" };
    private readonly TextBox _itemWhen = new() { Dock = DockStyle.Fill, PlaceholderText = "z. B. {systemtype} = Server   oder   {env:STANDORT} != Berlin" };
    private readonly DataGridView _fields = new() { Dock = DockStyle.Fill, AllowUserToResizeRows = false, RowHeadersWidth = 30, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle };
    private bool _tagsStale;
    private readonly TextBox _tagSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "in Platzhalter, Bedeutung und Wert suchen …" };
    // Alle Platzhalter in Listenreihenfolge; angezeigt werden die, die zur Suche passen.
    private readonly List<ListViewItem> _allTags = new();
    private readonly ListView _tags = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(40, 40, 40) };
    private readonly PictureBox _detail = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.CenterImage, BackColor = Color.FromArgb(40, 40, 40) };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _fileLabel = new() { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(6, 0, 6, 0) };
    private readonly ToolStripStatusLabel _versionLabel = new() { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(6, 0, 2, 0) };
    private readonly NumericUpDown _taskInterval = new() { Minimum = 0, Maximum = 1440, Value = 30, Width = 110 };
    private readonly CheckBox _taskWatch = new() { AutoSize = true, Text = "Zusätzlich auf Wechsel von Auflösung und Skalierung reagieren (--watch)" };
    private readonly CheckBox _taskUseConfig = new() { AutoSize = true, Checked = true, Text = "Die geöffnete Konfigurationsdatei im Task verwenden (--config)" };
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill };
    private readonly ToolTip _tips = new() { AutoPopDelay = 30000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly Label _previewCaption = new() { AutoSize = true, Margin = new Padding(3, 3, 3, 4) };
    // Rechts neben der Ueberschrift der Vorschau, in Orange: dieser Rechner ist laut White-/Blacklist ausgeschlossen.
    private readonly Label _previewHostNote = new()
    {
        AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true,
        ForeColor = Color.FromArgb(214, 108, 0), Margin = new Padding(12, 3, 3, 4)
    };
    // Erkannte Aufloesung und Skalierung des primaeren Monitors, fuer Titelleiste und Vorschau-Ueberschrift.
    private string _displayInfo = "";
    private TextBox _lastItemTextBox;

    public MainForm(string? fileToOpen)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont ?? Font;
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 900);
        Size = new Size(Math.Min(workArea.Width * 9 / 10, LogicalToDeviceUnits(1900)), workArea.Height * 85 / 100);
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterScreen;
        try
        {
            // Fenster- und Taskleisten-Icon aus der eigenen exe (dort ueber ApplicationIcon eingebettet).
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        }
        catch
        {
            // ohne Icon geht es auch
        }
        _lastItemTextBox = _itemValue;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildItemsTab());
        tabs.TabPages.Add(BuildAppearanceTab());
        tabs.TabPages.Add(BuildBackgroundTab());
        tabs.TabPages.Add(BuildHostsTab());
        tabs.TabPages.Add(BuildFieldsTab());
        tabs.TabPages.Add(BuildToolsTab());
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedIndex == 0 && _tagsStale)
            {
                LoadTags();
            }
        };

        // Rechts oben der ganze Bildschirm, darunter das Panel in Originalgroesse - auf breiten oder
        // hochaufloesenden Monitoren waere es in der Gesamtansicht sonst nicht lesbar.
        var previews = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(6) };
        previews.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previews.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        previews.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previews.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        var captionRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        captionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        captionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        captionRow.Controls.Add(_previewCaption);
        captionRow.Controls.Add(_previewHostNote);
        _previewHostNote.Font = new Font(Font, FontStyle.Bold);
        previews.Controls.Add(captionRow);
        previews.Controls.Add(_preview);
        previews.Controls.Add(new Label { Text = "Info-Panel in Originalgröße", AutoSize = true, Margin = new Padding(3, 10, 3, 4) });
        previews.Controls.Add(_detail);
        _detail.Resize += (_, _) => FitDetail();

        _split.Panel1.Controls.Add(tabs);
        _split.Panel2.Controls.Add(previews);

        // Fusszeile: links Meldungen, rechts die geladene Datei und die Programmversion.
        var status = new StatusStrip { ShowItemToolTips = true };
        status.Items.Add(_status);
        status.Items.Add(_fileLabel);
        status.Items.Add(_versionLabel);
        _versionLabel.Text = $"BGInfo-Reloaded {GitHubUpdater.CurrentVersion.ToString(3)}";
        _versionLabel.ToolTipText = "Version von Editor und BGInfo-Reloaded.exe aus diesem Build";

        // Reihenfolge zaehlt: das fuellende Element zuerst, die Leisten danach.
        Controls.Add(_split);
        Controls.Add(BuildToolbar());
        Controls.Add(status);

        _previewTimer.Tick += (_, _) => RenderPreview();
        _currentTimer.Tick += (_, _) => UpdateCurrentValues();

        // Aendert sich Aufloesung oder Skalierung, waehrend der Editor offen ist (RDP-Fenster, Docking),
        // Anzeige und Vorschau nachziehen. SystemEvents meldet auf einem eigenen Thread.
        RefreshDisplayInfo();
        void OnDisplayChanged(object? sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(() =>
                {
                    RefreshDisplayInfo();
                    UpdateTitle();
                    SchedulePreview();
                });
            }
        }
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        DpiChanged += (s, e) => OnDisplayChanged(s, e);
        FormClosed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;

        Load += (_, _) =>
        {
            _split.SplitterDistance = Math.Min(_split.Width / 2, LogicalToDeviceUnits(860));
            _split.Panel2Collapsed = !_previewEnabled;
            if (fileToOpen != null && File.Exists(fileToOpen))
            {
                OpenFile(fileToOpen);
                BeginInvoke(CheckUpdateFolderAtStart);
            }
            else
            {
                ShowConfig();
                // Erst nach dem Aufbau des Fensters fragen, damit der Dialog davor steht.
                BeginInvoke(() => AskForStartConfiguration(fileToOpen));
            }
        };
        FormClosing += (_, e) => e.Cancel = !_restarting && !ConfirmDiscard();
    }

    // ------------------------------------------------------------------ Aufbau der Oberflaeche

    private ToolStrip BuildToolbar()
    {
        var iconSize = LogicalToDeviceUnits(20);
        var bar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(6, 4, 6, 4),
            ImageScalingSize = new Size(iconSize, iconSize),
            RenderMode = ToolStripRenderMode.System
        };
        ToolStripButton Button(string text, char glyph, string tip, Action action)
        {
            var button = new ToolStripButton(text)
            {
                Image = GlyphIcon(glyph, iconSize),
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ToolTipText = tip,
                Padding = new Padding(4, 2, 4, 2),
                Margin = new Padding(1, 1, 4, 1)
            };
            button.Click += (_, _) => action();
            return button;
        }

        // Zeichen aus der Symbolschrift von Windows (Segoe Fluent Icons bzw. Segoe MDL2 Assets).
        bar.Items.Add(Button("Neu", '\uE7C3', "Mit den eingebauten Standardwerten beginnen", NewConfig));
        bar.Items.Add(Button("Öffnen…", '\uE8E5', "Eine BGInfo-Reloaded.json laden", () =>
        {
            using var dialog = new OpenFileDialog { Filter = "BgInfo-Konfiguration (*.json)|*.json|Alle Dateien|*.*" };
            if (ConfirmDiscard() && dialog.ShowDialog(this) == DialogResult.OK)
            {
                OpenFile(dialog.FileName);
            }
        }));
        bar.Items.Add(Button("Speichern", '\uE74E', "In die geöffnete Datei speichern", () => Save(false)));
        bar.Items.Add(Button("Speichern unter…", '\uE792', "In eine neue Datei speichern", () => Save(true)));
        bar.Items.Add(new ToolStripSeparator());
        var previewToggle = Button("Vorschau", '\uE7B3', "Vorschaubereich rechts ein- oder ausblenden (ausgeblendet wird auch nicht gerendert)", () => { });
        previewToggle.CheckOnClick = true;
        previewToggle.Checked = _previewEnabled;
        previewToggle.CheckedChanged += (_, _) => SetPreviewEnabled(previewToggle.Checked);
        bar.Items.Add(previewToggle);
        // "100 %": das Panel so zeichnen wie ohne Windows-Skalierung - schaltet dafuer die beiden
        // Einstellungen zusammen, die es sonst vergroessern (Skalierung mit der DPI, relative Breite).
        _scaleToggle = Button("100 %", '\uE8A3', "Panel wie bei 100 % zeichnen: Windows-Skalierung und relative Breite gelten dann nicht.\r\nEntspricht im Reiter „Aussehen“: „Größe mit der Windows-Skalierung vergrößern“ aus, „Relative Breite“ aus.\r\nAusschalten stellt die vorherigen Werte wieder her.", () => { });
        _scaleToggle.CheckOnClick = true;
        _scaleToggle.CheckedChanged += (_, _) => SetUnscaled(_scaleToggle.Checked);
        bar.Items.Add(_scaleToggle);
        bar.Items.Add(Button("Vorschau aktualisieren", '\uE72C', "Werte neu abfragen und Vorschau neu rendern", RenderPreview));
        bar.Items.Add(Button("Vorschau anzeigen", '\uE740', "Die aktuelle Darstellung als Vollbild zeigen – so, wie sie auf dem Desktop aussieht (Esc oder Klick schließt)", ShowFullscreenPreview));
        bar.Items.Add(Button("Jetzt anwenden", '\uE768', "Speichern und BGInfo-Reloaded.exe mit dieser Konfiguration ausführen", Apply));
        return bar;
    }

    /// <summary>
    /// Zeichnet ein Symbol aus der Windows-Symbolschrift in ein Bild. Fehlen beide Schriften (aeltere
    /// oder abgespeckte Systeme), bleibt das Bild leer und die Schaltflaeche zeigt nur ihren Text.
    /// </summary>
    private static Bitmap GlyphIcon(char glyph, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var installed = new InstalledFontCollection();
        var family = new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }
            .Select(name => installed.Families.FirstOrDefault(f => f.Name == name))
            .FirstOrDefault(f => f != null);
        if (family == null)
        {
            return bitmap;
        }

        using var graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font(family, size * 0.8f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(SystemColors.ControlText);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(glyph.ToString(), font, brush, new RectangleF(0, 0, size, size), format);
        return bitmap;
    }

    private TabPage BuildItemsTab()
    {
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        void Add(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(4, 2, 4, 2) };
            button.Click += (_, _) => action();
            buttons.Controls.Add(button);
        }

        Add("+ Überschrift", () => InsertItem(new ItemConfig { Section = "ÜBERSCHRIFT" }));
        Add("+ Zeile", () => InsertItem(new ItemConfig { Label = "Bezeichnung", Value = "" }));
        Add("+ Leerraum", () => InsertItem(new ItemConfig { Spacer = true }));
        Add("+ Laufwerke", () => InsertItem(new ItemConfig { Drives = true }));
        Add("+ Zentrale Zeilen", () => InsertItem(new ItemConfig { IncludeItems = true }));
        Add("+ Block", () =>
        {
            // Anfang und Ende zusammen einfuegen; die Bedingung kommt in die Filterfelder des Anfangs.
            var index = _items.SelectedIndex + 1;
            _config.Items.Insert(index, new ItemConfig { Block = "Block" });
            _config.Items.Insert(index + 1, new ItemConfig { EndBlock = true });
            RefreshItems(index);
            Changed();
            _itemWhen.Focus();
        });
        Add("Entfernen", RemoveItem);
        Add("▲", () => MoveItem(-1));
        Add("▼", () => MoveItem(1));

        var detail = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _labelCaption.Margin = _valueCaption.Margin = new Padding(3, 6, 10, 6);
        detail.Controls.Add(_labelCaption);
        detail.Controls.Add(_itemLabel);
        detail.Controls.Add(_valueCaption);
        detail.Controls.Add(_itemValue);
        detail.Controls.Add(new Label { Text = "Trennen bei", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 6) });
        detail.Controls.Add(_itemSplit);
        _tips.SetToolTip(_itemSplit, "Teilt den Wert an diesen Zeichen: der erste Teil steht neben der Bezeichnung,\r\njeder weitere in einer eigenen Zeile darunter – z. B. bei mehreren Grafikkarten.\r\nMehrere Trennzeichen mit | trennen. Leerzeichen um die Teile entfallen.");
        _itemSplit.TextChanged += (_, _) => EditSelectedItem();
        // Was "Value" auf diesem Rechner gerade ergibt - nur Anzeige, wird nicht gespeichert.
        detail.Controls.Add(new Label { Text = "Aktueller Wert", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 6) });
        detail.Controls.Add(_itemCurrent);
        _tips.SetToolTip(_itemCurrent, "Das Ergebnis von „Value“ auf diesem Rechner, mit aufgelösten Platzhaltern.\r\nNur zur Ansicht – auf anderen Rechnern steht hier deren Wert.");
        detail.Controls.Add(new Label { AutoSize = true });
        detail.Controls.Add(_itemHide);
        foreach (var (caption, box) in new[] { ("Nur auf Rechnern", _itemOnlyOn), ("Nicht auf Rechnern", _itemNotOn), ("Nur wenn", _itemWhen) })
        {
            detail.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 6) });
            detail.Controls.Add(box);
            box.TextChanged += (_, _) => EditSelectedItem();
        }

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(buttons);
        left.Controls.Add(_items);
        left.Controls.Add(detail);

        _tags.Columns.Add("Platzhalter", 150);
        _tags.Columns.Add("Bedeutung", 210);
        _tags.Columns.Add("Wert auf diesem Rechner", 260);
        var tagBox = new GroupBox { Text = "Platzhalter", Dock = DockStyle.Fill, Padding = new Padding(8) };
        // Der Bedienhinweis steht als eigene, umbrechende Zeile ueber der Liste - im Rahmentitel wurde er abgeschnitten.
        var tagHint = new Label
        {
            Text = "Doppelklick fügt in die markierte Zeile ein. Ziehen auf die Zeilenliste legt dort eine neue Zeile an, Ziehen auf ein Textfeld fügt dort ein.",
            Dock = DockStyle.Top,
            AutoSize = false,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(0, 0, 0, 6)
        };
        void FitTagHint() => tagHint.Height = TextRenderer.MeasureText(tagHint.Text, tagHint.Font, new Size(Math.Max(100, tagHint.Width), 0), TextFormatFlags.WordBreak).Height + tagHint.Padding.Vertical;
        tagHint.Resize += (_, _) => FitTagHint();

        // Suche unter der WMI-Abfrage: filtert die Liste beim Tippen ueber alle drei Spalten.
        _tagSearch.TextChanged += (_, _) => ApplyTagFilter();
        _tagSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                _tagSearch.Clear();
                e.SuppressKeyPress = true;
            }
        };
        var clearSearch = new Button { Text = "✕", Dock = DockStyle.Right, Width = LogicalToDeviceUnits(32), FlatStyle = FlatStyle.System };
        clearSearch.Click += (_, _) => { _tagSearch.Clear(); _tagSearch.Focus(); };
        _tips.SetToolTip(clearSearch, "Suche leeren (Esc)");
        var searchLabel = new Label { Text = "Suchen", Dock = DockStyle.Left, AutoSize = true, Padding = new Padding(0, 5, 8, 0) };
        var searchRow = new Panel { Dock = DockStyle.Bottom, Height = _tagSearch.PreferredHeight + LogicalToDeviceUnits(8), Padding = new Padding(0, LogicalToDeviceUnits(6), 0, 0) };
        searchRow.Controls.Add(_tagSearch);
        searchRow.Controls.Add(clearSearch);
        searchRow.Controls.Add(searchLabel);

        // Spaltenkoepfe hervorheben: fett auf abgesetztem Grund.
        _tags.OwnerDraw = true;
        var headerFont = new Font(Font, FontStyle.Bold);
        _tags.DrawColumnHeader += (_, e) =>
        {
            using var back = new SolidBrush(Color.FromArgb(218, 227, 240));
            e.Graphics.FillRectangle(back, e.Bounds);
            e.Graphics.DrawLine(SystemPens.ControlDark, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(SystemPens.ControlLightLight, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, headerFont, Rectangle.Inflate(e.Bounds, -6, 0), Color.FromArgb(20, 40, 80),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        _tags.DrawItem += (_, e) => e.DrawDefault = true;
        _tags.DrawSubItem += (_, e) => e.DrawDefault = true;
        var wmi = new Button { Text = "WMI-Abfrage…", AutoSize = true, Dock = DockStyle.Bottom };
        wmi.Click += (_, _) =>
        {
            using var dialog = new WmiDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                InsertAtCaret(dialog.Placeholder);
            }
        };
        // Reihenfolge zaehlt: das fuellende Element zuerst.
        tagBox.Controls.Add(_tags);
        tagBox.Controls.Add(wmi);
        tagBox.Controls.Add(searchRow);
        tagBox.Controls.Add(tagHint);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(left);
        root.Controls.Add(tagBox);

        // Nicht waehrend _loading: beim Ersetzen eines Listeneintrags (EditSelectedItem) meldet die
        // ListBox kurz "nichts ausgewaehlt" - das wuerde die Textfelder deaktivieren, und das Feld, in
        // dem gerade getippt wird, verloere nach dem ersten Zeichen den Fokus.
        _items.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                ShowSelectedItem();
            }
        };
        _itemLabel.Enter += (_, _) => _lastItemTextBox = _itemLabel;
        _itemValue.Enter += (_, _) => _lastItemTextBox = _itemValue;
        _itemWhen.Enter += (_, _) => _lastItemTextBox = _itemWhen;
        _itemLabel.TextChanged += (_, _) => EditSelectedItem();
        _itemValue.TextChanged += (_, _) => EditSelectedItem();
        _itemHide.CheckedChanged += (_, _) => EditSelectedItem();
        _tags.DoubleClick += (_, _) =>
        {
            if (_tags.SelectedItems.Count > 0)
            {
                var chosen = _tags.SelectedItems[0];
                InsertPlaceholder(chosen.Text, chosen.SubItems.Count > 1 ? chosen.SubItems[1].Text : "");
            }
        };
        SetUpPlaceholderDragDrop();

        var page = new TabPage("Inhalt");
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildAppearanceTab()
    {
        var table = NewFormTable();

        Heading(table, "Lage und Größe");
        Row(table, "Position", Combo(new[]
        {
            (PanelPosition.TopLeft, "Oben links"), (PanelPosition.TopCenter, "Oben Mitte"), (PanelPosition.TopRight, "Oben rechts"),
            (PanelPosition.MiddleLeft, "Mitte links"), (PanelPosition.Center, "Mitte"), (PanelPosition.MiddleRight, "Mitte rechts"),
            (PanelPosition.BottomLeft, "Unten links"), (PanelPosition.BottomCenter, "Unten Mitte"), (PanelPosition.BottomRight, "Unten rechts")
        }, () => _config.Panel.Position, v => _config.Panel.Position = v));
        Row(table, "Abstand zum Bildschirmrand", Number(0, 2000, () => _config.Panel.Margin, v => _config.Panel.Margin = (int)v));
        Row(table, "Innenabstand", Number(0, 500, () => _config.Panel.Padding, v => _config.Panel.Padding = (int)v));
        Row(table, "", Check("Größe mit der Windows-Skalierung des Monitors vergrößern", () => _config.Panel.ScaleWithDpi, v => _config.Panel.ScaleWithDpi = v));

        Heading(table, "Breite des Panels");
        // Die feste Breite gilt nur ohne relative Breite - das Feld wird sonst gesperrt, damit das sichtbar ist.
        var fixedWidth = Number(0, 10000, () => _config.Panel.Width, v => _config.Panel.Width = (int)v);
        var overflow = Combo(new[]
        {
            (TextOverflow.Truncate, "Abschneiden – der Text endet mit …"),
            (TextOverflow.Wrap, "Umbrechen – der Text läuft in der Spalte in die nächste Zeile")
        }, () => _config.Panel.TextOverflow, v => _config.Panel.TextOverflow = v);
        void SyncWidthFields()
        {
            fixedWidth.Enabled = overflow.Enabled = _config.Panel.ScreenFraction <= 0;
        }
        _loaders.Add(SyncWidthFields);
        Row(table, "Relative Breite", Combo(new[]
        {
            (0, "Aus – Breite nach Inhalt bzw. feste Breite unten"),
            (3, "1/3 der Bildschirmbreite"), (4, "1/4 der Bildschirmbreite"), (5, "1/5 der Bildschirmbreite"),
            (6, "1/6 der Bildschirmbreite"), (7, "1/7 der Bildschirmbreite"), (8, "1/8 der Bildschirmbreite")
        }, () => _config.Panel.ScreenFraction, v =>
        {
            _config.Panel.ScreenFraction = v;
            SyncWidthFields();
        }));
        Row(table, "", Hint("Mit relativer Breite werden Schrift, Abstände, Ecken und Schatten so skaliert, dass der Inhalt genau\r\ndiese Breite füllt – das Panel wirkt dann auf jedem Monitor gleich groß. Die Schriftgrößen weiter\r\nunten bestimmen dann nur noch das Verhältnis von Überschrift zu Zeile."));
        Row(table, "Feste Breite in Pixeln (0 = nach Inhalt)", fixedWidth);
        Row(table, "Texte, die nicht hineinpassen", overflow);
        Row(table, "", Hint("Die feste Breite und diese Auswahl gelten nur ohne relative Breite – dort wird der Inhalt passend skaliert.\r\nBeim Umbrechen wird die betroffene Zeile des Panels höher; das gilt auch für eine schmal eingestellte Spalte."));

        Heading(table, "Spalten: Bezeichnung und Wert");
        Row(table, "Anteil der Bezeichnungsspalte", Slider(0, 90, 5,
            v => v == 0 ? "automatisch (längste Bezeichnung)" : $"{Math.Max(v, 10)} % Bezeichnung, {100 - Math.Max(v, 10)} % Wert",
            () => _config.Panel.LabelShare, v => _config.Panel.LabelShare = v));
        Row(table, "Abstand zwischen den Spalten", Number(0, 500, () => _config.Panel.ColumnGap, v => _config.Panel.ColumnGap = (int)v));
        Row(table, "Bezeichnungsspalte in Pixeln (0 = automatisch)", Number(0, 5000, () => _config.Panel.LabelWidth, v => _config.Panel.LabelWidth = (int)v));
        Row(table, "", Hint("Der Anteil hat Vorrang vor dem Pixelwert; der Abstand wirkt bei automatischer Spalte.\r\nWas nicht in seine Spalte passt, wird mit „…“ gekürzt."));

        Heading(table, "Taskleiste");
        Row(table, "", Check("Abstand ab der Taskleiste messen – das Panel beginnt über bzw. neben ihr statt dahinter", () => _config.Panel.AvoidTaskbar, v => _config.Panel.AvoidTaskbar = v));
        Row(table, "", Hint("Der Abstand selbst ist „Abstand zum Bildschirmrand“ oben. Ohne Haken zählt er ab der Monitorkante."));

        Heading(table, "Hintergrund des Panels");
        Row(table, "Runde Ecken – Radius (0 = eckig)", Number(0, 500, () => _config.Panel.CornerRadius, v => _config.Panel.CornerRadius = (int)v));
        Row(table, "Schatten – Größe (0 = ohne)", Number(0, 500, () => _config.Panel.ShadowSize, v => _config.Panel.ShadowSize = (int)v));
        Row(table, "Schatten – Farbe", ColorButton(() => _config.Panel.ShadowColor, v => _config.Panel.ShadowColor = v));
        Row(table, "Schatten – Deckkraft (0–255)", Number(0, 255,
            () => SafeColor(_config.Panel.ShadowColor).A,
            v =>
            {
                _config.Panel.ShadowColor = ToHex(SafeColor(_config.Panel.ShadowColor), (int)v);
                ReloadControls();
            }));
        Row(table, "Farbe des Panels", ColorButton(() => _config.Panel.Background, v => _config.Panel.Background = v));
        Row(table, "Deckkraft des Panels (0–255)", Number(0, 255,
            () => SafeColor(_config.Panel.Background).A,
            v =>
            {
                _config.Panel.Background = ToHex(SafeColor(_config.Panel.Background), (int)v);
                ReloadControls();
            }));

        Heading(table, "Schrift");
        var family = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDown };
        using (var installed = new InstalledFontCollection())
        {
            family.Items.AddRange(installed.Families.Select(f => (object)f.Name).ToArray());
        }
        _loaders.Add(() => family.Text = _config.Font.Family);
        family.TextChanged += (_, _) =>
        {
            if (!_loading)
            {
                _config.Font.Family = family.Text;
                Changed();
            }
        };
        Row(table, "Schriftart", family);
        Row(table, "Schriftgröße", Number(4, 200, () => (decimal)_config.Font.Size, v => _config.Font.Size = (float)v, 1));
        Row(table, "Schriftgröße der Überschriften", Number(4, 200, () => (decimal)_config.Font.HeaderSize, v => _config.Font.HeaderSize = (float)v, 1));

        Heading(table, "Textfarben");
        Row(table, "Überschriften", ColorButton(() => _config.Colors.Header, v => _config.Colors.Header = v));
        Row(table, "Bezeichnungen", ColorButton(() => _config.Colors.Label, v => _config.Colors.Label = v));
        Row(table, "Werte", ColorButton(() => _config.Colors.Value, v => _config.Colors.Value = v));

        Heading(table, "Rahmen");
        Row(table, "Rahmen um", Combo(new[]
        {
            (BorderScope.Panel, "Das ganze Panel"),
            (BorderScope.Sections, "Jeden Abschnitt – Überschrift mit ihren Zeilen"),
            (BorderScope.Rows, "Jede Zeile – Gitter mit Trennlinie zwischen Bezeichnung und Wert")
        }, () => _config.Border.Scope, v => _config.Border.Scope = v));
        Row(table, "Strichstärke (0 = kein Rahmen)", Number(0, 40, () => _config.Border.Width, v => _config.Border.Width = (int)v));
        Row(table, "Art", Combo(new[]
        {
            (BorderLine.Solid, "Linie"),
            (BorderLine.Dotted, "Punkte"),
            (BorderLine.Dashed, "Strichliert")
        }, () => _config.Border.Style, v => _config.Border.Style = v));
        Row(table, "Farbe", ColorButton(() => _config.Border.Color, v => _config.Border.Color = v));
        Row(table, "", Hint("Der Rahmen liegt im vorhandenen Innen- und Zeilenabstand – Größe und Lage des Panels ändern sich nicht.\r\nFür Kästen und Gitter gegebenenfalls den Innenabstand oben etwas vergrößern."));

        Heading(table, "Datum und Zahlen");
        var formatSample = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };
        void ShowFormatSample()
        {
            // Dieselbe Formatierung wie im Panel; ein ungueltiges Format faellt dort auf den Standard zurueck.
            ValueFormat.Current = _config.Format;
            var now = DateTime.Now;
            var unknownCulture = !string.IsNullOrWhiteSpace(_config.Format.Culture) && ValueFormat.ResolveCulture(_config.Format.Culture) == null;
            formatSample.Text = $"Beispiel:  {ValueFormat.FormatDate(now)}   ·   {ValueFormat.FormatDateTime(now)}   ·   {ValueFormat.FormatNumber(1234.5678)} GB"
                + (unknownCulture ? $"\r\nSprache „{_config.Format.Culture}“ ist unbekannt – es gilt die neutrale Schreibweise." : "");
        }
        // Auswahlfeld mit Vorschlaegen, frei ueberschreibbar.
        ComboBox FormatCombo(string[] suggestions, Func<string> get, Action<string> set)
        {
            var combo = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDown };
            combo.Items.AddRange(suggestions);
            _loaders.Add(() => combo.Text = get());
            combo.TextChanged += (_, _) =>
            {
                if (!_loading)
                {
                    set(combo.Text.Trim());
                    Changed();
                }
                ShowFormatSample();
            };
            return combo;
        }
        Row(table, "Datum", FormatCombo(
            new[] { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "d. MMMM yyyy", "ddd, dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy" },
            () => _config.Format.Date, v => _config.Format.Date = v));
        Row(table, "Datum mit Uhrzeit", FormatCombo(
            new[] { "yyyy-MM-dd HH:mm", "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "d. MMMM yyyy, HH:mm", "ddd, dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm", "MM/dd/yyyy hh:mm tt" },
            () => _config.Format.DateTime, v => _config.Format.DateTime = v));
        var decimals = Number(0, 6, () => _config.Format.Decimals, v => _config.Format.Decimals = (int)v);
        decimals.ValueChanged += (_, _) => ShowFormatSample();
        Row(table, "Nachkommastellen (höchstens)", decimals);
        Row(table, "Sprache für Trennzeichen und Namen", FormatCombo(
            new[] { "", ValueFormat.SystemCultureName, "de-DE", "de-AT", "de-CH", "en-US", "en-GB", "fr-FR", "it-IT", "es-ES", "nl-NL" },
            () => _config.Format.Culture ?? "", v => _config.Format.Culture = v.Length == 0 ? null : v));
        Row(table, "", formatSample);
        Row(table, "", Hint("Formatangaben wie in .NET: d/dd Tag, M/MM Monat, MMMM Monatsname, yy/yyyy Jahr, ddd Wochentag, HH Stunde (24 h),\r\nhh Stunde (12 h) mit tt, mm Minute, ss Sekunde. Gilt für Werte wie {boot}, {now}, {biosdate}, {lastupdate}; ein eigenes\r\n{now:Format} in einer Zeile hat Vorrang. Sprache: leer = neutral (Punkt, englische Namen), system = wie das jeweilige Windows, sonst z. B. de-DE.\r\nNachkommastellen: {ram}, {netspeed}, Laufwerke."));
        _loaders.Add(ShowFormatSample);

        return ScrollPage("Aussehen", table);
    }

    private TabPage BuildBackgroundTab()
    {
        var table = NewFormTable();

        Heading(table, "Hintergrundbild");
        // Beim ersten Start uebernommenes Wallpaper: zeigen, woher die Kopie stammt (nur Anzeige).
        var original = new TextBox { Width = 420, ReadOnly = true, TabStop = false };
        void ShowOriginal()
        {
            var from = _config.Wallpaper.OriginalPath;
            original.Text = string.IsNullOrWhiteSpace(from) ? "–" : from;
            _tips.SetToolTip(original, string.IsNullOrWhiteSpace(from)
                ? "Das Bild wurde nicht vom gesetzten Wallpaper übernommen."
                : $"Beim ersten Start vom gesetzten Wallpaper übernommen:\r\n{from}");
        }
        _loaders.Add(ShowOriginal);
        var path = TextField(() => _config.Wallpaper.Path ?? "", v =>
        {
            _config.Wallpaper.Path = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            // Ein anderes Bild stammt nicht mehr vom uebernommenen Wallpaper.
            if (_config.Wallpaper.OriginalPath != null
                && !Path.GetFileName(_config.Wallpaper.Path ?? "").StartsWith(WallpaperState.AdoptedName, StringComparison.OrdinalIgnoreCase))
            {
                _config.Wallpaper.OriginalPath = null;
                ShowOriginal();
            }
        });
        path.Width = 420;
        var browse = new Button { Text = "…", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Bilder (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|Alle Dateien|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Liegt das Bild neben der Konfigurationsdatei, relativ eintragen - dann funktioniert
                // das Paar aus Datei und Bild in jedem Ordner, in den es verteilt wird.
                var sameFolder = _path != null && string.Equals(Path.GetDirectoryName(dialog.FileName), Path.GetDirectoryName(_path), StringComparison.OrdinalIgnoreCase);
                path.Text = sameFolder ? @".\" + Path.GetFileName(dialog.FileName) : dialog.FileName;
            }
        };
        var pathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        pathRow.Controls.Add(path);
        pathRow.Controls.Add(browse);
        Row(table, "Bilddatei", pathRow);
        Row(table, "Übernommen von (Originalpfad)", original);
        Row(table, "", Hint("Leer = das Wallpaper des jeweiligen Benutzers. Für die Verteilung einen Pfad angeben, der auf allen\r\nRechnern gilt: UNC-Share, %ProgramData%\\… oder relativ zur Konfigurationsdatei (.\\bild.jpg)."));
        Row(table, "Anpassung an die Auflösung", Combo(new[]
        {
            (WallpaperFit.Fill, "Ausfüllen – füllt den Bildschirm, Überstand wird abgeschnitten"),
            (WallpaperFit.Fit, "Anpassen – ganzes Bild, Ränder in der Hintergrundfarbe"),
            (WallpaperFit.Stretch, "Dehnen – füllt den Bildschirm, Bild wird verzerrt"),
            (WallpaperFit.Center, "Zentriert – unskaliert in der Mitte"),
            (WallpaperFit.Tile, "Nebeneinander – unskaliert gekachelt"),
            (WallpaperFit.None, "Keine – Originalgröße, Windows skaliert Bild samt Panel")
        }, () => _config.Wallpaper.Fit, v => _config.Wallpaper.Fit = v));
        Row(table, "", Check("Eigenes Bild je Auflösung bevorzugen (z. B. bild_3440x1440.jpg)", () => _config.Wallpaper.ResolutionVariants, v => _config.Wallpaper.ResolutionVariants = v));
        Row(table, "Hintergrundfarbe (Ränder, kein Bild)", ColorButton(() => _config.Wallpaper.BackgroundColor, v => _config.Wallpaper.BackgroundColor = v));

        Heading(table, "Mehrere Monitore");
        Row(table, "Bilder", Combo(new[]
        {
            (MonitorMode.PerMonitor, "Je Monitor ein eigenes Bild in dessen Auflösung"),
            (MonitorMode.Single, "Ein Bild für alle Monitore")
        }, () => _config.Monitors.Mode, v => _config.Monitors.Mode = v));
        Row(table, "Info-Panel", Combo(new[]
        {
            (PanelMonitors.All, "Auf jedem Monitor"),
            (PanelMonitors.Primary, "Nur einmal, auf dem primären Monitor")
        }, () => _config.Monitors.Panel, v => _config.Monitors.Panel = v));
        Row(table, "", Hint("Die Vorschau zeigt den primären Monitor dieses Rechners."));

        return ScrollPage("Hintergrund", table);
    }

    private TabPage BuildHostsTab()
    {
        var table = NewFormTable();

        Heading(table, "Auf welchen Rechnern das Info-Panel erscheint");
        Row(table, "", Hint("Ein Eintrag je Zeile – Rechnername oder IP-Angabe:\r\n" +
            "• Name mit Platzhaltern: * = beliebig viele Zeichen, ? = genau ein Zeichen (SRV-*, PC-0??, *.dmz.firma.local)\r\n" +
            "• IP-Adresse (10.10.1.5), Netz (10.10.0.0/16) oder Bereich (10.10.1.10-10.10.1.50, kurz 10.10.1.10-50)\r\n" +
            "Groß-/Kleinschreibung ist egal. Namen werden gegen Rechnername und voll qualifizierten Namen geprüft,\r\n" +
            "IP-Angaben gegen alle IPv4-Adressen der aktiven Netzwerkadapter."));

        // Liste als Textfeld mit Import/Export daneben - z.B. fuer eine bisher von Hand gepflegte Textdatei.
        Control Patterns(string what, Func<List<string>> get, Action<List<string>> set)
        {
            var box = TextField(
                () => string.Join(Environment.NewLine, get()),
                v => set(v.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList()));
            box.Multiline = true;
            box.AcceptsReturn = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.Size = new Size(420, 130);
            // Beim Verlassen des Felds bereinigen (nicht waehrend des Tippens - sonst spraenge der Text
            // unter der Schreibmarke weg): Doppelte und leere Zeilen entfernen.
            box.Leave += (_, _) =>
            {
                var typed = box.Lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
                var cleaned = HostsConfig.Normalize(typed);
                if (cleaned.Count != typed.Count)
                {
                    set(cleaned);
                    ReloadControls();
                    Changed();
                    _status.Text = $"{what}: {typed.Count - cleaned.Count} doppelte Einträge entfernt, jetzt {cleaned.Count}.";
                }
            };

            var import = new Button { Text = "Importieren…", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 3, 6, 3) };
            _tips.SetToolTip(import, "Liste aus einer Textdatei lesen: ein Eintrag je Zeile (auch durch Komma oder Semikolon getrennt).\r\nLeerzeilen und Zeilen, die mit # oder // beginnen, werden übergangen.");
            import.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog { Title = $"{what} importieren", Filter = "Textdateien (*.txt;*.csv;*.lst)|*.txt;*.csv;*.lst|Alle Dateien|*.*" };
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    var imported = HostsConfig.ParseList(File.ReadAllText(dialog.FileName));
                    var current = HostsConfig.Normalize(get());
                    var result = imported;
                    if (current.Count > 0)
                    {
                        var answer = MessageBox.Show(this,
                            $"Die Datei enthält {imported.Count} Einträge, die Liste hat bereits {current.Count}.\r\n\r\n" +
                            "Ja\t\t= anhängen (ohne Doppelte)\r\nNein\t\t= vorhandene Einträge ersetzen\r\nAbbrechen\t= nichts ändern",
                            AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (answer == DialogResult.Cancel)
                        {
                            return;
                        }
                        if (answer == DialogResult.Yes)
                        {
                            result = current.Concat(imported.Where(entry => !current.Contains(entry, StringComparer.OrdinalIgnoreCase))).ToList();
                        }
                    }
                    var appended = !ReferenceEquals(result, imported);
                    var added = result.Count - (appended ? current.Count : 0);
                    var skipped = imported.Count - added;
                    set(result);
                    ReloadControls();
                    Changed();
                    _status.Text = $"{what}: {added} Einträge aus {Path.GetFileName(dialog.FileName)} übernommen"
                        + (appended && skipped > 0 ? $", {skipped} schon vorhandene übergangen" : "") + $", jetzt {result.Count}.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Import fehlgeschlagen: {ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var export = new Button { Text = "Exportieren…", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 3, 6, 3) };
            _tips.SetToolTip(export, "Liste als Textdatei speichern, ein Eintrag je Zeile.");
            export.Click += (_, _) =>
            {
                using var dialog = new SaveFileDialog { Title = $"{what} exportieren", Filter = "Textdatei (*.txt)|*.txt|Alle Dateien|*.*", FileName = what.StartsWith("Black") ? "disallowed_hosts.txt" : "allowed_hosts.txt" };
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    var entries = HostsConfig.Normalize(get());
                    File.WriteAllLines(dialog.FileName, entries);
                    _status.Text = $"{what}: {entries.Count} Einträge nach {dialog.FileName} geschrieben.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Export fehlgeschlagen: {ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(6, 0, 0, 0) };
            buttons.Controls.Add(import);
            buttons.Controls.Add(export);
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            row.Controls.Add(box);
            row.Controls.Add(buttons);
            return row;
        }

        // Zusaetzlich zur Liste in der Konfiguration: eine zentral gepflegte Textdatei, die jeder Lauf frisch liest.
        Control ListFile(string what, Func<string?> get, Action<string?> set)
        {
            var path = TextField(() => get() ?? "", v => set(string.IsNullOrWhiteSpace(v) ? null : v.Trim()));
            path.Width = 420;
            path.PlaceholderText = @"z. B. \\fileserver.example.com\share\bginfo\hosts.txt   (leer = keine Datei)";
            _tips.SetToolTip(path, $"{what}-Einträge aus einer Textdatei, zusätzlich zur Liste oben.\r\nEin Eintrag je Zeile – dieselbe Form wie beim Import. Pfad, UNC-Share oder http(s)-URL;\r\nein relativer Pfad gilt relativ zur Konfigurationsdatei.\r\nDie Datei wird bei jedem Lauf gelesen; ist sie nicht erreichbar, gilt die zuletzt gelesene Fassung.");
            var browse = new Button { Text = "…", AutoSize = true };
            browse.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog { Title = $"{what}-Datei wählen", Filter = "Textdateien (*.txt;*.csv;*.lst)|*.txt;*.csv;*.lst|Alle Dateien|*.*" };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var sameFolder = _path != null && string.Equals(Path.GetDirectoryName(dialog.FileName), Path.GetDirectoryName(_path), StringComparison.OrdinalIgnoreCase);
                    path.Text = sameFolder ? @".\" + Path.GetFileName(dialog.FileName) : dialog.FileName;
                }
            };
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            row.Controls.Add(path);
            row.Controls.Add(browse);
            return row;
        }

        Row(table, "Whitelist – nur auf diesen Rechnern\r\n(leer = auf allen)", Patterns("Whitelist", () => _config.Hosts.Include, v => _config.Hosts.Include = v));
        Row(table, "… zusätzlich aus Datei", ListFile("Whitelist", () => _config.Hosts.IncludeFile, v => _config.Hosts.IncludeFile = v));
        Row(table, "Blacklist – nicht auf diesen Rechnern\r\n(hat Vorrang)", Patterns("Blacklist", () => _config.Hosts.Exclude, v => _config.Hosts.Exclude = v));
        Row(table, "… zusätzlich aus Datei", ListFile("Blacklist", () => _config.Hosts.ExcludeFile, v => _config.Hosts.ExcludeFile = v));
        Row(table, "", Hint("Die Listen oben stehen in der Konfigurationsdatei. Eine Datei daneben wird bei jedem Lauf frisch gelesen –\r\nÄnderungen an ihr wirken ohne neues Verteilen der Konfiguration. Beides zusammen ist möglich; es gilt die Summe.\r\nWie viele Einträge aus den Dateien kamen, steht nach dem Rendern der Vorschau in der Statuszeile."));
        Row(table, "Auf ausgeschlossenen Rechnern", Combo(new[]
        {
            (ExcludedAction.BackgroundOnly, "Hintergrund ohne Panel setzen"),
            (ExcludedAction.Skip, "Nichts tun – Wallpaper bleibt unverändert")
        }, () => _config.Hosts.OnExcluded, v => _config.Hosts.OnExcluded = v));
        Row(table, "", Hint($"Dieser Rechner heißt {Environment.MachineName}. Ob er ausgeschlossen wäre, steht unten in der Statuszeile."));

        return ScrollPage("Rechner", table);
    }

    private TabPage BuildFieldsTab()
    {
        var include = TextField(() => _config.Include ?? "", v =>
        {
            _config.Include = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            _tagsStale = true;
        });
        include.Dock = DockStyle.Fill;
        var browse = new Button { Text = "…", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "JSON-Dateien (*.json)|*.json|Alle Dateien|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var sameFolder = _path != null && string.Equals(Path.GetDirectoryName(dialog.FileName), Path.GetDirectoryName(_path), StringComparison.OrdinalIgnoreCase);
                include.Text = sameFolder ? @".\" + Path.GetFileName(dialog.FileName) : dialog.FileName;
            }
        };

        _fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name (ohne Klammern)", FillWeight = 20 });
        _fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value – Platzhalter und freier Text", FillWeight = 45 });
        // Nur Anzeige: was die Definition auf diesem Rechner gerade ergibt (fuellt UpdateCurrentValues).
        _fields.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Aktueller Wert",
            FillWeight = 35,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = SystemColors.Control, ForeColor = SystemColors.GrayText }
        });
        void LoadFields()
        {
            _fields.Rows.Clear();
            foreach (var (name, definition) in _config.Fields)
            {
                _fields.Rows.Add(name, definition, "");
            }
            ScheduleCurrentValues();
        }
        void StoreFields()
        {
            if (_loading || _fillingFieldValues)
            {
                return;
            }
            var fields = new Dictionary<string, string>();
            foreach (DataGridViewRow row in _fields.Rows)
            {
                var name = (row.Cells[0].Value as string ?? "").Trim().Trim('{', '}');
                if (!row.IsNewRow && name.Length > 0)
                {
                    fields[name] = row.Cells[1].Value as string ?? "";
                }
            }
            _config.Fields = fields;
            _tagsStale = true;
            Changed();
        }
        _loaders.Add(LoadFields);
        _fields.CellValueChanged += (_, _) => StoreFields();
        _fields.RowsRemoved += (_, _) => StoreFields();

        var wmi = new Button { Text = "WMI-Abfrage in die Definition einfügen…", AutoSize = true };
        wmi.Click += (_, _) =>
        {
            using var dialog = new WmiDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            // In die aktuelle Zeile; ohne Auswahl (oder in der leeren Eingabezeile) eine neue anlegen.
            var row = _fields.CurrentRow is { IsNewRow: false } current ? current : _fields.Rows[_fields.Rows.Add("neuesfeld", "")];
            row.Cells[1].Value = (row.Cells[1].Value as string ?? "") + dialog.Placeholder;
            _fields.CurrentCell = row.Cells[0];
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 9; i++)
        {
            table.RowStyles.Add(new RowStyle(i == 7 ? SizeType.Percent : SizeType.AutoSize, 100));
        }

        void Span(Control control, int row)
        {
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 3);
        }
        Label Caption(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 14, 7) };

        var version = TextField(() => _config.Version ?? "", v =>
        {
            _config.Version = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            _tagsStale = true;
        });
        version.PlaceholderText = "z. B. 1.0 – als {configversion} im Panel nutzbar";
        version.Width = 320;
        var description = TextField(() => _config.Description ?? "", v => _config.Description = string.IsNullOrWhiteSpace(v) ? null : v);
        description.Dock = DockStyle.Fill;
        description.PlaceholderText = "eigene Anmerkungen – bleiben beim Speichern erhalten (eigene //-Kommentare nicht)";

        Span(new Label { Text = "Diese Datei", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 3, 3, 6) }, 0);
        table.Controls.Add(Caption("Version"), 0, 1);
        table.Controls.Add(version, 1, 1);
        table.Controls.Add(Caption("Beschreibung"), 0, 2);
        table.Controls.Add(description, 1, 2);
        table.SetColumnSpan(description, 2);
        Span(new Label { Text = "Zentrale Datei", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 14, 3, 6) }, 3);
        table.Controls.Add(Caption("Pfad, UNC oder URL"), 0, 4);
        table.Controls.Add(include, 1, 4);
        table.Controls.Add(browse, 2, 4);
        Span(Hint("Ihre „fields“ gelten zusätzlich zu den eigenen (bei gleichem Namen gewinnt die eigene Definition). Ihre „items“ erscheinen dort,\r\nwo im Reiter Inhalt ein Eintrag „Zentrale Zeilen“ steht. Die Datei wird bei jedem Lauf gelesen und lokal zwischengespeichert."), 5);
        Span(new Label { Text = "Eigene Felder dieser Datei", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 14, 3, 6) }, 6);
        Span(_fields, 7);
        Span(wmi, 8);

        var page = new TabPage("Datei und Felder");
        page.Controls.Add(table);
        return page;
    }

    /// <summary>
    /// Alles, was BGInfo-Reloaded.exe ueber Schalter kann und nicht in der Konfigurationsdatei steht:
    /// Autostart-Tasks, Vorschau in eine Datei, Log.
    /// </summary>
    private TabPage BuildToolsTab()
    {
        var table = NewFormTable();
        Button Action(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 3, 6, 3) };
            button.Click += (_, _) => action();
            return button;
        }

        Heading(table, "Autostart für den angemeldeten Benutzer");
        Row(table, "Aktualisieren alle … Minuten (0 = nur bei Anmeldung)", _taskInterval);
        Row(table, "", _taskWatch);
        Row(table, "", _taskUseConfig);
        var tasks = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        tasks.Controls.Add(Action("Autostart einrichten", () =>
        {
            var arguments = $"--register-task --interval-minutes {(int)_taskInterval.Value}" + (_taskWatch.Checked ? " --watch" : "");
            if (_taskUseConfig.Checked)
            {
                if ((_dirty || _path == null) && !Save(false))
                {
                    return;
                }
                arguments += $" --config \"{_path}\"";
            }
            RunTool(arguments, "Autostart eingerichtet.");
        }));
        tasks.Controls.Add(Action("Autostart entfernen", () => RunTool("--unregister-task", "Autostart entfernt.")));
        Row(table, "", tasks);
        Row(table, "", Hint("Die Tasks gelten für den Benutzer, der sie einrichtet, und laufen in dessen Sitzung – auf den Zielrechnern\r\nmuss das je Benutzer geschehen (z. B. per Anmeldeskript: BGInfo-Reloaded.exe --register-task)."));

        Heading(table, "Selbst-Update aus einem Ordner");
        var updatePath = TextField(() => _config.Update.Path ?? "", v => _config.Update.Path = string.IsNullOrWhiteSpace(v) ? null : v.Trim());
        updatePath.Width = 420;
        updatePath.PlaceholderText = @"z. B. \\fileserver.example.com\share\BGInfo-Reloaded   (leer = kein Update aus einem Ordner)";
        var browseUpdate = new Button { Text = "…", AutoSize = true };
        browseUpdate.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Ordner mit der aktuellen BGInfo-Reloaded.exe", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                updatePath.Text = dialog.SelectedPath;
            }
        };
        var updateRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        updateRow.Controls.Add(updatePath);
        updateRow.Controls.Add(browseUpdate);
        Row(table, "Ordner mit den aktuellen Programmdateien", updateRow);
        Row(table, "", Check("Bei jedem Lauf prüfen und Neueres von selbst holen", () => _config.Update.Auto, v => _config.Update.Auto = v));

        // Was geholt wird - die genauen Dateien stehen im Tooltip jedes Kaestchens.
        CheckBox UpdateOption(string text, string files, Func<bool> get, Action<bool> set)
        {
            var box = Check(text, get, set);
            _tips.SetToolTip(box, files);
            return box;
        }
        Row(table, "Aus dem Ordner holen", UpdateOption("Programm  (Standard: an)",
            $"{FolderUpdater.MainExe}\r\nWird ersetzt, wenn im Ordner eine höhere Version liegt. Aktiv ab dem nächsten Lauf;\r\ndie vorherige Fassung bleibt bis dahin als {FolderUpdater.MainExe}.old liegen.",
            () => _config.Update.Program, v => _config.Update.Program = v));
        Row(table, "", UpdateOption("Editor  (Standard: an)",
            $"{FolderUpdater.EditorExe}\r\nWird ersetzt, wenn im Ordner eine höhere Version liegt – aber nur auf Rechnern,\r\nauf denen der Editor schon neben der exe liegt. Er wird nirgends neu hingelegt.",
            () => _config.Update.Editor, v => _config.Update.Editor = v));
        Row(table, "", UpdateOption("Konfiguration  (Standard: aus)",
            $"{AppConfig.FileName}\r\nWird neben die exe geholt, wenn die Datei im Ordner neuer ist (Änderungsdatum).\r\nDie vorherige bleibt als {AppConfig.FileName}.bak. Gilt ab dem nächsten Lauf.\r\nDie Datei im Ordner sollte denselben Update-Abschnitt enthalten – sonst endet der Abgleich mit ihr.",
            () => _config.Update.Config, v => _config.Update.Config = v));
        Row(table, "", UpdateOption("Hintergrundbilder  (Standard: aus)",
            $"{FolderUpdater.BackgroundPattern}  (z. B. background.jpg, background_3440x1440.jpg)\r\nund alle Dateien im Unterordner {FolderUpdater.BackgroundFolder}\\\r\nGeholt wird, was neuer ist oder noch fehlt. Nichts wird gelöscht.",
            () => _config.Update.Backgrounds, v => _config.Update.Backgrounds = v));
        Row(table, "", Hint("Standard: nur BGInfo-Reloaded.exe und – wo vorhanden – BGInfo-Reloaded-Editor.exe.\r\nDie genauen Dateien zeigt jedes Kästchen, wenn die Maus darauf zeigt.\r\nOhne „Bei jedem Lauf prüfen“ geschieht alles nur mit BGInfo-Reloaded.exe --update.\r\nWer in den Ordner schreiben darf, bestimmt Programm und Konfiguration auf den Rechnern –\r\nSchreibrechte dort entsprechend einschränken."));

        Heading(table, "Selbst-Update aus GitHub-Releases");
        var updateRepo = TextField(() => _config.Update.Repo ?? "", v => _config.Update.Repo = string.IsNullOrWhiteSpace(v) ? null : v.Trim());
        updateRepo.Width = 420;
        updateRepo.PlaceholderText = "owner/name, z. B. Kilowatt-W/bginfo-reloaded   (leer = keins)";
        _tips.SetToolTip(updateRepo, $"Aus dem neuesten Release wird nur {FolderUpdater.MainExe} geholt.\r\nEntspricht --update --repo owner/name, nur zentral über die Konfiguration verteilbar.");
        Row(table, "GitHub-Repository", updateRepo);
        Row(table, "", Hint("Wird nur mit BGInfo-Reloaded.exe --update benutzt, nie automatisch,\r\nund nur wenn oben kein Ordner eingetragen ist. Geholt wird ausschließlich BGInfo-Reloaded.exe.\r\nEin privates Repository ist ohne Anmeldung nicht erreichbar."));

        Heading(table, "Werkzeuge");
        Row(table, "", Action("Vorschau als Bilddatei speichern…", () =>
        {
            using var dialog = new SaveFileDialog { Filter = "PNG-Bild (*.png)|*.png|JPEG-Bild (*.jpg)|*.jpg", FileName = "vorschau.png" };
            if (dialog.ShowDialog(this) == DialogResult.OK && ((!_dirty && _path != null) || Save(false)))
            {
                RunTool($"--preview \"{dialog.FileName}\" --config \"{_path}\"", $"Vorschau gespeichert: {dialog.FileName}");
            }
        }));
        Row(table, "", Action("Logdatei öffnen", () => OpenWithShell(RunLog.LogFile)));
        Row(table, "", Action("Ordner mit Log, Cache und erzeugten Bildern öffnen", () => OpenWithShell(WallpaperState.StateDirectory)));
        Row(table, "", Hint("„Jetzt anwenden“ in der Symbolleiste setzt das Wallpaper sofort. Das Update selbst (--update, --update-source) und\r\n--list-tags gibt es nur auf der Kommandozeile; die Platzhalter zeigt der Reiter Inhalt."));

        return ScrollPage("Autostart und Werkzeuge", table);
    }

    /// <summary>Fuehrt BGInfo-Reloaded.exe neben dem Editor mit den Argumenten aus und meldet das Ergebnis in der Fusszeile.</summary>
    private async void RunTool(string arguments, string successText)
    {
        var exe = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "BGInfo-Reloaded.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, $"BGInfo-Reloaded.exe wurde nicht neben dem Editor gefunden:\r\n{exe}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _status.Text = "BGInfo-Reloaded.exe läuft…";
            using var process = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false })
                ?? throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");
            await process.WaitForExitAsync();
            _status.Text = process.ExitCode == 0
                ? successText
                : $"BGInfo-Reloaded.exe meldet einen Fehler (Exit-Code {process.ExitCode}). Details: {RunLog.LogFile}";
        }
        catch (Exception ex)
        {
            _status.Text = $"Aufruf fehlgeschlagen: {ex.Message}";
        }
    }

    private void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _status.Text = $"Öffnen fehlgeschlagen: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------ Bausteine fuer Eingabefelder

    private static TableLayoutPanel NewFormTable()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static TabPage ScrollPage(string title, Control content)
    {
        var page = new TabPage(title) { AutoScroll = true };
        page.Controls.Add(content);
        return page;
    }

    private static void Row(TableLayoutPanel table, string caption, Control control)
    {
        table.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 14, 7) });
        control.Anchor = AnchorStyles.Left;
        if (control.Margin == new Padding(3))
        {
            control.Margin = new Padding(3, 4, 3, 4);
        }
        table.Controls.Add(control);
    }

    private void Heading(TableLayoutPanel table, string text)
    {
        var label = new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 14, 3, 6) };
        table.Controls.Add(label);
        table.SetColumnSpan(label, 2);
    }

    private static Label Hint(string text) => new() { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText };

    private NumericUpDown Number(decimal min, decimal max, Func<decimal> get, Action<decimal> set, int decimals = 0)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = 110 };
        _loaders.Add(() => box.Value = Math.Clamp(get(), min, max));
        box.ValueChanged += (_, _) =>
        {
            if (!_loading)
            {
                set(box.Value);
                Changed();
            }
        };
        return box;
    }

    /// <summary>Schieberegler mit Klartext daneben - fuer Werte, die man am besten nach Augenmass an der Vorschau einstellt.</summary>
    private Control Slider(int min, int max, int step, Func<int, string> describe, Func<int> get, Action<int> set)
    {
        var bar = new TrackBar { Minimum = min, Maximum = max, TickFrequency = step * 2, SmallChange = step, LargeChange = step * 2, Width = 260, AutoSize = false, Height = LogicalToDeviceUnits(30) };
        var text = new Label { AutoSize = true, Margin = new Padding(6, 7, 3, 0) };
        _loaders.Add(() =>
        {
            bar.Value = Math.Clamp(get(), min, max);
            text.Text = describe(bar.Value);
        });
        bar.ValueChanged += (_, _) =>
        {
            text.Text = describe(bar.Value);
            if (!_loading)
            {
                // Auf die Schrittweite einrasten, damit auch mit der Maus runde Werte entstehen.
                var snapped = (int)Math.Round(bar.Value / (double)step) * step;
                if (snapped != bar.Value)
                {
                    bar.Value = Math.Clamp(snapped, min, max);
                    return;
                }
                set(bar.Value);
                Changed();
            }
        };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.Add(bar);
        row.Controls.Add(text);
        return row;
    }

    private ComboBox Combo<T>((T Value, string Text)[] options, Func<T> get, Action<T> set) where T : struct
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
        box.Items.AddRange(options.Select(o => (object)o.Text).ToArray());
        _loaders.Add(() => box.SelectedIndex = Math.Max(0, Array.FindIndex(options, o => EqualityComparer<T>.Default.Equals(o.Value, get()))));
        box.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading && box.SelectedIndex >= 0)
            {
                set(options[box.SelectedIndex].Value);
                Changed();
            }
        };
        return box;
    }

    private CheckBox Check(string text, Func<bool> get, Action<bool> set)
    {
        var box = new CheckBox { Text = text, AutoSize = true };
        _loaders.Add(() => box.Checked = get());
        box.CheckedChanged += (_, _) =>
        {
            if (!_loading)
            {
                set(box.Checked);
                Changed();
            }
        };
        return box;
    }

    private TextBox TextField(Func<string> get, Action<string> set)
    {
        var box = new TextBox { Width = 260 };
        _loaders.Add(() => box.Text = get());
        box.TextChanged += (_, _) =>
        {
            if (!_loading)
            {
                set(box.Text);
                Changed();
            }
        };
        return box;
    }

    /// <summary>Farbfeld: zeigt die Farbe, ein Klick oeffnet den Farbdialog. Eine vorhandene Deckkraft bleibt erhalten.</summary>
    private Button ColorButton(Func<string> get, Action<string> set)
    {
        var button = new Button { Width = 130, Height = 28, FlatStyle = FlatStyle.Flat };
        void Show()
        {
            var color = SafeColor(get());
            button.BackColor = Color.FromArgb(255, color);
            button.ForeColor = color.GetBrightness() > 0.55f ? Color.Black : Color.White;
            button.Text = get();
        }

        _loaders.Add(Show);
        button.Click += (_, _) =>
        {
            var current = SafeColor(get());
            using var dialog = new ColorDialog { Color = Color.FromArgb(255, current), FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var hadAlpha = get().Trim().TrimStart('#').Length == 8;
                set(ToHex(dialog.Color, hadAlpha ? current.A : null));
                Show();
                Changed();
            }
        };
        return button;
    }

    private static Color SafeColor(string hex)
    {
        try
        {
            return WallpaperRenderer.ParseColor(hex, "");
        }
        catch
        {
            return Color.Gray; // ungueltiger Wert aus einer von Hand bearbeiteten Datei
        }
    }

    private static string ToHex(Color color, int? alpha) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}" + (alpha is { } a ? $"{a:X2}" : "");

    // ------------------------------------------------------------------ Zeilen des Panels

    private static string Describe(ItemConfig item)
    {
        var text = DescribeContent(item);
        if (item.HasFilter)
        {
            var filters = new List<string>();
            if (item.OnlyOn is { Count: > 0 }) filters.Add("nur auf " + string.Join(", ", item.OnlyOn));
            if (item.NotOn is { Count: > 0 }) filters.Add("nicht auf " + string.Join(", ", item.NotOn));
            if (!string.IsNullOrWhiteSpace(item.When)) filters.Add("wenn " + item.When);
            text += $"   [{string.Join("; ", filters)}]";
        }
        return text;
    }

    /// <summary>Wie tief der Eintrag in Bloecken steckt - nur fuer die Einrueckung in der Liste.</summary>
    private int BlockDepth(int index)
    {
        var depth = 0;
        for (var i = 0; i < index; i++)
        {
            depth += _config.Items[i].Block != null ? 1 : _config.Items[i].EndBlock ? -1 : 0;
        }
        if (_config.Items[index].EndBlock)
        {
            depth--;
        }
        return Math.Max(0, depth);
    }

    private string DescribeAt(int index) => new string(' ', BlockDepth(index) * 6) + Describe(_config.Items[index]);

    private static string DescribeContent(ItemConfig item)
    {
        if (item.EndBlock)
        {
            return "└ Blockende";
        }
        if (item.Block != null)
        {
            return $"┌ Block {item.Block}".TrimEnd() + (item.HasFilter ? "" : "   (ohne Bedingung – immer sichtbar)");
        }
        if (item.IncludeItems)
        {
            return "      (Zeilen der zentralen Datei)";
        }
        if (item.Spacer)
        {
            return "      (Leerraum)";
        }
        if (item.Section != null)
        {
            return $"■ {item.Section}";
        }
        if (item.Drives)
        {
            return "      Laufwerke – eine Zeile je Festplatte"
                + (item.Label != null || item.Value != null ? $":  {item.Label}  →  {item.Value}" : "");
        }
        return $"      {item.Label}:  {item.Value}" + (item.HideIfEmpty ? "   (nur wenn vorhanden)" : "") + (string.IsNullOrEmpty(item.Split) ? "" : "   (geteilt)");
    }

    private void RefreshItems(int select)
    {
        var wasLoading = _loading;
        _loading = true;
        _items.BeginUpdate();
        _items.Items.Clear();
        for (var i = 0; i < _config.Items.Count; i++)
        {
            _items.Items.Add(DescribeAt(i));
        }
        _items.EndUpdate();
        _items.SelectedIndex = _config.Items.Count == 0 ? -1 : Math.Clamp(select, 0, _config.Items.Count - 1);
        _loading = wasLoading;
        ShowSelectedItem();
    }

    private ItemConfig? SelectedItem =>
        _items.SelectedIndex >= 0 && _items.SelectedIndex < _config.Items.Count ? _config.Items[_items.SelectedIndex] : null;

    private void ShowSelectedItem()
    {
        var item = SelectedItem;
        var wasLoading = _loading;
        _loading = true;

        var isBlock = item is { Block: not null, EndBlock: false };
        var isMarker = isBlock || item is { EndBlock: true };
        var isSection = !isMarker && item is { Section: not null, Spacer: false, IncludeItems: false };
        var isDrives = !isMarker && item is { Drives: true, Spacer: false, Section: null, IncludeItems: false };
        var isValue = item != null && !isMarker && !item.Spacer && !item.IncludeItems && !isSection && !isDrives;

        _labelCaption.Text = isBlock ? "Blockname" : isSection ? "Überschrift" : "Bezeichnung";
        _valueCaption.Text = "Value";
        _itemLabel.Enabled = isBlock || isSection || isDrives || isValue;
        _itemValue.Enabled = isDrives || isValue;
        _itemHide.Enabled = isValue;
        _itemSplit.Enabled = isDrives || isValue;
        _itemSplit.Text = isDrives || isValue ? item!.Split ?? "" : "";
        _itemLabel.Text = isBlock ? item!.Block : isSection ? item!.Section : isMarker ? "" : item?.Label ?? "";
        _itemValue.Text = isSection || isMarker ? "" : item?.Value ?? "";
        _itemHide.Checked = isValue && item!.HideIfEmpty;
        _itemOnlyOn.Enabled = _itemNotOn.Enabled = _itemWhen.Enabled = item is { EndBlock: false };
        _itemOnlyOn.Text = string.Join(", ", item?.OnlyOn ?? new());
        _itemNotOn.Text = string.Join(", ", item?.NotOn ?? new());
        _itemWhen.Text = item?.When ?? "";
        _itemLabel.PlaceholderText = isDrives ? "Standard: Laufwerk {drive}" : "";
        _itemValue.PlaceholderText = isDrives ? "Standard: {used} GB / {total} GB belegt ({free} GB frei)" : "";

        _loading = wasLoading;
        // Nicht den Wert der vorher gewaehlten Zeile stehen lassen, waehrend der neue ermittelt wird.
        _itemCurrent.Text = item == null || item.Spacer || item.EndBlock ? "" : "wird ermittelt…";
        ScheduleCurrentValues();
    }

    // ------------------------------------------------------------------ Aktuelle Werte

    /// <summary>Kurz warten, bis nicht mehr getippt wird - das Aufloesen fragt WMI, Registry und Dateien ab.</summary>
    private void ScheduleCurrentValues()
    {
        _currentTimer.Stop();
        _currentTimer.Start();
    }

    /// <summary>
    /// Fuellt "Aktueller Wert" der ausgewaehlten Zeile und die Spalte "Aktueller Wert" der eigenen
    /// Felder - im Hintergrund auf einer Kopie der Konfiguration, wie die Vorschau.
    /// </summary>
    private async void UpdateCurrentValues()
    {
        _currentTimer.Stop();
        var generation = ++_currentGeneration;
        var index = _items.SelectedIndex;
        var json = _config.ToJson();
        var baseDirectory = ConfigDirectory;

        try
        {
            var (itemText, fieldValues) = await Task.Run(() =>
            {
                var config = AppConfig.FromJson(json, "Editor");
                config.BaseDirectory = baseDirectory;
                var ownFields = config.Fields.Keys.ToList();
                try
                {
                    config.ResolveInclude();
                }
                catch
                {
                    // Fehler der zentralen Datei meldet die Vorschau in der Statuszeile
                }

                var resolver = new ValueResolver(config);
                var values = ownFields.ToDictionary(name => name, name => resolver.Expand($"{{{name.Trim()}}}"), StringComparer.OrdinalIgnoreCase);
                var text = index >= 0 && index < config.Items.Count ? DescribeCurrentValue(config, index, resolver) : "";
                return (text, values);
            });
            if (generation != _currentGeneration)
            {
                return; // inzwischen wurde weiter getippt oder eine andere Zeile gewaehlt
            }

            _itemCurrent.Text = itemText;
            _fillingFieldValues = true;
            foreach (DataGridViewRow row in _fields.Rows)
            {
                var name = (row.Cells[0].Value as string ?? "").Trim().Trim('{', '}');
                if (!row.IsNewRow)
                {
                    row.Cells[2].Value = fieldValues.TryGetValue(name, out var value) ? value : "";
                }
            }
        }
        catch (Exception ex)
        {
            if (generation == _currentGeneration)
            {
                _itemCurrent.Text = $"nicht ermittelbar: {ex.Message}";
            }
        }
        finally
        {
            _fillingFieldValues = false;
        }
    }

    /// <summary>Was eine Zeile der Konfiguration auf diesem Rechner ergibt - samt Hinweis, wenn sie hier gar nicht erscheint.</summary>
    private static string DescribeCurrentValue(AppConfig config, int index, ValueResolver resolver)
    {
        var item = config.Items[index];
        if (item.EndBlock || item.Spacer)
        {
            return "";
        }

        var hostNames = new[] { Environment.MachineName, resolver.Expand("{fqdn}") };

        // Liegt die Zeile in einem Block, dessen Filter hier nicht zutrifft? Dann erscheint sie nicht,
        // egal was sie selbst ergibt.
        var blocks = new Stack<bool>();
        for (var i = 0; i < index; i++)
        {
            if (config.Items[i].EndBlock)
            {
                blocks.TryPop(out _);
            }
            else if (config.Items[i].Block != null)
            {
                blocks.Push((blocks.Count == 0 || blocks.Peek()) && SystemInfoCollector.Applies(config.Items[i], resolver, hostNames));
            }
        }
        var inHiddenBlock = blocks.Count > 0 && !blocks.Peek();

        var hidden = inHiddenBlock ? "   – liegt in einem Block, der auf diesem Rechner ausgeblendet ist"
            : SystemInfoCollector.Applies(item, resolver, hostNames) ? "" : "   – auf diesem Rechner ausgeblendet (Filter trifft nicht zu)";

        if (item.Block != null)
        {
            return hidden.Length == 0 ? "Block wird auf diesem Rechner angezeigt" : "Block ist auf diesem Rechner ausgeblendet (Filter trifft nicht zu)";
        }
        if (item.IncludeItems)
        {
            return $"{config.IncludedItems.Count} Zeilen aus der zentralen Datei{hidden}";
        }
        if (item.Section != null)
        {
            return resolver.Expand(item.Section) + hidden;
        }
        if (item.Drives)
        {
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).ToList();
            if (drives.Count == 0)
            {
                return "kein Laufwerk gefunden" + hidden;
            }
            var label = resolver.Expand(SystemInfoCollector.FormatDrive(drives[0], item.Label ?? SystemInfoCollector.DefaultDriveLabel));
            var value = resolver.Expand(SystemInfoCollector.FormatDrive(drives[0], item.Value ?? SystemInfoCollector.DefaultDriveValue));
            return $"{label}   {ShowParts(value, item.Split)}" + (drives.Count > 1 ? $"   (+ {drives.Count - 1} weitere)" : "") + hidden;
        }

        var current = resolver.Expand(item.Value);
        var dropped = item.HideIfEmpty && (string.IsNullOrWhiteSpace(current) || current == ValueResolver.NotAvailable)
            ? "   – Zeile entfällt auf diesem Rechner (Wert leer oder n/a)"
            : "";
        // Enthaelt auch die Bezeichnung Platzhalter, sie mit anzeigen.
        var labelText = item.Label != null && item.Label.Contains('{') ? resolver.Expand(item.Label) + ":  " : "";
        return labelText + ShowParts(current, item.Split) + dropped + hidden;
    }

    /// <summary>Den Wert so zeigen, wie "Trennen bei" ihn auf Zeilen verteilt: die Teile mit ⏎ dazwischen.</summary>
    private static string ShowParts(string value, string? split)
    {
        var parts = SystemInfoCollector.SplitValue(value, split);
        return parts.Count > 1 ? string.Join("  ⏎  ", parts) + $"   ({parts.Count} Zeilen)" : value;
    }

    private void EditSelectedItem()
    {
        var item = SelectedItem;
        if (_loading || item == null)
        {
            return;
        }

        static List<string>? Patterns(string text)
        {
            var list = text.Split(',', ';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            return list.Count > 0 ? list : null;
        }
        item.OnlyOn = Patterns(_itemOnlyOn.Text);
        item.NotOn = Patterns(_itemNotOn.Text);
        item.When = string.IsNullOrWhiteSpace(_itemWhen.Text) ? null : _itemWhen.Text.Trim();

        if (item.EndBlock)
        {
            item.OnlyOn = item.NotOn = null;
            item.When = null;
        }
        else if (item.Block != null)
        {
            item.Block = _itemLabel.Text;
        }
        else if (item.Spacer || item.IncludeItems)
        {
            // nur Filter
        }
        else if (item.Section != null)
        {
            item.Section = _itemLabel.Text;
        }
        else if (item.Drives)
        {
            // Leere Felder bedeuten "Standardtext" - deshalb als null speichern, nicht als "".
            item.Label = _itemLabel.Text.Length > 0 ? _itemLabel.Text : null;
            item.Value = _itemValue.Text.Length > 0 ? _itemValue.Text : null;
            // Nicht trimmen: ein Leerzeichen kann zum Trennzeichen gehoeren.
            item.Split = _itemSplit.Text.Length > 0 ? _itemSplit.Text : null;
        }
        else
        {
            item.Label = _itemLabel.Text;
            item.Value = _itemValue.Text;
            item.HideIfEmpty = _itemHide.Checked;
            item.Split = _itemSplit.Text.Length > 0 ? _itemSplit.Text : null;
        }

        _loading = true;
        _items.Items[_items.SelectedIndex] = DescribeAt(_items.SelectedIndex);
        _loading = false;
        Changed();
    }

    private void InsertItem(ItemConfig item)
    {
        var index = _items.SelectedIndex + 1;
        _config.Items.Insert(index, item);
        RefreshItems(index);
        Changed();
        (item.Section != null || !item.Spacer && !item.Drives && !item.IncludeItems ? _itemLabel : null)?.Focus();
    }

    private void RemoveItem()
    {
        var index = _items.SelectedIndex;
        if (index >= 0)
        {
            _config.Items.RemoveAt(index);
            RefreshItems(index);
            Changed();
        }
    }

    private void MoveItem(int direction)
    {
        var index = _items.SelectedIndex;
        var target = index + direction;
        if (index >= 0 && target >= 0 && target < _config.Items.Count)
        {
            (_config.Items[index], _config.Items[target]) = (_config.Items[target], _config.Items[index]);
            RefreshItems(target);
            Changed();
        }
    }

    // ------------------------------------------------------------------ Platzhalter ziehen

    /// <summary>Der Platzhalter, der gerade aus der Liste gezogen wird (Text und Bedeutung), sonst null.</summary>
    private (string Tag, string Meaning)? _draggedTag;

    /// <summary>Einfuegestelle in der Zeilenliste waehrend des Ziehens, -1 = keine.</summary>
    private int _dropIndex = -1;

    /// <summary>
    /// Platzhalter aus der rechten Liste ziehen: auf die Zeilenliste = neue Zeile an dieser Stelle
    /// (Bezeichnung = Bedeutung, Value = Platzhalter), auf ein Textfeld = dort einfuegen.
    /// </summary>
    private void SetUpPlaceholderDragDrop()
    {
        _tags.ItemDrag += (_, e) =>
        {
            if (e.Item is ListViewItem item)
            {
                _draggedTag = (item.Text, item.SubItems.Count > 1 ? item.SubItems[1].Text : item.Text);
                try
                {
                    _tags.DoDragDrop(item.Text, DragDropEffects.Copy);
                }
                finally
                {
                    _draggedTag = null;
                    ShowDropLine(-1);
                }
            }
        };

        _items.AllowDrop = true;
        _items.DragEnter += (_, e) => e.Effect = _draggedTag != null ? DragDropEffects.Copy : DragDropEffects.None;
        _items.DragOver += (_, e) =>
        {
            e.Effect = _draggedTag != null ? DragDropEffects.Copy : DragDropEffects.None;
            if (_draggedTag != null)
            {
                ShowDropLine(DropIndexAt(new Point(e.X, e.Y)));
            }
        };
        _items.DragLeave += (_, _) => ShowDropLine(-1);
        _items.DragDrop += (_, e) =>
        {
            if (_draggedTag is not { } dragged)
            {
                return;
            }
            var index = DropIndexAt(new Point(e.X, e.Y));
            ShowDropLine(-1);
            _config.Items.Insert(index, new ItemConfig { Label = dragged.Meaning, Value = dragged.Tag });
            RefreshItems(index);
            Changed();
            _status.Text = $"Zeile „{dragged.Meaning}“ mit {dragged.Tag} eingefügt – Bezeichnung unten anpassen.";
            _itemLabel.Focus();
            _itemLabel.SelectAll();
        };

        foreach (var box in new[] { _itemLabel, _itemValue, _itemWhen })
        {
            box.AllowDrop = true;
            box.DragEnter += (_, e) => e.Effect = _draggedTag != null && box.Enabled ? DragDropEffects.Copy : DragDropEffects.None;
            box.DragOver += (_, e) => e.Effect = _draggedTag != null && box.Enabled ? DragDropEffects.Copy : DragDropEffects.None;
            box.DragDrop += (_, e) =>
            {
                if (_draggedTag is not { } dragged || !box.Enabled)
                {
                    return;
                }
                // An der Stelle unter dem Mauszeiger einfuegen; hinter dem letzten Zeichen ans Ende.
                var point = box.PointToClient(new Point(e.X, e.Y));
                var position = box.TextLength == 0 ? 0 : box.GetCharIndexFromPosition(point);
                if (position == box.TextLength - 1 && point.X > box.GetPositionFromCharIndex(position).X + 4)
                {
                    position = box.TextLength;
                }
                box.Focus();
                box.Select(position, 0);
                box.SelectedText = dragged.Tag;
                _lastItemTextBox = box;
            };
        }
    }

    /// <summary>Stelle in der Zeilenliste, an der beim Loslassen eingefuegt wird: vor der Zeile unter dem Zeiger, in ihrer unteren Haelfte dahinter.</summary>
    private int DropIndexAt(Point screen)
    {
        var point = _items.PointToClient(screen);
        var index = _items.IndexFromPoint(point);
        if (index < 0 || index >= _items.Items.Count)
        {
            return _items.Items.Count;
        }
        var bounds = _items.GetItemRectangle(index);
        return point.Y > bounds.Top + bounds.Height / 2 ? index + 1 : index;
    }

    /// <summary>Zeichnet waehrend des Ziehens eine Linie an der Einfuegestelle.</summary>
    private void ShowDropLine(int index)
    {
        if (index == _dropIndex)
        {
            return;
        }
        _dropIndex = index;
        _items.Invalidate();
        _items.Update();
        if (index < 0)
        {
            return;
        }
        var y = _items.Items.Count == 0 ? 1
            : index < _items.Items.Count ? _items.GetItemRectangle(index).Top
            : _items.GetItemRectangle(_items.Items.Count - 1).Bottom;
        y = Math.Clamp(y, 1, _items.ClientSize.Height - 2);
        using var graphics = _items.CreateGraphics();
        using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(2));
        graphics.DrawLine(pen, 0, y, _items.ClientSize.Width, y);
    }

    /// <summary>Bezeichnung einer frisch mit "+ Zeile" angelegten Zeile - gilt beim Einfuegen wie ein leeres Feld.</summary>
    private const string NewLineLabel = "Bezeichnung";

    /// <summary>
    /// Doppelklick auf einen Platzhalter. Bei einer normalen Zeile (Bezeichnung + Value) richtet sich
    /// das Einfuegen nach dem Feld, in dem die Schreibmarke steht:
    /// - in "Value": der Platzhalter an der Schreibmarke; ist die Bezeichnung noch leer, kommt die
    ///   Bedeutung des Platzhalters dazu,
    /// - in "Bezeichnung": die Bedeutung an der Schreibmarke; ist "Value" noch leer, kommt der
    ///   Platzhalter dazu.
    /// In allen anderen Faellen (Ueberschrift, Blockname, Laufwerke, "Nur wenn") nur der Platzhalter.
    /// </summary>
    private void InsertPlaceholder(string tag, string meaning)
    {
        var item = SelectedItem;
        var isPlainLine = item != null && _itemLabel.Enabled && _itemValue.Enabled && !item.Drives && meaning.Length > 0;
        if (!isPlainLine || ReferenceEquals(_lastItemTextBox, _itemWhen))
        {
            InsertAtCaret(tag);
            return;
        }

        var labelEmpty = string.IsNullOrWhiteSpace(_itemLabel.Text) || _itemLabel.Text.Trim() == NewLineLabel;
        var valueEmpty = string.IsNullOrWhiteSpace(_itemValue.Text);
        if (ReferenceEquals(_lastItemTextBox, _itemLabel))
        {
            if (_itemLabel.Text.Trim() == NewLineLabel)
            {
                _itemLabel.Text = ""; // der Vorgabetext wird ersetzt, nicht ergaenzt
            }
            if (valueEmpty)
            {
                _itemValue.Text = tag;
            }
            InsertAtCaret(meaning);
        }
        else
        {
            if (labelEmpty)
            {
                _itemLabel.Text = meaning;
            }
            InsertAtCaret(tag);
        }
    }

    private void InsertAtCaret(string text)
    {
        if (!_lastItemTextBox.Enabled)
        {
            // Bei Leerraum, Blockende und zentralen Zeilen gibt es kein Textfeld; bei einer Ueberschrift
            // ist "Value" gesperrt. Deutlich sagen, statt scheinbar nichts zu tun.
            var item = SelectedItem;
            var reason = item == null ? "Es ist keine Zeile markiert."
                : item.Spacer ? "Markiert ist ein Leerraum – der hat keinen Text."
                : item.EndBlock ? "Markiert ist ein Blockende – das hat keinen Text."
                : item.IncludeItems ? "Markiert sind die zentralen Zeilen – ihr Inhalt kommt aus der zentralen Datei."
                : $"Das Feld „{(ReferenceEquals(_lastItemTextBox, _itemValue) ? "Value" : ReferenceEquals(_lastItemTextBox, _itemWhen) ? "Nur wenn" : _labelCaption.Text)}“ ist bei dieser Zeile gesperrt.";
            _status.Text = "Zum Einfügen zuerst eine Zeile auswählen und in das Zielfeld klicken.";
            MessageBox.Show(this,
                $"{text} kann hier nicht eingefügt werden.\r\n\r\n{reason}\r\n\r\n" +
                "Eine Textzeile markieren und in das Zielfeld klicken – oder den Platzhalter auf die Liste ziehen: dort entsteht eine neue Zeile.",
                AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _lastItemTextBox.SelectedText = text;
        // Schreibmarke hinter dem eingefuegten Platzhalter halten - beim ersten Fokus per
        // Focus() markiert eine TextBox sonst den ganzen Text.
        var caret = _lastItemTextBox.SelectionStart;
        _lastItemTextBox.Focus();
        _lastItemTextBox.Select(caret, 0);
    }

    /// <summary>Ordner, auf den sich relative Pfade beziehen - bei einer noch ungespeicherten Datei der Ordner des Editors.</summary>
    private string ConfigDirectory => _path != null ? Path.GetDirectoryName(_path)! : Path.GetDirectoryName(Environment.ProcessPath) ?? "";

    /// <summary>Zeigt die Platzhalter, die alle Suchwoerter enthalten (in irgendeiner Spalte, Gross-/Kleinschreibung egal).</summary>
    private void ApplyTagFilter()
    {
        var words = _tagSearch.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var shown = _allTags.Where(item => words.All(word =>
            item.SubItems.Cast<ListViewItem.ListViewSubItem>().Any(cell => cell.Text.Contains(word, StringComparison.OrdinalIgnoreCase)))).ToArray();
        _tags.BeginUpdate();
        _tags.Items.Clear();
        _tags.Items.AddRange(shown);
        _tags.EndUpdate();
        if (words.Length > 0)
        {
            _status.Text = shown.Length == 0 ? $"Kein Platzhalter passt zu „{_tagSearch.Text.Trim()}“." : $"{shown.Length} von {_allTags.Count} Platzhaltern passen zur Suche.";
        }
    }

    private async void LoadTags()
    {
        _tagsStale = false;
        var json = _config.ToJson();
        var baseDirectory = ConfigDirectory;
        try
        {
            var rows = await Task.Run(() =>
            {
                // Eigene Felder zuerst - samt denen der zentralen Datei, wie sie auch zur Laufzeit gelten.
                var config = AppConfig.FromJson(json, "Editor");
                config.BaseDirectory = baseDirectory;
                var local = new HashSet<string>(config.Fields.Keys, StringComparer.OrdinalIgnoreCase);
                try
                {
                    config.ResolveInclude();
                }
                catch
                {
                    // Fehler der zentralen Datei meldet die Vorschau in der Statuszeile
                }

                var resolver = new ValueResolver(config);
                var list = config.Fields.Keys
                    .Where(name => name.Trim().Length > 0)
                    .Select(name => (Tag: $"{{{name.Trim()}}}", Description: local.Contains(name) ? "Eigenes Feld" : "Feld der zentralen Datei", Value: resolver.Expand($"{{{name.Trim()}}}")))
                    .ToList();
                list.AddRange(ValueResolver.BuiltIns
                    .Select(b => (Tag: $"{{{b.Name}}}", b.Description, Value: resolver.Expand($"{{{b.Name}}}"))));
                list.Add(("{env:NAME}", "Umgebungsvariable", "z. B. {env:LOGONSERVER}"));
                list.Add((@"{reg:HKLM\Pfad\Wertname}", "Registry-Wert", ""));
                list.Add(("{wmi:Klasse.Eigenschaft}", "WMI-Eigenschaft", "z. B. {wmi:Win32_TimeZone.Caption}"));
                list.Add(("{wmi:SELECT Eigenschaft FROM Klasse}", "WQL-Abfrage wie in BGInfo", "Schaltfläche „WMI-Abfrage…“"));
                list.Add(("{file:Pfad}", "Erste Zeile einer Datei", ""));
                list.Add(("{filever:Pfad}", "Dateiversion", @"z. B. {filever:%WINDIR%\explorer.exe}"));
                list.Add(("{filetime:Pfad}", "Änderungsdatum einer Datei", ""));
                list.Add(("{cpu,20}", "Kürzen auf 20 Zeichen – geht mit jedem Platzhalter", resolver.Expand("{cpu,20}")));
                list.Add(("{cpu,20...}", "Kürzen mit Auslassungszeichen", resolver.Expand("{cpu,20...}")));
                list.Add(("{serial,-8}", "Nur die letzten 8 Zeichen", resolver.Expand("{serial,-8}")));
                list.Add(("{now:dd.MM.yyyy HH:mm}", "Datum/Uhrzeit mit eigenem Format", resolver.Expand("{now:dd.MM.yyyy HH:mm}")));
                foreach (var (tag, description) in new[]
                         {
                             ("{drive}", "Laufwerksbuchstabe"), ("{volume}", "Datenträgername"), ("{used}", "Belegt in GB"),
                             ("{total}", "Größe in GB"), ("{free}", "Frei in GB"), ("{percent}", "Belegt in Prozent")
                         })
                {
                    list.Add((tag, description, "nur in Laufwerke-Zeilen"));
                }
                return list;
            });

            _allTags.Clear();
            _allTags.AddRange(rows.Select(row => new ListViewItem(new[] { row.Item1, row.Item2, row.Item3 })));
            ApplyTagFilter();
        }
        catch (Exception ex)
        {
            _status.Text = $"Platzhalter konnten nicht ermittelt werden: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------ Laden, Speichern, Anwenden

    private void ReloadControls()
    {
        var wasLoading = _loading;
        _loading = true;
        foreach (var load in _loaders)
        {
            load();
        }
        _loading = wasLoading;
        SyncScaleToggle();
    }

    private ToolStripButton _scaleToggle = null!;
    private bool _syncingScaleToggle;
    // Werte vor dem Einschalten von "100 %", damit das Ausschalten sie zurueckholt.
    private (bool ScaleWithDpi, int ScreenFraction) _scaledSettings = (true, 0);

    private bool IsUnscaled => !_config.Panel.ScaleWithDpi && _config.Panel.ScreenFraction <= 0;

    /// <summary>Der Schalter "100 %" zeigt immer den Zustand der Konfiguration.</summary>
    private void SyncScaleToggle()
    {
        if (_scaleToggle == null)
        {
            return; // Leiste noch nicht aufgebaut
        }
        _syncingScaleToggle = true;
        _scaleToggle.Checked = IsUnscaled;
        _syncingScaleToggle = false;
    }

    private void SetUnscaled(bool unscaled)
    {
        if (_syncingScaleToggle || unscaled == IsUnscaled)
        {
            return;
        }
        if (unscaled)
        {
            _scaledSettings = (_config.Panel.ScaleWithDpi, _config.Panel.ScreenFraction);
            _config.Panel.ScaleWithDpi = false;
            _config.Panel.ScreenFraction = 0;
            _status.Text = "100 %: Windows-Skalierung und relative Breite gelten für das Panel nicht mehr.";
        }
        else
        {
            // Stand vorher beides schon auf "aus" (z.B. so geladen), ist "mit Skalierung" der sinnvolle Gegenzustand.
            var previous = _scaledSettings is { ScaleWithDpi: false, ScreenFraction: <= 0 } ? (true, 0) : _scaledSettings;
            _config.Panel.ScaleWithDpi = previous.Item1;
            _config.Panel.ScreenFraction = previous.Item2;
            _status.Text = "100 % aus: das Panel folgt wieder der Windows-Skalierung bzw. der relativen Breite.";
        }
        ReloadControls();
        Changed();
    }

    private void ShowConfig()
    {
        ReloadControls();
        RefreshItems(0);
        LoadTags();
        UpdateTitle();
        SchedulePreview();
    }

    private void Changed()
    {
        _dirty = true;
        SyncScaleToggle();
        UpdateTitle();
        SchedulePreview();
        ScheduleCurrentValues();
    }

    /// <summary>Liest Aufloesung und Skalierung neu - dieselbe Quelle, mit der BGInfo-Reloaded.exe rendert.</summary>
    private void RefreshDisplayInfo()
    {
        try
        {
            var monitors = WallpaperInterop.GetMonitors();
            var primary = monitors.First(m => m.IsPrimary);
            _displayInfo = $"{primary.Width} × {primary.Height}, Skalierung {primary.Dpi * 100 / 96} %"
                + (monitors.Count > 1 ? $" (primär, {monitors.Count} Monitore)" : "");
            _previewCaption.Text = "Vorschau – ganzer Bildschirm:  " + string.Join("   |   ", monitors.Select(m =>
                $"Monitor {m.Number}{(m.IsPrimary && monitors.Count > 1 ? " (primär)" : "")}: {m.Width} × {m.Height}, Skalierung {m.Dpi * 100 / 96} %"));
        }
        catch
        {
            _displayInfo = "";
            _previewCaption.Text = "Vorschau – ganzer Bildschirm";
        }
    }

    private void UpdateTitle()
    {
        Text = $"{(_path != null ? Path.GetFileName(_path) : "Neue Konfiguration")}{(_dirty ? "*" : "")} – {AppTitle}"
            + (_displayInfo.Length > 0 ? $"   –   Bildschirm: {_displayInfo}" : "");
        // In der Fusszeile nur der Dateiname - der volle Pfad erscheint, wenn die Maus darauf zeigt.
        _fileLabel.Text = (_path != null ? Path.GetFileName(_path) : "Neue Konfiguration (noch nicht gespeichert)") + (_dirty ? "  •  geändert" : "");
        _fileLabel.ToolTipText = _path != null ? $"Geladene Konfigurationsdatei:\r\n{Path.GetFullPath(_path)}" : "";
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty)
        {
            return true;
        }
        var answer = MessageBox.Show(this, "Die Konfiguration wurde geändert. Jetzt speichern?", AppTitle,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer switch
        {
            DialogResult.Yes => Save(false),
            DialogResult.No => true,
            _ => false
        };
    }

    /// <summary>
    /// Start ohne Konfiguration - es wurde keine Datei angegeben (oder die angegebene gibt es nicht),
    /// und weder im Startverzeichnis noch neben der exe liegt eine BGInfo-Reloaded.json. Dann fragen:
    /// Standard-Konfiguration als Datei anlegen, eine vorhandene laden oder ohne Datei beginnen.
    /// </summary>
    private void AskForStartConfiguration(string? requestedFile)
    {
        // Die Standard-Konfiguration kommt immer in den Ordner des Programms - dort sucht auch
        // BGInfo-Reloaded.exe, egal aus welchem Verzeichnis sie gestartet wird.
        var programDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
        string target;
        try
        {
            target = Path.GetFullPath(requestedFile ?? Path.Combine(programDirectory, AppConfig.FileName));
        }
        catch
        {
            target = Path.Combine(programDirectory, AppConfig.FileName);
        }

        var create = new TaskDialogCommandLinkButton("Standard-Konfiguration erstellen",
            $"Legt {target} an – mit allen Einstellungen in ihren Standardwerten und leerer White- und Blacklist. " +
            $"Das gesetzte Wallpaper wird nach {WallpaperState.AdoptedFolder}\\ daneben kopiert und als Hintergrundbild eingetragen.");
        var load = new TaskDialogCommandLinkButton("Vorhandene Konfiguration laden…", "Eine bestehende BGInfo-Reloaded.json auswählen und öffnen.");
        var none = new TaskDialogCommandLinkButton("Ohne Datei beginnen", "Mit den Standardwerten arbeiten und erst beim Speichern einen Ort wählen.");
        var page = new TaskDialogPage
        {
            Caption = AppTitle,
            Heading = "Keine Konfiguration gefunden",
            Text = requestedFile != null
                ? $"Die Datei {target} gibt es nicht."
                : $"Weder im Startverzeichnis noch im Programmordner liegt eine {AppConfig.FileName}.\r\nProgrammordner: {programDirectory}",
            Icon = TaskDialogIcon.Information,
            Buttons = { create, load, none },
            DefaultButton = create,
            AllowCancel = true
        };

        var choice = TaskDialog.ShowDialog(this, page);
        if (choice == create)
        {
            try
            {
                var directory = Path.GetDirectoryName(target)!;
                Directory.CreateDirectory(directory);
                var config = AppConfig.CreateInitial(directory, out var adoptedFrom);
                File.WriteAllText(target, config.ToJson());
                OpenFile(target);
                _status.Text = adoptedFrom != null
                    ? $"Standard-Konfiguration angelegt: {target} – Wallpaper übernommen von {adoptedFrom} als {config.Wallpaper.Path}"
                    : $"Standard-Konfiguration angelegt: {target}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ordner nicht beschreibbar (z.B. unter Programme) - dann den Ort waehlen lassen.
                MessageBox.Show(this, $"Die Datei konnte dort nicht angelegt werden:\r\n{ex.Message}\r\n\r\nBitte einen anderen Ort wählen.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Save(true);
            }
        }
        else if (choice == load)
        {
            using var dialog = new OpenFileDialog { Filter = "BgInfo-Konfiguration (*.json)|*.json|Alle Dateien|*.*", InitialDirectory = Path.GetDirectoryName(target) };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                OpenFile(dialog.FileName);
            }
        }
    }

    /// <summary>
    /// Beim Start: den Update-Ordner der geladenen Konfiguration pruefen - wie BGInfo-Reloaded.exe bei
    /// jedem Lauf. Mit update.auto wird geholt, sonst nur gemeldet, was bereitliegt. Gibt es etwas zu
    /// sagen (geholt, verfuegbar, Ordner nicht erreichbar), erscheint eine Meldung; danach geht es
    /// normal weiter. Ein Fehler hier darf den Editor nie am Start hindern.
    /// </summary>
    private async void CheckUpdateFolderAtStart()
    {
        if (_path == null || string.IsNullOrWhiteSpace(_config.Update.Path))
        {
            return;
        }

        var file = _path;
        var messages = new List<string>();
        FolderUpdateResult? result = null;
        string folder = "";
        bool auto = false;
        try
        {
            // Eigene Kopie: der Hintergrund-Thread liest, waehrend hier schon editiert werden kann.
            var snapshot = AppConfig.FromJson(_config.ToJson(), file);
            auto = snapshot.Update.Auto;
            folder = snapshot.ResolveUpdatePath() ?? "";
            if (folder.Length == 0)
            {
                return;
            }
            result = await Task.Run(() => FolderUpdater.Run(folder, snapshot.Update, checkOnly: !auto, quiet: true, message =>
            {
                lock (messages)
                {
                    messages.Add(message);
                }
            }));
        }
        catch (Exception ex)
        {
            messages.Add($"Update-Prüfung fehlgeschlagen: {ex.Message}");
        }

        if (messages.Count == 0)
        {
            return; // alles aktuell
        }

        // Die Konfiguration neben dem Programm wurde ersetzt: neu laden, sofern es die geoeffnete ist
        // und hier noch nichts geaendert wurde.
        var ownConfig = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", AppConfig.FileName);
        var reloaded = false;
        if (result is { ConfigUpdated: true } && !_dirty && string.Equals(Path.GetFullPath(file), Path.GetFullPath(ownConfig), StringComparison.OrdinalIgnoreCase))
        {
            OpenFile(file);
            reloaded = true;
        }

        var text = $"Update-Ordner: {folder}\r\n\r\n" + string.Join("\r\n", messages);
        if (result is { Reachable: true, Changes: > 0 })
        {
            text += auto
                ? "\r\n\r\n" + (result.SelfUpdated ? "Der Editor selbst wurde aktualisiert – die neue Version ist ab dem nächsten Start aktiv." : "Die Dateien wurden geholt.")
                  + (reloaded ? "\r\nDie Konfiguration wurde neu geladen." : "")
                : "\r\n\r\nEs wurde nichts geholt: „Bei jedem Lauf prüfen und Neueres von selbst holen“ ist ausgeschaltet\r\n(Reiter „Autostart und Werkzeuge“). Von Hand: BGInfo-Reloaded.exe --update";
        }
        _status.Text = messages[^1];

        // Der Editor selbst wurde ersetzt: die neue Version laeuft erst nach einem Neustart - den gleich anbieten.
        if (auto && result is { SelfUpdated: true })
        {
            var restart = new TaskDialogButton("Jetzt neu starten");
            var later = new TaskDialogButton("Später");
            var page = new TaskDialogPage
            {
                Caption = AppTitle + " – Update",
                Heading = "Der Editor wurde aktualisiert",
                Text = text,
                Icon = TaskDialogIcon.Information,
                Buttons = { restart, later },
                DefaultButton = restart,
                AllowCancel = true
            };
            if (TaskDialog.ShowDialog(this, page) == restart)
            {
                RestartEditor();
            }
            return;
        }

        MessageBox.Show(this, text, AppTitle + " – Update", MessageBoxButtons.OK,
            result is { Reachable: true } ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    // Beim Neustart nicht noch einmal nach ungespeicherten Aenderungen fragen - das ist dann schon geklaert.
    private bool _restarting;

    /// <summary>Beendet den Editor und startet ihn mit derselben Konfigurationsdatei neu (nach einem Selbst-Update).</summary>
    private void RestartEditor()
    {
        if (!ConfirmDiscard())
        {
            return;
        }
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            };
            if (_path != null)
            {
                start.ArgumentList.Add(_path);
            }
            System.Diagnostics.Process.Start(start);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Der Neustart ist nicht gelungen:\r\n{ex.Message}\r\n\r\nBitte den Editor von Hand neu starten.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _restarting = true;
        Close();
    }

    private void NewConfig()
    {
        if (ConfirmDiscard())
        {
            _config = new AppConfig();
            _path = null;
            _dirty = false;
            _fileHadComments = false;
            ShowConfig();
        }
    }

    private void OpenFile(string file)
    {
        try
        {
            var json = File.ReadAllText(file);
            _config = AppConfig.FromJson(json, file);
            _path = file;
            _dirty = false;
            // Die Standardkommentare schreibt ToJson bei jedem Speichern neu. Eigene Kommentare kann die
            // Serialisierung nicht erhalten - vor dem ersten Ueberschreiben nachfragen.
            _fileHadComments = AppConfig.HasOwnComments(json);
            ShowConfig();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowConfig();
        }
    }

    private bool Save(bool askForName)
    {
        var target = _path;
        if (askForName || target == null)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "BgInfo-Konfiguration (*.json)|*.json",
                FileName = _path != null ? Path.GetFileName(_path) : AppConfig.FileName,
                InitialDirectory = _path != null ? Path.GetDirectoryName(_path) : null
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }
            target = dialog.FileName;
        }
        else if (_fileHadComments && MessageBox.Show(this,
                     "Diese Datei enthält eigene Kommentare. Der Editor schreibt beim Speichern nur seine Standardkommentare – eigene gehen verloren.\r\n" +
                     "Eigene Anmerkungen lassen sich im Reiter „Datei und Felder“ als Beschreibung eintragen; die bleibt erhalten.\r\n\r\nTrotzdem überschreiben?",
                     AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return false;
        }

        CheckBackgroundFit(Path.GetDirectoryName(Path.GetFullPath(target)) ?? ConfigDirectory);

        // Rechnerlisten bereinigt speichern - auch wenn Doppelte schon in der geladenen Datei standen
        // oder das Feld beim Klick auf "Speichern" noch den Fokus hatte.
        var listEntries = _config.Hosts.Include.Count + _config.Hosts.Exclude.Count;
        _config.Hosts.Include = HostsConfig.Normalize(_config.Hosts.Include);
        _config.Hosts.Exclude = HostsConfig.Normalize(_config.Hosts.Exclude);
        var removedDuplicates = listEntries - _config.Hosts.Include.Count - _config.Hosts.Exclude.Count;
        if (removedDuplicates > 0)
        {
            ReloadControls();
        }

        try
        {
            File.WriteAllText(target, _config.ToJson());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Speichern fehlgeschlagen: {ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _path = target;
        _dirty = false;
        _fileHadComments = false;
        UpdateTitle();
        _status.Text = $"Gespeichert: {target}" + (removedDuplicates > 0 ? $" – {removedDuplicates} doppelte Einträge aus den Rechnerlisten entfernt" : "");
        return true;
    }

    // Fuer welche Lage die Frage nach der Anpassung schon mit "beibehalten" beantwortet wurde.
    private string? _fitQuestionDeclinedFor;

    /// <summary>
    /// Vor dem Speichern: Gibt es fuer die Monitore dieses Rechners ein Hintergrundbild in passender
    /// Aufloesung? Wenn nicht und die gewaehlte Anpassung einen Teil des Ersatzbildes abschneiden
    /// wuerde, darauf hinweisen und die Anpassung auf "alles sichtbar" umstellen (Dehnen oder Anpassen).
    /// </summary>
    private void CheckBackgroundFit(string configDirectory)
    {
        try
        {
            var fit = _config.Wallpaper.Fit;
            // Auf einer Kopie pruefen: relative Pfade gelten relativ zum Ordner, in den gespeichert wird.
            var probe = AppConfig.FromJson(_config.ToJson(), "Editor");
            probe.BaseDirectory = configDirectory;
            var baseImage = ResolveBaseImage(probe);
            if (baseImage == null)
            {
                return;
            }

            foreach (var monitor in WallpaperInterop.GetMonitors())
            {
                var image = probe.Wallpaper.ResolutionVariants
                    ? WallpaperState.ResolveResolutionVariant(baseImage, monitor.Width, monitor.Height)
                    : baseImage;
                if (!File.Exists(image) || WallpaperRenderer.TryGetImageSize(image) is not { } size
                    || !WallpaperRenderer.CutsOffImage(fit, size, monitor))
                {
                    continue;
                }

                var situation = $"{image}|{monitor.Width}x{monitor.Height}|{fit}";
                if (situation == _fitQuestionDeclinedFor)
                {
                    return;
                }

                var wanted = $"{Path.GetFileNameWithoutExtension(baseImage)}_{monitor.Width}x{monitor.Height}{Path.GetExtension(baseImage)}";
                var consequence = fit switch
                {
                    WallpaperFit.None => "Mit der Anpassung „Keine“ skaliert Windows das Bild samt Info-Panel selbst. Weil das Seitenverhältnis nicht zum Bildschirm passt, schneidet Windows dabei einen Teil ab – auch vom Panel. Die Vorschau zeigt das nicht.",
                    WallpaperFit.Fill => "Mit der Anpassung „Ausfüllen“ wird der überstehende Teil des Bildes abgeschnitten.",
                    _ => "Mit der gewählten Anpassung wird das Bild nicht auf den Bildschirm gebracht – ein Teil fehlt oder es bleiben Ränder."
                };
                var answer = MessageBox.Show(this,
                    $"Für diesen Bildschirm ({monitor.Width} × {monitor.Height}) gibt es kein eigenes Hintergrundbild.\r\n\r\n" +
                    (probe.Wallpaper.ResolutionVariants ? $"Gesucht:\t\t{wanted}\r\n" : "Bilder je Auflösung sind abgeschaltet.\r\n") +
                    $"Verwendet wird:\t{Path.GetFileName(image)} ({size.Width} × {size.Height})\r\n" +
                    $"Ordner:\t\t{Path.GetDirectoryName(image)}\r\n\r\n" +
                    consequence + "\r\n\r\n" +
                    "Damit das ganze Bild zu sehen ist, die Anpassung umstellen?\r\n\r\n" +
                    "Ja\t\t= „Dehnen“: füllt den Bildschirm, das Bild wird verzerrt\r\n" +
                    "Nein\t\t= „Anpassen“: unverzerrt, Ränder in der Hintergrundfarbe\r\n" +
                    "Abbrechen\t= Einstellung beibehalten",
                    AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

                if (answer == DialogResult.Cancel)
                {
                    _fitQuestionDeclinedFor = situation;
                }
                else
                {
                    _config.Wallpaper.Fit = answer == DialogResult.Yes ? WallpaperFit.Stretch : WallpaperFit.Fit;
                    ReloadControls();
                    SchedulePreview();
                }
                return; // eine Frage genuegt, auch bei mehreren Monitoren
            }
        }
        catch
        {
            // Die Pruefung ist ein Hinweis - sie darf das Speichern nie verhindern.
        }
    }

    /// <summary>Speichert und laesst die eigentliche Arbeit BGInfo-Reloaded.exe tun - so wird genau der Weg benutzt, der spaeter auch auf den Zielrechnern laeuft.</summary>
    private async void Apply()
    {
        var exe = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "BGInfo-Reloaded.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, $"BGInfo-Reloaded.exe wurde nicht neben dem Editor gefunden:\r\n{exe}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if ((_dirty || _path == null) && !Save(false))
        {
            return;
        }

        try
        {
            _status.Text = "Wende an…";
            using var process = Process.Start(new ProcessStartInfo(exe, $"--config \"{_path}\"") { UseShellExecute = false })
                ?? throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");
            await process.WaitForExitAsync();
            _status.Text = process.ExitCode == 0
                ? "Angewendet – das Wallpaper wurde gesetzt."
                : $"BGInfo-Reloaded.exe meldet einen Fehler (Exit-Code {process.ExitCode}). Details: {RunLog.LogFile}";
        }
        catch (Exception ex)
        {
            _status.Text = $"Anwenden fehlgeschlagen: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------ Vorschau

    /// <summary>Merkt sich je Benutzer, ob der Vorschaubereich eingeblendet ist.</summary>
    private static string PreviewSettingFile => Path.Combine(WallpaperState.StateDirectory, "editor-preview.txt");

    private bool _previewEnabled = ReadPreviewSetting();

    private static bool ReadPreviewSetting()
    {
        try
        {
            return !File.Exists(PreviewSettingFile) || File.ReadAllText(PreviewSettingFile).Trim() != "0";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>Blendet den Vorschaubereich ein oder aus; ausgeblendet wird auch nicht gerendert.</summary>
    private void SetPreviewEnabled(bool enabled)
    {
        _previewEnabled = enabled;
        _split.Panel2Collapsed = !enabled;
        try
        {
            Directory.CreateDirectory(WallpaperState.StateDirectory);
            File.WriteAllText(PreviewSettingFile, enabled ? "1" : "0");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // nur eine Bequemlichkeit - dann gilt die Wahl eben bis zum Schliessen
        }
        if (enabled)
        {
            RenderPreview();
        }
        else
        {
            _previewTimer.Stop();
            _previewGeneration++; // ein noch laufendes Rendern verwerfen
            _status.Text = "Vorschau ausgeblendet – „Vorschau anzeigen“ zeigt die Darstellung weiterhin als Vollbild.";
        }
    }

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        if (_previewEnabled)
        {
            _previewTimer.Start();
        }
    }

    private async void RenderPreview()
    {
        _previewTimer.Stop();
        if (!_previewEnabled)
        {
            _status.Text = "Vorschau ist ausgeblendet – oben „Vorschau“ einschalten.";
            return;
        }
        var generation = ++_previewGeneration;
        // Als Text uebergeben: der Hintergrund-Thread arbeitet auf einer eigenen Kopie, waehrend hier weiter editiert wird.
        var json = _config.ToJson();
        var file = Path.Combine(Path.GetTempPath(), $"BGInfo-Reloaded-Editor-preview-{Environment.ProcessId}-{generation}.jpg");
        _status.Text = "Vorschau wird gerendert…";

        // Relative Pfade gelten relativ zur Konfigurationsdatei - bei einer noch ungespeicherten zum Ordner des Editors.
        var baseDirectory = ConfigDirectory;

        try
        {
            var (note, panel, hostNote) = await Task.Run(() =>
            {
                var config = AppConfig.FromJson(json, "Editor");
                config.BaseDirectory = baseDirectory;
                var includeNote = config.ResolveInclude();
                // Auch die Listendateien nur auf der Kopie lesen - ihre Eintraege gehoeren nicht in die gespeicherte Datei.
                var notes = config.ResolveHostFiles();
                if (includeNote != null)
                {
                    notes.Insert(0, includeNote);
                }
                var (text, rectangle, host) = RenderToFile(config, file);
                return (notes.Count > 0 ? $"{text} – {string.Join(" – ", notes)}" : text, rectangle, host);
            });
            if (generation != _previewGeneration)
            {
                return; // inzwischen wurde weiter editiert, das neuere Bild folgt
            }
            _previewHostNote.Text = hostNote;
            _tips.SetToolTip(_previewHostNote, hostNote);

            Bitmap image;
            using (var stream = File.OpenRead(file))
            using (var loaded = Image.FromStream(stream))
            {
                image = new Bitmap(loaded);
            }

            Bitmap? detail = null;
            if (!panel.IsEmpty)
            {
                var crop = Rectangle.Intersect(Rectangle.Inflate(panel, 24, 24), new Rectangle(Point.Empty, image.Size));
                if (crop.Width > 0 && crop.Height > 0)
                {
                    detail = image.Clone(crop, image.PixelFormat);
                }
            }

            var previous = _preview.Image;
            var previousDetail = _detail.Image;
            _preview.Image = image;
            _detail.Image = detail;
            previous?.Dispose();
            previousDetail?.Dispose();
            FitDetail();
            _status.Text = note;
        }
        catch (Exception ex)
        {
            if (generation == _previewGeneration)
            {
                _status.Text = $"Vorschau nicht möglich: {ex.Message}";
            }
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Temp-Datei, raeumt Windows irgendwann auf
            }
        }
    }

    /// <summary>
    /// Zeigt die aktuelle Darstellung als Vollbild im Vordergrund - ohne Rahmen, Taskleiste und
    /// Desktop, auf dem Bildschirm des Editors und fuer dessen Aufloesung gerendert. Das Wallpaper
    /// des Rechners bleibt unberuehrt. Esc, eine Taste oder ein Klick schliesst.
    /// </summary>
    private async void ShowFullscreenPreview()
    {
        var json = _config.ToJson();
        var baseDirectory = ConfigDirectory;
        var screen = Screen.FromControl(this);
        var monitors = WallpaperInterop.GetMonitors();
        var center = new Point(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
        var monitor = monitors.FirstOrDefault(m => center.X >= m.Left && center.X < m.Left + m.Width && center.Y >= m.Top && center.Y < m.Top + m.Height)
            ?? monitors.First(m => m.IsPrimary);
        var file = Path.Combine(Path.GetTempPath(), $"BGInfo-Reloaded-Editor-fullscreen-{Environment.ProcessId}.jpg");
        _status.Text = "Vollbild-Vorschau wird gerendert…";

        try
        {
            var (note, _, hostNote) = await Task.Run(() =>
            {
                var config = AppConfig.FromJson(json, "Editor");
                config.BaseDirectory = baseDirectory;
                config.ResolveInclude();
                config.ResolveHostFiles();
                return RenderToFile(config, file, monitor);
            });
            if (hostNote.Length > 0)
            {
                note += " – " + hostNote;
            }

            Bitmap image;
            using (var stream = File.OpenRead(file))
            using (var loaded = Image.FromStream(stream))
            {
                image = new Bitmap(loaded);
            }

            using (image)
            using (var view = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = screen.Bounds,
                TopMost = true,
                ShowInTaskbar = false,
                BackColor = Color.Black,
                KeyPreview = true,
                Cursor = Cursors.Hand,
                Text = "Vorschau – " + AppTitle
            })
            {
                // Zoom: das Bild hat genau die Aufloesung des Bildschirms; nur bei Anpassung "Keine"
                // (Originalgroesse des Bildes) wird es hier eingepasst.
                var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = image };
                view.Controls.Add(picture);
                view.KeyDown += (_, _) => view.Close();
                picture.MouseDown += (_, _) => view.Close();
                _status.Text = note + " – Vollbild: Esc, Taste oder Klick schließt";
                view.ShowDialog(this);
            }
            _status.Text = note;
        }
        catch (Exception ex)
        {
            _status.Text = $"Vorschau nicht möglich: {ex.Message}";
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Temp-Datei, raeumt Windows irgendwann auf
            }
        }
    }

    /// <summary>Originalgroesse, solange das Panel in das Feld passt - sonst verkleinern statt abschneiden.</summary>
    private void FitDetail()
    {
        if (_detail.Image is { } image)
        {
            _detail.SizeMode = image.Width > _detail.ClientSize.Width || image.Height > _detail.ClientSize.Height
                ? PictureBoxSizeMode.Zoom
                : PictureBoxSizeMode.CenterImage;
        }
    }

    /// <summary>
    /// Rendert die Vorschau. Das Panel wird immer gezeichnet - auch wenn dieser Rechner laut White-
    /// oder Blacklist keins bekaeme: wer die Konfiguration auf einem ausgeschlossenen Rechner pflegt,
    /// muss trotzdem sehen, was er einstellt. "HostNote" sagt dann, was die exe hier wirklich taete.
    /// </summary>
    private static (string Note, Rectangle Panel, string HostNote) RenderToFile(AppConfig config, string file, MonitorTarget? target = null)
    {
        var monitor = target ?? WallpaperInterop.GetMonitors().FirstOrDefault(m => m.IsPrimary) ?? WallpaperInterop.GetMonitors().First();
        var hostNames = new[] { Environment.MachineName, new ValueResolver().Expand("{fqdn}") };
        var allowed = config.Hosts.Allows(hostNames, out var reason);
        var lines = SystemInfoCollector.Collect(config);

        var image = ResolveBaseImage(config);
        if (image != null && config.Wallpaper.ResolutionVariants)
        {
            image = WallpaperState.ResolveResolutionVariant(image, monitor.Width, monitor.Height);
        }
        if (image != null && !File.Exists(image))
        {
            image = null; // nur Bilder fuer andere Aufloesungen vorhanden
        }

        var panel = WallpaperRenderer.Render(lines, file, image, config, monitor);

        var note = $"Vorschau für {monitor}";
        note += image != null ? $" – Hintergrund: {Path.GetFileName(image)}" : " – kein Hintergrundbild gefunden";
        if (image != null && WallpaperRenderer.TryGetImageSize(image) is { } size && WallpaperRenderer.CutsOffImage(config.Wallpaper.Fit, size, monitor))
        {
            // Bei None zeigt die Vorschau das unskalierte Bild - auf dem Desktop skaliert Windows es noch.
            note += config.Wallpaper.Fit == WallpaperFit.None
                ? $" ({size.Width} × {size.Height}) – ACHTUNG: Anpassung „Keine“, Windows skaliert und beschneidet das Bild samt Panel selbst; die Vorschau zeigt das nicht"
                : $" ({size.Width} × {size.Height}) – Achtung: passt nicht zum Bildschirm, ein Teil des Bildes ist nicht zu sehen";
        }
        var hostNote = allowed ? ""
            : $"Achtung: {Environment.MachineName} {reason} – " + (config.Hosts.OnExcluded == ExcludedAction.Skip
                ? "BGInfo-Reloaded.exe ändert hier nichts. Die Vorschau zeigt das Panel trotzdem."
                : "BGInfo-Reloaded.exe setzt hier nur den Hintergrund, ohne Panel. Die Vorschau zeigt das Panel trotzdem.");
        return (note, panel, hostNote);
    }

    /// <summary>
    /// Wie BGInfo-Reloaded.exe: festes Bild aus der Konfiguration, sonst das gemerkte Original, sonst das
    /// aktuelle Wallpaper - aber ohne dabei original.txt zu schreiben (der Editor soll nichts am
    /// Zustand des Rechners veraendern).
    /// </summary>
    private static string? ResolveBaseImage(AppConfig config)
    {
        var configured = config.ResolveWallpaperPath();
        if (configured != null && (File.Exists(configured) || (config.Wallpaper.ResolutionVariants && WallpaperState.HasAnyResolutionVariant(configured))))
        {
            return configured;
        }

        return WallpaperState.PeekOriginalWallpaper();
    }
}
