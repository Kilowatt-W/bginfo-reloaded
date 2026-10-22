namespace BgInfoReloaded.Editor;

/// <summary>
/// Zustand des Autostarts: immer sichtbar in der Fusszeile, ausfuehrlich im Reiter "Autostart und
/// Werkzeuge". Gelesen wird aus der Aufgabenplanung (<see cref="AppTaskScheduler.QueryStatus"/>) -
/// auf einem eigenen Thread, weil schtasks und WMI einen Moment brauchen.
/// </summary>
public sealed partial class MainForm
{
    private readonly ToolStripStatusLabel _autostartLabel = new()
    {
        BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(6, 0, 6, 0), IsLink = true,
        LinkBehavior = LinkBehavior.HoverUnderline, Text = "Autostart: …"
    };
    private readonly Label _autostartHeadline = new() { AutoSize = true, Margin = new Padding(3, 4, 3, 2), Text = "Autostart wird geprüft…" };
    private readonly Label _autostartDetails = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 6) };
    private readonly System.Windows.Forms.Timer _autostartTimer = new() { Interval = 120_000 };
    private bool _autostartPresetDone;
    private int _autostartGeneration;

    /// <summary>Zustandsanzeige oben im Reiter "Autostart und Werkzeuge".</summary>
    private Control BuildAutostartStatus()
    {
        _autostartHeadline.Font = new Font(Font, FontStyle.Bold);
        var check = new Button { Text = "Erneut prüfen", AutoSize = true, Margin = new Padding(0, 2, 0, 6) };
        check.Click += (_, _) => RefreshAutostartStatus();
        _tips.SetToolTip(check, "Den Autostart in der Aufgabenplanung erneut nachsehen (auch nach Änderungen außerhalb des Editors).");
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        panel.Controls.Add(_autostartHeadline);
        panel.Controls.Add(_autostartDetails);
        panel.Controls.Add(check);
        return panel;
    }

    /// <summary>Fusszeile: Klick fuehrt zum Reiter; regelmaessig nachsehen, falls der Task anderswo geaendert wird.</summary>
    private void SetUpAutostartLabel(TabControl tabs, TabPage toolsPage)
    {
        _autostartLabel.ToolTipText = "Zustand des Autostarts – Klick öffnet den Reiter „Autostart und Werkzeuge“.";
        _autostartLabel.Click += (_, _) => tabs.SelectedTab = toolsPage;
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab == toolsPage)
            {
                RefreshAutostartStatus();
            }
        };
        _autostartTimer.Tick += (_, _) => RefreshAutostartStatus();
        _autostartTimer.Start();
    }

    private async void RefreshAutostartStatus()
    {
        var generation = ++_autostartGeneration;
        AppTaskScheduler.AutostartStatus status;
        try
        {
            status = await Task.Run(AppTaskScheduler.QueryStatus);
        }
        catch
        {
            return; // QueryStatus wirft nicht - zur Sicherheit trotzdem
        }
        if (generation != _autostartGeneration || IsDisposed)
        {
            return;
        }

        var color = status.Active ? Color.FromArgb(0, 128, 0) : status.Logon != null || status.Refresh != null ? Color.FromArgb(190, 90, 0) : SystemColors.GrayText;
        _autostartLabel.Text = status.Summary;
        _autostartLabel.LinkColor = _autostartLabel.ActiveLinkColor = _autostartLabel.VisitedLinkColor = color;
        _autostartHeadline.Text = status.Active ? "Autostart ist aktiv" : status.Logon != null || status.Refresh != null ? "Autostart ist eingerichtet, aber deaktiviert" : "Autostart ist nicht eingerichtet";
        _autostartHeadline.ForeColor = color;

        var lines = status.Details().ToList();
        var task = status.Logon ?? status.Refresh;
        if (task != null)
        {
            // Abweichungen von dem, was der Editor gerade zeigt, deutlich sagen.
            var ownExe = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", FolderUpdater.MainExe);
            if (!SamePath(task.Command, ownExe))
            {
                lines.Add($"Hinweis: der Autostart startet ein anderes Programm als das neben diesem Editor ({ownExe}).");
            }
            if (_path != null && task.Config != null && !AppConfig.IsUrl(task.Config) && !SamePath(task.Config, _path))
            {
                lines.Add("Hinweis: der Autostart verwendet eine andere Konfigurationsdatei als die geöffnete.");
            }
            if (status.Logon is { Watch: true } && !status.WatchRunning)
            {
                lines.Add("Die Überwachung (--watch) startet mit der nächsten Anmeldung wieder.");
            }
        }
        _autostartDetails.Text = string.Join(Environment.NewLine, lines);
        _autostartLabel.ToolTipText = string.Join(Environment.NewLine, lines) + Environment.NewLine + "Klick öffnet den Reiter „Autostart und Werkzeuge“.";

        // Beim ersten Mal die Eingabefelder so vorbelegen, wie der Autostart eingerichtet ist.
        if (!_autostartPresetDone && task != null)
        {
            _autostartPresetDone = true;
            _taskWatch.Checked = status.Logon is { Watch: true };
            _taskInterval.Value = status.Refresh is { Interval: { } every } ? Math.Clamp((decimal)every.TotalMinutes, _taskInterval.Minimum, _taskInterval.Maximum) : 0;
        }
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
