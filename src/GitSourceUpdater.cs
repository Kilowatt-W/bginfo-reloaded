using System.Diagnostics;
using System.Runtime.Versioning;

namespace BgInfoReloaded;

/// <summary>
/// Alternative zum GitHub-Release-Updater: zieht den *Quellcode* direkt per git clone/pull und baut
/// ihn lokal neu (dotnet publish). Ehrlich gesagt der schwergewichtigere Weg - braucht git.exe UND das
/// .NET SDK auf der Zielmaschine, was auf einem normalen Anwender-Desktop eher unueblich ist. Fuer
/// Admin-/Buildserver-Workstations, auf denen beides ohnehin installiert ist, aber genau die
/// "direkt vom Git pullen"-Variante, die auch ohne GitHub-Releases funktioniert - jedes erreichbare
/// Git-Repo (GitHub, GitLab, interner Server) reicht.
/// </summary>
[SupportedOSPlatform("windows")]
public static class GitSourceUpdater
{
    public static void Run(string gitUrl, string branch)
    {
        ExecutableSwap.CleanupOldBinary();

        RequireTool("git", "--version", "Git ist nicht installiert oder nicht im PATH. https://git-scm.com/download/win");
        RequireTool("dotnet", "--version", ".NET SDK ist nicht installiert oder nicht im PATH. https://dotnet.microsoft.com/download");

        var srcDir = Path.Combine(WallpaperState.StateDirectory, "src");

        if (branch.StartsWith('-'))
        {
            throw new InvalidOperationException($"Ungueltiger Branch-Name: {branch}");
        }

        if (Directory.Exists(Path.Combine(srcDir, ".git")))
        {
            Console.WriteLine($"Bestehendes Repository gefunden, hole Aenderungen ({branch})...");
            // Adresse nachziehen - sonst bliebe eine neu angegebene git-url stillschweigend wirkungslos.
            RunTool("git", ["-C", srcDir, "remote", "set-url", "origin", gitUrl], srcDir);
            RunTool("git", ["-C", srcDir, "fetch", "--depth", "1", "origin", branch], srcDir);
            RunTool("git", ["-C", srcDir, "reset", "--hard", "FETCH_HEAD"], srcDir);
        }
        else
        {
            Console.WriteLine($"Klone {gitUrl} ({branch}) nach {srcDir}...");
            Directory.CreateDirectory(Path.GetDirectoryName(srcDir)!);
            RunTool("git", ["clone", "--branch", branch, "--depth", "1", "--", gitUrl, srcDir], Path.GetTempPath());
        }

        // Gezielt das Hauptprogramm bauen: die erste beliebige .csproj waere editor\ (alphabetisch vor src\) -
        // dann laege danach der Editor als BGInfo-Reloaded.exe im Programmordner.
        var csproj = Path.Combine(srcDir, "src", "BGInfo-Reloaded.csproj");
        if (!File.Exists(csproj))
        {
            csproj = Directory.GetFiles(srcDir, "BGInfo-Reloaded.csproj", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException($"Keine BGInfo-Reloaded.csproj in {srcDir} gefunden - falsches Repository?");
        }

        var publishDir = Path.Combine(WallpaperState.StateDirectory, "publish");
        if (Directory.Exists(publishDir))
        {
            Directory.Delete(publishDir, recursive: true);
        }

        Console.WriteLine("Baue neue Version (dotnet publish)...");
        RunTool("dotnet",
            ["publish", csproj, "-c", "Release", "-r", "win-x64", "--self-contained", "true",
             "-p:PublishSingleFile=true", "-o", publishDir],
            srcDir);

        var newExe = Path.Combine(publishDir, FolderUpdater.MainExe);
        if (!File.Exists(newExe))
        {
            throw new InvalidOperationException($"Nach dem Build fehlt {FolderUpdater.MainExe} in {publishDir}.");
        }

        ExecutableSwap.ReplaceRunningExecutable(newExe);

        Console.WriteLine("Update aus dem Quellcode installiert. Wird beim naechsten Start dieser exe aktiv.");
    }

    private static void RequireTool(string exe, string versionArg, string errorMessage)
    {
        try
        {
            RunTool(exe, [versionArg], Path.GetTempPath());
        }
        catch
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static void RunTool(string exe, string[] arguments, string workingDirectory)
    {
        // ArgumentList statt einer zusammengesetzten Zeile: Werte mit Leerzeichen oder Anfuehrungszeichen
        // (Pfade, Branch) koennen so keine weiteren Schalter einschleusen.
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }
        // git soll nie auf eine Anmeldung warten - es gibt kein Fenster, in das jemand tippen koennte.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"{exe} konnte nicht gestartet werden.");
        // stderr parallel lesen: nacheinander gelesen verklemmt sich der Prozess, sobald stderr den Puffer fuellt.
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{exe} {string.Join(' ', arguments)} fehlgeschlagen (Exit {process.ExitCode}):\n{stderr}{stdout}");
        }
    }
}
