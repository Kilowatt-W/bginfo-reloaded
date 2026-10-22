using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BgInfoReloaded;
using Microsoft.Win32;

[SupportedOSPlatform("windows")]
static class EntryPoint
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern int GetFileType(IntPtr hFile);

    private const int STD_OUTPUT_HANDLE = -11;
    private const int FILE_TYPE_UNKNOWN = 0;

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    private struct INPUT_RECORD
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public int KeyDown;
        [FieldOffset(8)] public ushort RepeatCount;
        [FieldOffset(10)] public ushort VirtualKeyCode;
        [FieldOffset(12)] public ushort VirtualScanCode;
        [FieldOffset(14)] public char UnicodeChar;
        [FieldOffset(16)] public uint ControlKeyState;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string fileName, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WriteConsoleInput(IntPtr consoleInput, INPUT_RECORD[] buffer, uint length, out uint written);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static bool _attachedToParentConsole;

    static int Main(string[] args)
    {
        ValueFormat.PinInvariantCulture();
        var exitCode = MainCore(args);
        ReleaseParentConsole();
        return exitCode;
    }

    /// <summary>
    /// Die Shell wartet nicht auf eine fensterlose exe: ihr Prompt steht schon da, bevor unsere Ausgabe
    /// kommt, danach blinkt die Schreibmarke in einer leeren Zeile und es sieht aus, als haenge das
    /// Programm, bis man Enter drueckt. Deshalb am Ende ein Enter in den Eingabepuffer der Konsole
    /// legen - die Shell zeichnet daraufhin ihren Prompt neu. Nur, wenn wir uns an eine fremde Konsole
    /// angehaengt haben (nicht bei Umleitung, Task oder Doppelklick).
    /// </summary>
    private static void ReleaseParentConsole()
    {
        if (!_attachedToParentConsole)
        {
            return;
        }

        try
        {
            Console.Out.Flush();
            Console.Error.Flush();

            const uint GENERIC_READ_WRITE = 0xC0000000;
            const uint FILE_SHARE_READ_WRITE = 3;
            const uint OPEN_EXISTING = 3;
            var input = CreateFile("CONIN$", GENERIC_READ_WRITE, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (input == IntPtr.Zero || input == new IntPtr(-1))
            {
                return;
            }

            const ushort KEY_EVENT = 1;
            const ushort VK_RETURN = 0x0D;
            var enter = new INPUT_RECORD { EventType = KEY_EVENT, KeyDown = 1, RepeatCount = 1, VirtualKeyCode = VK_RETURN, VirtualScanCode = 0x1C, UnicodeChar = '\r' };
            var release = enter;
            release.KeyDown = 0;
            WriteConsoleInput(input, new[] { enter, release }, 2, out _);
            CloseHandle(input);
        }
        catch
        {
            // reine Kosmetik
        }
    }

    private static int MainCore(string[] args)
    {
        // Fensterlose exe (OutputType=WinExe): aus einer Konsole gestartet haengen wir uns an diese an,
        // damit Hilfe/Fehlermeldungen sichtbar bleiben. Muss vor der ersten Console-Nutzung passieren.
        // Ohne aufrufende Konsole (Scheduled Task, Doppelklick) schlaegt das fehl und die Ausgabe
        // verpufft einfach - genau das gewuenschte Verhalten.
        // Ist stdout bereits umgeleitet (Datei/Pipe), NICHT anhaengen: AttachConsole wuerde die
        // geerbten Handles durch die Konsole ersetzen und die Umleitung bliebe leer.
        var stdout = GetStdHandle(STD_OUTPUT_HANDLE);
        if (stdout == IntPtr.Zero || stdout == new IntPtr(-1) || GetFileType(stdout) == FILE_TYPE_UNKNOWN)
        {
            _attachedToParentConsole = AttachConsole(ATTACH_PARENT_PROCESS);
        }
        else
        {
            // Umgeleitete Ausgabe (Datei/Pipe) als UTF-8 schreiben - sonst landen Umlaute in der
            // ANSI-Codepage des Rechners in der Datei. Bewusst ueber eigene Writer statt
            // Console.OutputEncoding: das wuerde die Codepage einer Konsole umstellen wollen, die es
            // hier gar nicht gibt.
            var utf8 = new System.Text.UTF8Encoding(false);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
        }

        Console.WriteLine($"BGInfo-Reloaded {GitHubUpdater.CurrentVersion} - eigenstaendiger, quelloffener BGInfo-Ersatz");

        if (args.Contains("--version") || args.Contains("-v"))
        {
            // Versionsnummer wurde oben schon ausgegeben - hier reicht ein sauberer Exit.
            return 0;
        }

        if (args.Contains("--help") || args.Contains("-h") || args.Contains("/?"))
        {
            PrintHelp();
            return 0;
        }

        // Vor jeder Abfrage von Aufloesung/DPI - sonst liefert Windows bei Skalierung virtuelle Pixel.
        WallpaperInterop.EnableDpiAwareness();

        if (args.Contains("--list-tags"))
        {
            // Reine Auskunft, bewusst ohne Logdatei (die Liste wuerde das Log nur aufblaehen).
            try
            {
                TagList.Print(GetStringOption(args, "--list-tags"), GetStringOption(args, "--config"));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fehler: {ex.Message}");
                return 1;
            }
        }

        RunLog.Start(args);
        Console.WriteLine($"Version {GitHubUpdater.CurrentVersion}, Log: {RunLog.LogFile}");
        var exitCode = Run(args);
        RunLog.Finish(exitCode);
        return exitCode;
    }

    private static int Run(string[] args)
    {
        try
        {
            var configSource = GetStringOption(args, "--config");

            if (args.Contains("--export-config"))
            {
                ExportConfig(GetStringOption(args, "--export-config"), configSource, args.Contains("--with-placeholders"));
                return 0;
            }

            if (args.Contains("--register-task"))
            {
                var minutes = GetIntOption(args, "--interval-minutes", 30);
                AppTaskScheduler.Register(minutes, configSource, args.Contains("--watch"));
                return 0;
            }

            if (args.Contains("--unregister-task"))
            {
                AppTaskScheduler.Unregister();
                return 0;
            }

            if (args.Contains("--task-status"))
            {
                var status = AppTaskScheduler.QueryStatus();
                Console.WriteLine(status.Summary);
                foreach (var line in status.Details())
                {
                    Console.WriteLine($"  {line}");
                }
                return status.Active ? 0 : 1;
            }

            if (args.Contains("--update"))
            {
                var repo = GetStringOption(args, "--repo");
                var checkOnly = args.Contains("--check-only");
                // Quelle: ausdruecklich --repo (GitHub) oder --from (Ordner), sonst der Ordner aus
                // "update.path" der Konfiguration, sonst das frueher gemerkte GitHub-Repository.
                // Reihenfolge ohne Angabe: Ordner aus "update.path", dann "update.repo", dann das frueher
                // mit --repo gemerkte Repository. Was aus einem Ordner geholt wird, steht in "update".
                var folder = GetStringOption(args, "--from");
                var (updateConfig, _) = AppConfig.LoadWithoutInclude(configSource);
                if (repo == null)
                {
                    folder ??= updateConfig.ResolveUpdatePath();
                }
                if (repo == null && folder != null)
                {
                    FolderUpdater.Run(folder, updateConfig.Update, checkOnly, quiet: false);
                    return 0;
                }
                var configuredRepo = string.IsNullOrWhiteSpace(updateConfig.Update.Repo) ? null : updateConfig.Update.Repo.Trim();
                GitHubUpdater.RunAsync(repo, checkOnly, configuredRepo).GetAwaiter().GetResult();
                return 0;
            }

            if (args.Contains("--update-source"))
            {
                var url = GetStringOption(args, "--update-source")
                    ?? throw new ArgumentException("--update-source braucht eine Git-URL, z.B. --update-source https://github.com/org/repo.git");
                var branch = GetStringOption(args, "--branch") ?? "main";
                GitSourceUpdater.Run(url, branch);
                return 0;
            }

            var previewPath = GetStringOption(args, "--preview");
            var preview = args.Contains("--preview");
            if (preview && previewPath == null)
            {
                throw new ArgumentException("--preview braucht eine Zieldatei, z.B. --preview vorschau.png");
            }

            if (args.Contains("--watch"))
            {
                Watch(configSource, previewPath, preview);
                return 0;
            }

            RunOnce(configSource, previewPath, preview);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Rendert einmal und bleibt dann aktiv: aendert sich Aufloesung oder Skalierung des primaeren
    /// Monitors, wird neu gerendert. Windows meldet das per WM_DISPLAYCHANGE (hier ueber SystemEvents);
    /// zusaetzlich wird alle 15 Sekunden nachgesehen, weil eine reine Skalierungsaenderung dieses
    /// Ereignis nicht zuverlaessig ausloest. Gerendert wird nur, wenn sich der Zustand wirklich
    /// geaendert hat. Endet mit der Abmeldung des Benutzers.
    /// </summary>
    private static void Watch(string? configSource, string? previewPath, bool preview)
    {
        // "Local\": eine Instanz je Benutzersitzung - der Logon-Task darf keine zweite starten.
        using var instance = new Mutex(true, @"Local\BGInfo-Reloaded.Watch", out var isFirst);
        if (!isFirst)
        {
            Console.WriteLine("In dieser Sitzung laeuft bereits eine --watch-Instanz - nichts zu tun.");
            return;
        }

        using var displayChanged = new AutoResetEvent(false);
        SystemEvents.DisplaySettingsChanged += (_, _) => displayChanged.Set();

        var rendered = WallpaperInterop.GetDisplayState();
        Console.WriteLine($"Ueberwache Aufloesung und Skalierung (aktuell {rendered}).");
        RenderForWatch(configSource, previewPath, preview);

        while (true)
        {
            if (displayChanged.WaitOne(TimeSpan.FromSeconds(15)))
            {
                // Ein Wechsel loest oft mehrere Ereignisse aus (Monitor an/ab, Docking) - kurz setzen lassen.
                Thread.Sleep(1500);
                displayChanged.Reset();
            }

            var current = WallpaperInterop.GetDisplayState();
            if (current != rendered)
            {
                Console.WriteLine($"Anzeige geaendert: {rendered} -> {current}. Rendere neu.");
                rendered = current;
                RenderForWatch(configSource, previewPath, preview);
            }
        }
    }

    /// <summary>Ein fehlgeschlagener Lauf (z.B. Share gerade nicht erreichbar) darf die Ueberwachung nicht beenden.</summary>
    private static void RenderForWatch(string? configSource, string? previewPath, bool preview)
    {
        try
        {
            RunOnce(configSource, previewPath, preview);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
        }
    }

    private static void RunOnce(string? configSource, string? previewPath, bool preview)
    {
        // --watch-Instanz und Refresh-Task koennen gleichzeitig laufen - nacheinander statt gegeneinander,
        // sonst schreiben beide dieselbe Bilddatei.
        using var renderLock = new Mutex(false, @"Local\BGInfo-Reloaded.Render");
        var locked = false;
        try
        {
            try
            {
                locked = renderLock.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                locked = true;
            }
            RunOnceLocked(configSource, previewPath, preview);
        }
        finally
        {
            if (locked)
            {
                renderLock.ReleaseMutex();
            }
        }
    }

    private static void RunOnceLocked(string? configSource, string? previewPath, bool preview)
    {
        ExecutableSwap.CleanupOldBinary();

        // Erster Lauf ohne jede Konfiguration: das gesetzte Wallpaper uebernehmen und eine
        // BGInfo-Reloaded.json neben der exe anlegen. Nicht bei --preview - das aendert nichts am Rechner.
        if (!preview && string.IsNullOrWhiteSpace(configSource))
        {
            AppConfig.TryCreateInitialFile();
        }

        var (config, loadedFrom) = AppConfig.Load(configSource);
        Console.WriteLine($"Konfiguration: {loadedFrom}");

        // Automatisches Selbst-Update aus dem konfigurierten Ordner. Die neue Version wird erst beim
        // naechsten Start aktiv; ein Fehler dabei (Share weg, keine Schreibrechte) darf den Lauf nicht
        // verhindern.
        if (!preview && config.Update.Auto && config.ResolveUpdatePath() is { } updateFolder)
        {
            try
            {
                FolderUpdater.Run(updateFolder, config.Update, checkOnly: false, quiet: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warnung: automatisches Update aus '{updateFolder}' fehlgeschlagen: {ex.Message}");
            }
        }

        // White-/Blacklist: auf ausgeschlossenen Rechnern kein Panel - je nach Einstellung nur der
        // Hintergrund oder gar keine Aenderung.
        var hostNames = new[] { Environment.MachineName, new ValueResolver().Expand("{fqdn}") };
        var panelAllowed = config.Hosts.Allows(hostNames, out var excludedReason);
        if (!panelAllowed)
        {
            Console.WriteLine($"Info-Panel auf diesem Rechner abgeschaltet: {Environment.MachineName} {excludedReason}.");
            if (config.Hosts.OnExcluded == ExcludedAction.Skip)
            {
                Console.WriteLine("hosts.onExcluded = Skip - das Wallpaper bleibt unveraendert.");
                return;
            }
        }

        List<InfoLine> lines;
        if (panelAllowed)
        {
            Console.WriteLine("Sammle Systeminformationen...");
            lines = SystemInfoCollector.Collect(config);
        }
        else
        {
            lines = new List<InfoLine>();
        }

        Console.WriteLine("Ermittle Original-Wallpaper...");
        var baseImage = ResolveBaseImage(config);
        Console.WriteLine(baseImage != null
            ? $"Standardbild: {baseImage}"
            : "Kein bestehendes Wallpaper gefunden - erzeuge einheitlichen Hintergrund.");

        // Single: ein Bild fuer den primaeren Monitor, Windows zeigt es auf allen. PerMonitor: je Monitor ein Bild.
        var perMonitor = config.Monitors.Mode == MonitorMode.PerMonitor;
        var monitors = WallpaperInterop.GetMonitors();
        if (!perMonitor)
        {
            monitors = monitors.Where(m => m.IsPrimary).Take(1).ToList();
        }

        if (preview)
        {
            foreach (var line in lines)
            {
                Console.WriteLine(line.Separator ? "  " + new string('-', 30) : line.Kind == LineKind.Section ? $"  {line.Label}" : $"    {line.Label,-20} {line.Value}");
            }
        }

        var noLines = new List<InfoLine>();
        var rendered = new List<(MonitorTarget Monitor, string ImagePath)>();
        foreach (var monitor in monitors)
        {
            var (image, choice) = config.ResolveWallpaperFor(baseImage, monitor.Width, monitor.Height);
            var withPanel = !perMonitor || config.Monitors.Panel == PanelMonitors.All || monitor.IsPrimary;

            string outputPath;
            if (preview)
            {
                // Der primaere Monitor bekommt den angegebenen Namen, weitere "<name>_m2.png" usw.
                var previewFile = Path.GetFullPath(previewPath!);
                outputPath = monitor.IsPrimary
                    ? previewFile
                    : Path.Combine(Path.GetDirectoryName(previewFile)!,
                        $"{Path.GetFileNameWithoutExtension(previewFile)}_m{monitor.Number}{Path.GetExtension(previewFile)}");
            }
            else
            {
                outputPath = perMonitor ? WallpaperState.NewMonitorOutputPath(monitor.Number) : WallpaperState.OutputImagePath;
            }

            // Immer ausdruecklich nennen, welches Bild es wurde - und wenn es das Standardbild ist, auch
            // nach welchem Dateinamen vergeblich gesucht wurde. Sonst ist von aussen nicht erkennbar,
            // ob die Variante fehlt, falsch heisst oder die Option aus ist.
            string imageNote;
            var size = $"{monitor.Width}x{monitor.Height}";
            if (image == null)
            {
                // Mit Standardbild, aber ohne Treffer: es gibt nur Bilder fuer andere Aufloesungen bzw. gar keins.
                imageNote = baseImage == null
                    ? ", ohne Hintergrundbild"
                    : $", ohne Hintergrundbild (weder {Path.GetFileNameWithoutExtension(WallpaperState.StripResolutionSuffix(baseImage))}_{size}{Path.GetExtension(baseImage)} noch {Path.GetFileName(baseImage)} vorhanden)";
            }
            else
            {
                var wanted = baseImage == null ? "" : $"{Path.GetFileNameWithoutExtension(WallpaperState.StripResolutionSuffix(baseImage))}_{size}{Path.GetExtension(baseImage)}";
                imageNote = $", Hintergrund {Path.GetFileName(image)} " + choice switch
                {
                    WallpaperChoice.Override => $"(eigene Zuordnung fuer {size} aus wallpaper.byResolution)",
                    WallpaperChoice.ExactVariant => "(eigenes Bild fuer diese Aufloesung)",
                    WallpaperChoice.DefaultVariant => $"(Standardbild: kein {Path.GetFileName(baseImage)}, deshalb die Fassung mit 1920x1080 im Namen; kein eigenes Bild fuer {size})",
                    WallpaperChoice.BestFit => $"(kein Bild fuer {size} und kein {Path.GetFileName(baseImage)} - am besten passendes Bild gewaehlt)",
                    _ when config.Wallpaper.ResolutionVariants => $"(kein {wanted} im selben Ordner gefunden)",
                    _ => "(wallpaper.resolutionVariants ist aus)"
                };
            }
            Console.WriteLine($"Rendere {monitor}{(withPanel ? "" : ", ohne Info-Panel")}{imageNote}");
            if (image != null && WallpaperRenderer.TryGetImageSize(image) is { } imageSize
                && WallpaperRenderer.CutsOffImage(config.Wallpaper.Fit, imageSize, monitor))
            {
                Console.WriteLine(config.Wallpaper.Fit == WallpaperFit.None
                    ? $"Hinweis: {Path.GetFileName(image)} ist {imageSize.Width}x{imageSize.Height}, der Monitor {monitor.Width}x{monitor.Height}. Bei wallpaper.fit = None skaliert Windows das Bild samt Panel selbst und schneidet je nach eigener Einstellung einen Teil ab - mit Fit oder Stretch bleibt alles sichtbar."
                    : $"Hinweis: {Path.GetFileName(image)} ist {imageSize.Width}x{imageSize.Height}, der Monitor {monitor.Width}x{monitor.Height}. Bei wallpaper.fit = {config.Wallpaper.Fit} ist nicht das ganze Bild zu sehen - mit Fit oder Stretch bleibt alles sichtbar.");
            }
            WallpaperRenderer.Render(withPanel ? lines : noLines, outputPath, image, config, monitor);
            rendered.Add((monitor, outputPath));
        }

        if (preview)
        {
            Console.WriteLine($"Vorschau gespeichert (Wallpaper NICHT geaendert): {string.Join(", ", rendered.Select(r => r.ImagePath))}");
            return;
        }

        Console.WriteLine("Setze Wallpaper...");
        var primaryImage = rendered.First(r => r.Monitor.IsPrimary).ImagePath;
        if (perMonitor)
        {
            try
            {
                var assigned = WallpaperInterop.SetWallpaperPerMonitor(rendered);
                if (assigned == 0)
                {
                    throw new InvalidOperationException("kein Monitor zugeordnet");
                }
            }
            catch (Exception ex)
            {
                // Shell-Schnittstelle nicht verfuegbar (z.B. Server Core) - dann wie im Single-Modus.
                Console.WriteLine($"Warnung: Wallpaper je Monitor nicht moeglich ({ex.Message}) - setze ein Bild fuer alle Monitore.");
                WallpaperInterop.SetWallpaper(primaryImage);
            }
        }
        else
        {
            WallpaperInterop.SetWallpaper(primaryImage);
        }

        WallpaperState.CleanupOldOutputs(rendered.Select(r => r.ImagePath));
        Console.WriteLine($"Fertig: {string.Join(", ", rendered.Select(r => r.ImagePath))}");
    }

    /// <summary>
    /// Ein in der Konfiguration fest vorgegebenes Bild hat Vorrang; sonst das gemerkte Original des
    /// Benutzers (WallpaperState), damit nie auf ein bereits beschriftetes Bild gerendert wird.
    /// </summary>
    private static string? ResolveBaseImage(AppConfig config)
    {
        var configured = config.ResolveWallpaperPath();
        if (configured != null)
        {
            // Das Bild selbst darf fehlen, wenn es Dateien mit angehaengter Aufloesung gibt.
            if (File.Exists(configured) || (config.Wallpaper.ResolutionVariants && WallpaperState.HasAnyResolutionVariant(configured)))
            {
                return configured;
            }
            Console.WriteLine($"Warnung: wallpaper.path '{configured}' nicht gefunden - verwende das Wallpaper des Benutzers.");
        }
        return WallpaperState.ResolveOriginalWallpaper();
    }

    /// <summary>
    /// Ohne --config: die eingebauten Standardwerte als Vorlage. Mit --config: genau diese
    /// Konfiguration neu geschrieben - mit allen aktuellen Optionen und den Standardkommentaren. So
    /// laesst sich eine aeltere Datei auf den Stand der Programmversion bringen; dafuer (und nur dafuer)
    /// darf das Ziel die Quelldatei selbst sein, die vorherige Fassung bleibt als .bak liegen.
    /// </summary>
    private static void ExportConfig(string? path, string? configSource, bool withPlaceholders)
    {
        var target = Path.GetFullPath(path ?? AppConfig.FileName);
        var refresh = configSource != null && !AppConfig.IsUrl(configSource)
            && string.Equals(Path.GetFullPath(configSource), target, StringComparison.OrdinalIgnoreCase);
        if (File.Exists(target) && !refresh)
        {
            throw new InvalidOperationException($"'{target}' existiert bereits - bitte anderen Pfad angeben oder die Datei vorher entfernen.");
        }

        if (configSource != null)
        {
            var (config, source) = AppConfig.LoadWithoutInclude(configSource);
            var json = config.ToJson(withPlaceholders);
            if (refresh)
            {
                File.Copy(target, target + ".bak", overwrite: true);
                Console.WriteLine($"Vorherige Fassung gesichert: {target}.bak");
            }
            File.WriteAllText(target, json);
            Console.WriteLine($"Konfiguration aus '{source}' im aktuellen Format geschrieben: {target}");
            Console.WriteLine("Eigene Kommentare der Quelldatei werden dabei nicht uebernommen.");
            return;
        }

        File.WriteAllText(target, new AppConfig().ToJson(withPlaceholders));
        Console.WriteLine($"Standard-Konfiguration geschrieben: {target}");
        Console.WriteLine("Alle Platzhalter mit ihren aktuellen Werten zeigt: BGInfo-Reloaded.exe --list-tags");
    }

    private static int GetIntOption(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value))
        {
            return value;
        }
        return fallback;
    }

    private static string? GetStringOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--")) ? args[index + 1] : null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Verwendung:
              BGInfo-Reloaded.exe                          Wallpaper einmalig aktualisieren
              BGInfo-Reloaded.exe --version                 Installierte Version anzeigen
              BGInfo-Reloaded.exe --register-task            Autostart einrichten (Anmeldung + alle 30 Min. Refresh)
              BGInfo-Reloaded.exe --register-task --interval-minutes 15
                                                           Autostart mit eigenem Refresh-Intervall (0 = nur bei Anmeldung)
              BGInfo-Reloaded.exe --register-task --watch    Autostart, der zusaetzlich auf Aufloesungswechsel reagiert
              BGInfo-Reloaded.exe --unregister-task          Autostart-Tasks wieder entfernen
              BGInfo-Reloaded.exe --task-status              Zeigen, ob und wie der Autostart eingerichtet ist (Exit 0 = aktiv)

              BGInfo-Reloaded.exe --watch                   Rendern und aktiv bleiben: bei Aenderung von Aufloesung
                                                           oder Skalierung automatisch neu rendern
              BGInfo-Reloaded.exe --list-tags               Alle Platzhalter mit Beschreibung und aktuellem Wert
              BGInfo-Reloaded.exe --list-tags wmi:<Klasse>  Alle Eigenschaften einer WMI-Klasse als Platzhalter
              BGInfo-Reloaded.exe --list-tags reg:<Schluessel>
                                                           Alle Werte eines Registry-Schluessels als Platzhalter

              BGInfo-Reloaded.exe --config <pfad|url>       Konfiguration von Datei, UNC-Share oder http(s)-URL laden
                                                          (auch mit --register-task kombinierbar). Ohne --config:
                                                          Umgebungsvariable BGINFORELOADED_CONFIG, dann BGInfo-Reloaded.json
                                                          im Startverzeichnis (aktueller Ordner beim Aufruf), dann
                                                          neben der exe, dann %ProgramData%\BGInfo-Reloaded\BGInfo-Reloaded.json,
                                                          sonst eingebaute Standardwerte.
              BGInfo-Reloaded.exe --export-config [pfad]    Standard-Konfiguration als Vorlage schreiben (BGInfo-Reloaded.json)
              BGInfo-Reloaded.exe --export-config <pfad> --config <pfad>
                                                          Vorhandene Konfiguration im aktuellen Format neu schreiben
                                                          (alle Optionen, Standardkommentare); gleiche Datei als Quelle
                                                          und Ziel ist erlaubt, die alte Fassung bleibt als .bak
              BGInfo-Reloaded.exe --export-config ... --with-placeholders
                                                          Zusaetzlich alle Platzhalter als Kommentar in die Datei
                                                          schreiben (so entsteht die Beispielkonfiguration eines Release)
              BGInfo-Reloaded.exe --preview <bild.png>      Nur in Datei rendern, Wallpaper nicht aendern

              BGInfo-Reloaded.exe --update --repo org/repo   Prueft GitHub-Releases von org/repo und installiert
                                                           ein neueres Release (Asset "BGInfo-Reloaded.exe" noetig).
                                                           Repo wird gemerkt, danach reicht --update.
              BGInfo-Reloaded.exe --update                   Ohne --repo: aus dem Ordner, der in der Konfiguration unter
                                                           "update.path" steht (lokal oder UNC-Share), sonst aus
                                                           "update.repo" (GitHub), sonst aus dem gemerkten Repository.
                                                           Aus einem Ordner wird geholt, was in "update" eingeschaltet
                                                           ist: program, editor, config, backgrounds. Mit
                                                           "update.auto": true prueft jeder normale Lauf den Ordner selbst.
              BGInfo-Reloaded.exe --update --from <ordner>   Einmalig aus diesem Ordner aktualisieren.
              BGInfo-Reloaded.exe --update --check-only      Nur pruefen, nicht installieren.
              BGInfo-Reloaded.exe --update-source <git-url> [--branch main]
                                                           Quellcode per git clone/pull holen und lokal neu
                                                           bauen (braucht git.exe + .NET SDK auf der Maschine).

              BGInfo-Reloaded.exe --help                    Diese Hilfe anzeigen

            Jeder Lauf wird in %LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log protokolliert (die exe ist
            fensterlos - im Scheduled-Task-Betrieb ist das Log die einzige Fehleranzeige).
            """);
    }
}
