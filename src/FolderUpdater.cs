using System.Diagnostics;
using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>Was ein Lauf von <see cref="FolderUpdater.Run"/> ergeben hat.</summary>
/// <param name="Changes">Anzahl geholter (bzw. bei "nur pruefen": verfuegbarer) Dateien.</param>
/// <param name="ConfigUpdated">Die BGInfo-Reloaded.json neben dem Programm wurde ersetzt.</param>
/// <param name="SelfUpdated">Das gerade laufende Programm wurde ersetzt - aktiv ab dem naechsten Start.</param>
/// <param name="Reachable">Der Update-Ordner war erreichbar.</param>
/// <param name="RestartPending">Das laufende Programm wurde schon frueher ersetzt und wartet nur noch auf einen Neustart.</param>
public sealed record FolderUpdateResult(int Changes, bool ConfigUpdated, bool SelfUpdated, bool Reachable, bool RestartPending = false);

/// <summary>
/// Selbst-Update aus einem Ordner (lokal oder UNC-Share), der in der Konfiguration unter
/// "update.path" steht. Laeuft in BGInfo-Reloaded.exe und im Editor gleich - verglichen wird immer
/// mit den Dateien im eigenen Programmordner, nicht mit dem laufenden Prozess. Was geholt wird,
/// bestimmen die Schalter in "update":
/// - program: BGInfo-Reloaded.exe, wenn die im Ordner eine hoehere Dateiversion hat (oder lokal fehlt).
/// - editor: BGInfo-Reloaded-Editor.exe, ebenfalls nach Dateiversion - aber nur dort, wo der Editor
///   schon neben der exe liegt (auf die Zielrechner wird er nicht verteilt).
/// - config: BGInfo-Reloaded.json, wenn die im Ordner neuer ist (Aenderungsdatum).
/// - backgrounds: background*.* und der Unterordner backgrounds\, jeweils wenn neuer oder noch nicht da.
/// Programme werden per "rename-then-replace" (siehe <see cref="ExecutableSwap"/>) getauscht - das
/// geht auch, waehrend sie laufen; die neue Fassung ist ab dem naechsten Start aktiv.
/// Fuer Umgebungen ohne Zugriff auf GitHub: der Admin legt die neuen Dateien auf den Share, die
/// Rechner holen sie sich selbst.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FolderUpdater
{
    public const string MainExe = "BGInfo-Reloaded.exe";
    public const string EditorExe = "BGInfo-Reloaded-Editor.exe";
    public const string BackgroundPattern = "background*.*";
    public const string BackgroundFolder = "backgrounds";

    /// <summary>
    /// Prueft den Ordner und aktualisiert bei Bedarf. <paramref name="quiet"/> (automatische Pruefung
    /// bei jedem Lauf) meldet nur, wenn es etwas zu tun gibt oder etwas nicht stimmt. Meldungen gehen
    /// an <paramref name="log"/>, ohne Angabe an die Konsole (und damit ins Log).
    /// </summary>
    public static FolderUpdateResult Run(string folder, UpdateConfig options, bool checkOnly, bool quiet, Action<string>? log = null)
    {
        log ??= Console.WriteLine;
        ExecutableSwap.CleanupOldBinary();

        if (AppConfig.IsUrl(folder))
        {
            throw new InvalidOperationException(
                $"update.path '{folder}' ist eine URL - erwartet wird ein Ordner (lokal oder UNC-Share). " +
                "Fuer Updates aus GitHub-Releases: \"update.repo\" bzw. --update --repo <owner/name>.");
        }

        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte den eigenen Programmpfad nicht ermitteln.");
        var ownDirectory = Path.GetDirectoryName(currentExe)!;

        if (!Directory.Exists(folder))
        {
            log($"Warnung: Update-Ordner '{folder}' ist nicht erreichbar - kein Update geprueft.");
            return new FolderUpdateResult(0, false, false, Reachable: false);
        }
        if (string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), ownDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            if (!quiet)
            {
                log($"Update-Ordner '{folder}' ist der eigene Programmordner - nichts zu tun.");
            }
            return new FolderUpdateResult(0, false, false, Reachable: true);
        }

        if (!quiet)
        {
            log($"Update-Ordner: {folder}");
        }

        var changes = 0;
        var selfUpdated = false;
        var configUpdated = false;
        var restartPending = false;
        // Das laufende Programm wurde in dieser Sitzung schon getauscht (die alte Fassung liegt als
        // .old daneben und ist noch gesperrt): nicht noch einmal versuchen. Seine Version laesst sich
        // dann auch nicht verlaesslich lesen - Windows liefert fuer den Pfad des laufenden Programms
        // die Angaben des geladenen, alten Abbilds.
        bool AlreadySwapped(string name)
        {
            if (!IsSelf(name, currentExe) || !File.Exists(currentExe + ".old"))
            {
                return false;
            }
            restartPending = true;
            if (!quiet)
            {
                log($"{name}: bereits aktualisiert - die neue Version wird mit dem naechsten Start aktiv.");
            }
            return true;
        }
        if (options.Program && !AlreadySwapped(MainExe))
        {
            var done = UpdateExecutable(MainExe, folder, ownDirectory, onlyIfInstalled: false, checkOnly, quiet, log);
            changes += done;
            selfUpdated |= done > 0 && !checkOnly && IsSelf(MainExe, currentExe);
        }
        if (options.Editor && !AlreadySwapped(EditorExe))
        {
            var done = UpdateExecutable(EditorExe, folder, ownDirectory, onlyIfInstalled: true, checkOnly, quiet, log);
            changes += done;
            selfUpdated |= done > 0 && !checkOnly && IsSelf(EditorExe, currentExe);
        }
        if (options.Config)
        {
            var done = SyncFile(Path.Combine(folder, AppConfig.FileName), Path.Combine(ownDirectory, AppConfig.FileName), checkOnly, keepBackup: true, log);
            changes += done;
            configUpdated = done > 0 && !checkOnly;
        }
        if (options.Backgrounds)
        {
            changes += SyncBackgrounds(folder, ownDirectory, checkOnly, log);
        }

        if (!quiet && changes == 0)
        {
            log("Alles aktuell - nichts zu holen.");
        }
        else if (checkOnly && changes > 0 && !quiet)
        {
            log("Nur geprueft - --update ohne --check-only holt die genannten Dateien.");
        }
        return new FolderUpdateResult(changes, configUpdated, selfUpdated, Reachable: true, restartPending);
    }

    private static bool IsSelf(string name, string currentExe) =>
        string.Equals(Path.GetFileName(currentExe), name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Holt eine exe, wenn die im Ordner eine hoehere Dateiversion hat als die im Programmordner.
    /// <paramref name="onlyIfInstalled"/>: nur aktualisieren, wo sie schon liegt (Editor) - sonst wird
    /// eine fehlende exe auch neu hingelegt.
    /// </summary>
    private static int UpdateExecutable(string name, string folder, string ownDirectory, bool onlyIfInstalled, bool checkOnly, bool quiet, Action<string> log)
    {
        var local = Path.Combine(ownDirectory, name);
        var source = Path.Combine(folder, name);
        var installedHere = File.Exists(local);
        if (onlyIfInstalled && !installedHere)
        {
            return 0;
        }
        if (!File.Exists(source))
        {
            if (!onlyIfInstalled)
            {
                log($"Warnung: im Update-Ordner '{folder}' liegt keine {name}.");
            }
            return 0;
        }

        var available = ReadVersion(source);
        var installed = installedHere ? ReadVersion(local) : null;
        var newer = available != null && (!installedHere || installed == null || available > installed);
        if (!quiet || newer)
        {
            log($"{name}: installiert {(installedHere ? installed?.ToString() ?? "unbekannt" : "nicht vorhanden")}, verfuegbar {available?.ToString() ?? "unbekannt (keine Dateiversion)"}");
        }
        if (!newer)
        {
            return 0;
        }
        if (checkOnly)
        {
            log($"  -> neuere Version {available} verfuegbar");
            return 1;
        }

        // Erst vollstaendig neben das Ziel kopieren, dann tauschen: bricht die Verbindung zum Share
        // beim Kopieren ab, bleibt die vorhandene Version unangetastet. Der Tausch per Umbenennen
        // geht auch, wenn die exe gerade laeuft - als dieses oder als das andere Programm.
        var staged = local + ".new";
        try
        {
            File.Copy(source, staged, overwrite: true);
            ExecutableSwap.Replace(local, staged);
            log($"  -> {name} auf {available} aktualisiert, aktiv ab dem naechsten Start");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"Warnung: {name} konnte nicht aktualisiert werden ({ex.Message}).");
            try
            {
                File.Delete(staged);
            }
            catch
            {
                // Rest der Kopie - stoert nicht, der naechste Versuch ueberschreibt sie
            }
            return 0;
        }
    }

    private static int SyncBackgrounds(string folder, string ownDirectory, bool checkOnly, Action<string> log)
    {
        var changes = 0;
        foreach (var source in Directory.GetFiles(folder, BackgroundPattern))
        {
            changes += SyncFile(source, Path.Combine(ownDirectory, Path.GetFileName(source)), checkOnly, keepBackup: false, log);
        }

        var sourceFolder = Path.Combine(folder, BackgroundFolder);
        if (Directory.Exists(sourceFolder))
        {
            foreach (var source in Directory.GetFiles(sourceFolder))
            {
                changes += SyncFile(source, Path.Combine(ownDirectory, BackgroundFolder, Path.GetFileName(source)), checkOnly, keepBackup: false, log);
            }
        }
        return changes;
    }

    /// <summary>
    /// Holt eine Datei, wenn es sie lokal noch nicht gibt oder die im Ordner neuer ist. Das
    /// Aenderungsdatum bleibt beim Kopieren erhalten - danach gelten beide als gleich. Eine lokal
    /// neuere Datei wird nicht ueberschrieben, und nichts wird geloescht.
    /// </summary>
    private static int SyncFile(string source, string target, bool checkOnly, bool keepBackup, Action<string> log)
    {
        if (!File.Exists(source))
        {
            return 0;
        }

        var exists = File.Exists(target);
        // 2 Sekunden Toleranz: Dateisysteme speichern Zeiten unterschiedlich genau.
        if (exists && File.GetLastWriteTimeUtc(source) <= File.GetLastWriteTimeUtc(target).AddSeconds(2))
        {
            return 0;
        }

        var name = Path.GetFileName(target);
        if (checkOnly)
        {
            log($"{name}: {(exists ? "neuere Fassung" : "neu")} im Update-Ordner");
            return 1;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (exists && keepBackup)
            {
                File.Copy(target, target + ".bak", overwrite: true);
            }
            File.Copy(source, target, overwrite: true);
            log($"{name} {(exists ? "aktualisiert" : "geholt")}{(exists && keepBackup ? $" (vorherige Fassung: {name}.bak)" : "")}.");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"Warnung: {name} konnte nicht geholt werden ({ex.Message}).");
            return 0;
        }
    }

    private static Version? ReadVersion(string file)
    {
        var info = FileVersionInfo.GetVersionInfo(file);
        return Version.TryParse(info.FileVersion, out var version) ? version : null;
    }
}
