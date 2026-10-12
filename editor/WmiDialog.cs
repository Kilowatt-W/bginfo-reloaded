namespace BgInfoReloaded.Editor;

/// <summary>
/// WMI-Abfrage zusammenstellen, angelehnt an "Custom &gt; WMI Query" im Original-BGInfo: Klasse waehlen,
/// Eigenschaft aus der Liste mit den aktuellen Werten uebernehmen, bei Bedarf eingrenzen - oder den
/// Platzhalter von Hand schreiben, auch als ganze WQL-Abfrage ({wmi:SELECT ... FROM ...}).
/// </summary>
public sealed class WmiDialog : Form
{
    private readonly ComboBox _class = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly TextBox _where = new() { Dock = DockStyle.Fill, PlaceholderText = "optional, z. B. IPEnabled=true" };
    private readonly ListView _properties = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly TextBox _tag = new() { Dock = DockStyle.Fill };
    private readonly TextBox _result = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private int _generation;

    /// <summary>Der fertige Platzhalter, wenn der Dialog mit "Einfügen" geschlossen wurde.</summary>
    public string Placeholder => _tag.Text.Trim();

    public WmiDialog()
    {
        Text = "WMI-Abfrage";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Size = new Size(LogicalToDeviceUnits(820), LogicalToDeviceUnits(640));
        MinimumSize = new Size(600, 420);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;

        var load = new Button { Text = "Eigenschaften laden", AutoSize = true };
        var evaluate = new Button { Text = "Auswerten", AutoSize = true };
        var ok = new Button { Text = "Einfügen", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        CancelButton = cancel;

        _properties.Columns.Add("Eigenschaft", 220);
        _properties.Columns.Add("Wert auf diesem Rechner (erster Treffer)", 480);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(10) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        static Label Caption(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 6) };

        table.Controls.Add(Caption("WMI-Klasse"), 0, 0);
        table.Controls.Add(_class, 1, 0);
        table.Controls.Add(load, 2, 0);
        table.Controls.Add(Caption("Bedingung (where)"), 0, 1);
        table.Controls.Add(_where, 1, 1);
        table.Controls.Add(_properties, 0, 2);
        table.SetColumnSpan(_properties, 3);
        table.Controls.Add(Caption("Platzhalter"), 0, 3);
        table.Controls.Add(_tag, 1, 3);
        table.Controls.Add(evaluate, 2, 3);
        table.Controls.Add(Caption("Ergebnis"), 0, 4);
        table.Controls.Add(_result, 1, 4);
        table.SetColumnSpan(_result, 2);
        var hint = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 6, 3, 6),
            Text = "Der Platzhalter lässt sich von Hand ändern. Abfragen aus BGInfo passen direkt hinein:\r\n{wmi:SELECT Caption FROM Win32_OperatingSystem}"
        };
        table.Controls.Add(hint, 0, 5);
        table.SetColumnSpan(hint, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(_status);
        table.Controls.Add(buttons, 0, 6);
        table.SetColumnSpan(buttons, 3);
        Controls.Add(table);

        load.Click += (_, _) => LoadProperties();
        _class.SelectionChangeCommitted += (_, _) => BeginInvoke(LoadProperties);
        _class.KeyDown += (_, e) => LoadOnEnter(e);
        _where.KeyDown += (_, e) => LoadOnEnter(e);
        _properties.SelectedIndexChanged += (_, _) =>
        {
            if (_properties.SelectedItems.Count > 0)
            {
                _tag.Text = (string)_properties.SelectedItems[0].Tag!;
                _result.Text = _properties.SelectedItems[0].SubItems[1].Text;
            }
        };
        _properties.DoubleClick += (_, _) =>
        {
            if (_properties.SelectedItems.Count > 0)
            {
                DialogResult = DialogResult.OK;
            }
        };
        evaluate.Click += (_, _) => Evaluate();
        _tag.TextChanged += (_, _) => ok.Enabled = Placeholder.Length > 0;
        ok.Enabled = false;
        Load += (_, _) => LoadClasses();
    }

    private void LoadOnEnter(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            LoadProperties();
        }
    }

    private async void LoadClasses()
    {
        _status.Text = "Lese WMI-Klassen…";
        try
        {
            var classes = await Task.Run(ValueResolver.ListWmiClasses);
            if (IsDisposed)
            {
                return;
            }
            var typed = _class.Text;
            _class.Items.AddRange(classes.Cast<object>().ToArray());
            _class.Text = typed;
            _status.Text = $"{classes.Count} Klassen";
        }
        catch (Exception ex)
        {
            _status.Text = $"Klassenliste nicht lesbar: {ex.Message}";
        }
    }

    private async void LoadProperties()
    {
        var className = _class.Text.Trim();
        var where = _where.Text.Trim();
        if (className.Length == 0)
        {
            return;
        }

        var generation = ++_generation;
        _status.Text = $"Frage {className} ab…";
        try
        {
            var rows = await Task.Run(() => ValueResolver.ListWmiClass(className, where));
            if (IsDisposed || generation != _generation)
            {
                return;
            }

            _properties.BeginUpdate();
            _properties.Items.Clear();
            foreach (var (tag, value) in rows)
            {
                // Aus "{wmi:Klasse.Eigenschaft where ...}" nur den Eigenschaftsnamen anzeigen.
                var name = tag[(tag.IndexOf('.') + 1)..].TrimEnd('}');
                var space = name.IndexOf(' ');
                _properties.Items.Add(new ListViewItem(new[] { space > 0 ? name[..space] : name, value.ReplaceLineEndings(" ") }) { Tag = tag });
            }
            _properties.EndUpdate();
            _status.Text = rows.Count > 0 ? $"{rows.Count} Eigenschaften" : "Kein Treffer – Klasse oder Bedingung prüfen";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && generation == _generation)
            {
                _properties.Items.Clear();
                _status.Text = $"Abfrage fehlgeschlagen: {ex.Message}";
            }
        }
    }

    private async void Evaluate()
    {
        var tag = Placeholder;
        _result.Text = "…";
        var value = await Task.Run(() => new ValueResolver().Expand(tag));
        if (!IsDisposed && tag == Placeholder)
        {
            _result.Text = value;
        }
    }
}
