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

        if (Directory.Exists(Path.Combine(srcDir, ".git")))
        {
            Console.WriteLine($"Bestehendes Repository gefunden, hole Aenderungen ({branch})...");
            RunTool("git", $"-C \"{srcDir}\" fetch origin {branch}", srcDir);
            RunTool("git", $"-C \"{srcDir}\" reset --hard origin/{branch}", srcDir);
        }
        else
        {
            Console.WriteLine($"Klone {gitUrl} ({branch}) nach {srcDir}...");
            Directory.CreateDirectory(Path.GetDirectoryName(srcDir)!);
            RunTool("git", $"clone --branch {branch} --depth 1 \"{gitUrl}\" \"{srcDir}\"", Path.GetTempPath());
        }

        var csproj = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException($"Keine .csproj-Datei in {srcDir} gefunden - falsches Repository?");

        var publishDir = Path.Combine(WallpaperState.StateDirectory, "publish");
        if (Directory.Exists(publishDir))
        {
            Directory.Delete(publishDir, recursive: true);
        }

        Console.WriteLine("Baue neue Version (dotnet publish)...");
        RunTool("dotnet",
            $"publish \"{csproj}\" -c Release -r win-x64 --self-contained true " +
            $"-p:PublishSingleFile=true -o \"{publishDir}\"",
            srcDir);

        var newExe = Directory.GetFiles(publishDir, "*.exe")
            .FirstOrDefault(f => !f.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Nach dem Build wurde keine .exe in {publishDir} gefunden.");

        ExecutableSwap.ReplaceRunningExecutable(newExe);

        Console.WriteLine("Update aus dem Quellcode installiert. Wird beim naechsten Start dieser exe aktiv.");
    }

    private static void RequireTool(string exe, string versionArg, string errorMessage)
    {
        try
        {
            RunTool(exe, versionArg, Path.GetTempPath());
        }
        catch
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static void RunTool(string exe, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo(exe, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"{exe} konnte nicht gestartet werden.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{exe} {arguments} fehlgeschlagen (Exit {process.ExitCode}):\n{stderr}{stdout}");
        }
    }
}
