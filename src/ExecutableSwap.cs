using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>
/// Gemeinsame Logik zum Austauschen der eigenen, gerade laufenden exe - genutzt sowohl vom
/// GitHub-Release-Updater als auch vom Quellcode-Updater (git clone/pull + dotnet publish).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ExecutableSwap
{
    /// <summary>
    /// Benennt die aktuell laufende exe in "<name>.old" um und legt die neue Datei an ihre Stelle.
    /// Funktioniert auf Windows auch waehrend das Programm noch laeuft (das geladene Image liegt im
    /// Speicher), nur direktes Ueberschreiben waere gesperrt.
    /// </summary>
    public static void ReplaceRunningExecutable(string newFilePath)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte den eigenen Programmpfad nicht ermitteln.");
        Replace(currentExe, newFilePath);
    }

    /// <summary>
    /// Wie <see cref="ReplaceRunningExecutable"/>, aber fuer eine beliebige exe - auch eine, die ein
    /// anderer Prozess gerade ausfuehrt (der Editor ersetzt die BGInfo-Reloaded.exe und umgekehrt).
    /// Gibt es das Ziel noch nicht, wird die neue Datei einfach hingelegt.
    /// </summary>
    public static void Replace(string target, string newFilePath)
    {
        if (File.Exists(target))
        {
            var backupPath = target + ".old";
            if (File.Exists(backupPath))
            {
                try
                {
                    File.Delete(backupPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Die alte Fassung laeuft noch - z.B. eine --watch-Instanz, gestartet vor einem frueheren
                    // Update. Loeschen geht dann nicht, Umbenennen der aktuellen exe schon: unter einem
                    // freien Namen ablegen; CleanupOldBinary raeumt ihn spaeter mit auf.
                    backupPath = $"{target}.{DateTime.Now:yyyyMMddHHmmss}.old";
                }
            }
            File.Move(target, backupPath);
        }
        File.Move(newFilePath, target);
    }

    /// <summary>
    /// Raeumt eine .old-Datei aus einem vorherigen Update auf. Am Anfang jedes Laufs aufrufen.
    /// </summary>
    public static void CleanupOldBinary()
    {
        try
        {
            var currentExe = Environment.ProcessPath;
            if (currentExe == null)
            {
                return;
            }
            // Auch die .old der jeweils anderen exe (Editor bzw. Hauptprogramm) im selben Ordner - und
            // die mit Zeitstempel, die entstehen, wenn eine .old beim Update noch lief (Replace).
            foreach (var backupPath in Directory.GetFiles(Path.GetDirectoryName(currentExe)!, "*.exe*.old"))
            {
                try
                {
                    File.Delete(backupPath);
                    if (backupPath.StartsWith(currentExe + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Alte Version aus vorherigem Update aufgeraeumt.");
                    }
                }
                catch
                {
                    // Datei noch gesperrt (das Programm laeuft noch) - beim naechsten Lauf erneut versuchen.
                }
            }
        }
        catch
        {
            // Ordner nicht lesbar - beim naechsten Lauf erneut versuchen.
        }
    }
}
