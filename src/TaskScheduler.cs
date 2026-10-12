using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;

namespace BgInfoReloaded;

/// <summary>
/// Registriert/entfernt die Autostart-Automatisierung ueber schtasks.exe - bewusst kein
/// Register-ScheduledTask aus PowerShell, damit die exe wirklich vollstaendig eigenstaendig ist und
/// keine PowerShell-Abhaengigkeit mehr hat. Zwei Tasks: einer bei Anmeldung, optional einer fuer ein
/// periodisches Refresh - beide bewusst NICHT als SYSTEM, weil SYSTEM keinen sichtbaren Desktop hat.
///
/// Die Tasks werden als XML-Definition angelegt (schtasks /Create /XML), weil sich nur so die
/// Standardwerte abschalten lassen, die hier stoeren: "nur im Netzbetrieb starten" (Notebooks) und
/// "nach 3 Tagen beenden" (wuerde eine dauerhaft laufende --watch-Instanz abschiessen).
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppTaskScheduler
{
    private const string LogonTaskName = "BGInfo-Reloaded - Logon";
    private const string RefreshTaskName = "BGInfo-Reloaded - Refresh";

    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static void Register(int refreshMinutes, string? configSource = null, bool watch = false)
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte den eigenen Programmpfad nicht ermitteln.");
        var user = $"{Environment.UserDomainName}\\{Environment.UserName}";

        // Lokale/UNC-Pfade absolut machen - der Task startet spaeter mit einem anderen Arbeitsverzeichnis.
        var arguments = "";
        if (!string.IsNullOrWhiteSpace(configSource))
        {
            var source = AppConfig.IsUrl(configSource) ? configSource : Path.GetFullPath(configSource);
            arguments = $"--config \"{source}\"";
        }

        var logonTrigger = new XElement(Ns + "LogonTrigger",
            new XElement(Ns + "Enabled", "true"),
            new XElement(Ns + "UserId", user));
        var logonArguments = watch ? $"{arguments} --watch".Trim() : arguments;
        CreateTask(LogonTaskName, user, exePath, logonArguments, logonTrigger,
            watch
                ? "Aktualisiert das BGInfo-Reloaded-Wallpaper bei der Anmeldung und danach bei jeder Aenderung von Aufloesung oder Skalierung."
                : "Aktualisiert das BGInfo-Reloaded-Wallpaper bei der Anmeldung.");
        Console.WriteLine(watch
            ? $"Task '{LogonTaskName}' registriert (startet bei jeder Anmeldung von {user} und bleibt aktiv, um auf Aufloesungswechsel zu reagieren)."
            : $"Task '{LogonTaskName}' registriert (laeuft bei jeder Anmeldung von {user}).");

        if (refreshMinutes > 0)
        {
            var refreshTrigger = new XElement(Ns + "TimeTrigger",
                new XElement(Ns + "Repetition",
                    new XElement(Ns + "Interval", $"PT{refreshMinutes}M"),
                    new XElement(Ns + "StopAtDurationEnd", "false")),
                new XElement(Ns + "StartBoundary", DateTime.Today.ToString("yyyy-MM-dd'T'HH:mm:ss")),
                new XElement(Ns + "Enabled", "true"));
            CreateTask(RefreshTaskName, user, exePath, arguments, refreshTrigger,
                $"Aktualisiert das BGInfo-Reloaded-Wallpaper alle {refreshMinutes} Minuten.");
            Console.WriteLine($"Task '{RefreshTaskName}' registriert (Refresh alle {refreshMinutes} Minuten).");
        }
        else
        {
            // Ein frueher registrierter Refresh-Task soll bei "nur bei Anmeldung" nicht weiterlaufen.
            TryDelete(RefreshTaskName, quietIfMissing: true);
        }
    }

    public static void Unregister()
    {
        TryDelete(LogonTaskName);
        TryDelete(RefreshTaskName);
    }

    private static void CreateTask(string name, string user, string exePath, string arguments, XElement trigger, string description)
    {
        var exec = new XElement(Ns + "Exec", new XElement(Ns + "Command", exePath));
        if (arguments.Length > 0)
        {
            exec.Add(new XElement(Ns + "Arguments", arguments));
        }

        var task = new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description", description)),
            new XElement(Ns + "Triggers", trigger),
            new XElement(Ns + "Principals",
                new XElement(Ns + "Principal", new XAttribute("id", "Author"),
                    new XElement(Ns + "UserId", user),
                    new XElement(Ns + "LogonType", "InteractiveToken"),
                    new XElement(Ns + "RunLevel", "LeastPrivilege"))),
            new XElement(Ns + "Settings",
                new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(Ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(Ns + "StopIfGoingOnBatteries", "false"),
                new XElement(Ns + "StartWhenAvailable", "true"),
                new XElement(Ns + "Enabled", "true"),
                new XElement(Ns + "ExecutionTimeLimit", "PT0S")),
            new XElement(Ns + "Actions", new XAttribute("Context", "Author"), exec));

        var xmlFile = Path.Combine(Path.GetTempPath(), $"BGInfo-Reloaded-task-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks erwartet die Datei in der Kodierung, die die XML-Deklaration nennt.
            File.WriteAllText(xmlFile, "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" + task, Encoding.Unicode);
            RunSchtasks($"/Create /TN \"{name}\" /XML \"{xmlFile}\" /F");
        }
        finally
        {
            File.Delete(xmlFile);
        }
    }

    private static void TryDelete(string taskName, bool quietIfMissing = false)
    {
        try
        {
            // Erst beenden: /Delete entfernt nur die Definition, eine laufende --watch-Instanz liefe sonst weiter.
            RunSchtasks($"/End /TN \"{taskName}\"");
        }
        catch
        {
            // Task existiert nicht oder laeuft nicht - das klaert der Delete-Aufruf.
        }

        try
        {
            RunSchtasks($"/Delete /TN \"{taskName}\" /F");
            Console.WriteLine($"Task '{taskName}' entfernt.");
        }
        catch (Exception ex)
        {
            if (!quietIfMissing)
            {
                Console.WriteLine($"Task '{taskName}' konnte nicht entfernt werden (existierte er?): {ex.Message}");
            }
        }
    }

    private static void RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("schtasks.exe konnte nicht gestartet werden.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"schtasks.exe {arguments} fehlgeschlagen (Exit {process.ExitCode}): {stderr}{stdout}");
        }
    }
}
