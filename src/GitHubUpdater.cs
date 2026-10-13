using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text.Json;

namespace BgInfoReloaded;

/// <summary>
/// Selbst-Update direkt aus einem GitHub-Repository: prueft das neueste Release, laedt die dort
/// hinterlegte BGInfo-Reloaded.exe herunter und ersetzt die laufende Programmdatei.
///
/// Funktioniert nach dem klassischen "rename-then-replace"-Trick: Auf Windows kann man die Datei
/// eines gerade laufenden Prozesses umbenennen (das Image liegt schon im Speicher), einfach
/// ueberschreiben geht dagegen nicht. Also: aktuelle exe -> BGInfo-Reloaded.exe.old, neue Datei an ihre
/// Stelle. Der naechste Start (naechster Anmelde-/Refresh-Task) laeuft dann automatisch mit der neuen
/// Version; die .old-Datei wird beim naechsten Lauf aufgeraeumt.
///
/// Erwartet ein GitHub-Release mit einem Asset, das exakt "BGInfo-Reloaded.exe" heisst (so, wie es
/// 06-Build-NativeApp.ps1 bzw. `dotnet publish` in diesem Paket auch erzeugt) und einen Tag-Namen wie
/// "v1.2.0" oder "1.2.0".
/// </summary>
[SupportedOSPlatform("windows")]
public static class GitHubUpdater
{
    private const string AssetName = "BGInfo-Reloaded.exe";

    /// <summary>Der Editor haengt als weiteres Asset am Release; geholt wird er nur, wo er schon neben der exe liegt.</summary>
    private const string EditorAssetName = FolderUpdater.EditorExe;

    /// <summary>
    /// "https://github.com/owner/name", "github.com/owner/name.git" oder "owner/name" -> "owner/name".
    /// </summary>
    public static string NormalizeRepo(string repo)
    {
        var text = repo.Trim();
        foreach (var prefix in new[] { "https://", "http://", "www.", "github.com/" })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..];
            }
        }
        text = text.Trim('/');
        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }
        // Nur owner/name - was dahinter steht (/releases, /tree/main ...), gehoert nicht dazu.
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : text;
    }

    public static string ConfigPath => Path.Combine(WallpaperState.StateDirectory, "update.json");

    /// <param name="repoArg">Mit --repo angegeben; wird fuer spaetere Aufrufe gemerkt.</param>
    /// <param name="configuredRepo">"update.repo" aus der Konfiguration - gilt vor dem gemerkten Repository.</param>
    public static async Task RunAsync(string? repoArg, bool checkOnly, string? configuredRepo = null)
    {
        ExecutableSwap.CleanupOldBinary();

        var repo = repoArg ?? configuredRepo ?? LoadStoredRepo()
            ?? throw new InvalidOperationException(
                "Keine Update-Quelle angegeben. In der Konfiguration \"update.path\" (Ordner) oder " +
                "\"update.repo\" (GitHub, owner/name) eintragen - oder einmalig mit --repo <owner/name> " +
                "aufrufen, z.B. --update --repo meinorg/bginfo-reloaded (wird danach gemerkt).");

        if (repoArg != null)
        {
            SaveStoredRepo(repoArg);
        }
        repo = NormalizeRepo(repo);

        Console.WriteLine($"Pruefe GitHub-Repository '{repo}' auf ein neueres Release...");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BGInfo-Reloaded", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        string json;
        try
        {
            json = await http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"GitHub kennt unter '{repo}' kein oeffentliches Repository mit einem Release (Antwort 404). " +
                "Schreibweise pruefen (owner/name); ein privates Repository ist ohne Anmeldung nicht erreichbar.");
        }
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.GetProperty("tag_name").GetString() ?? throw new InvalidOperationException("Release ohne tag_name erhalten.");
        var remoteVersion = ParseVersion(tagName);

        Console.WriteLine($"Installiert: {CurrentVersion}  |  Verfuegbar: {remoteVersion} ({tagName})");

        string? AssetUrl(string name)
        {
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (string.Equals(asset.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return asset.GetProperty("browser_download_url").GetString();
                }
            }
            return null;
        }

        // Der Editor wird nur aktualisiert, wo er schon neben der exe liegt - wie beim Update aus einem Ordner.
        var ownDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
        var editorPath = Path.Combine(ownDirectory, EditorAssetName);
        Version? editorVersion = null;
        if (File.Exists(editorPath))
        {
            editorVersion = Version.TryParse(System.Diagnostics.FileVersionInfo.GetVersionInfo(editorPath).FileVersion, out var parsed) ? parsed : new Version(0, 0);
            Console.WriteLine($"{EditorAssetName}: installiert {editorVersion}");
        }

        var programNewer = remoteVersion != null && remoteVersion > CurrentVersion;
        var editorNewer = remoteVersion != null && editorVersion != null && remoteVersion > editorVersion;
        var editorUrl = editorNewer ? AssetUrl(EditorAssetName) : null;
        if (editorNewer && editorUrl == null)
        {
            Console.WriteLine($"Hinweis: Release {tagName} enthaelt keine {EditorAssetName} - der Editor bleibt, wie er ist.");
            editorNewer = false;
        }

        if (!programNewer && !editorNewer)
        {
            Console.WriteLine("Bereits aktuell - kein Update noetig.");
            return;
        }

        if (checkOnly)
        {
            Console.WriteLine("Neueres Release verfuegbar (nur geprueft, --update ohne --check-only installiert es).");
            return;
        }

        if (programNewer)
        {
            var downloadUrl = AssetUrl(AssetName) ?? throw new InvalidOperationException(
                $"Release {tagName} hat kein Asset namens '{AssetName}'. Beim Release-Erstellen die per " +
                "dotnet publish erzeugte exe unter genau diesem Namen anhaengen.");

            Console.WriteLine($"Lade {AssetName} {tagName} herunter...");
            var tempFile = Path.Combine(Path.GetTempPath(), $"BGInfo-Reloaded_{tagName}.exe");
            var bytes = await http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tempFile, bytes);

            ExecutableSwap.ReplaceRunningExecutable(tempFile);
            Console.WriteLine($"{AssetName} auf {tagName} aktualisiert. Wird beim naechsten Start dieser exe aktiv.");
        }

        if (editorNewer)
        {
            Console.WriteLine($"Lade {EditorAssetName} {tagName} herunter...");
            var staged = editorPath + ".new";
            try
            {
                var bytes = await http.GetByteArrayAsync(editorUrl);
                await File.WriteAllBytesAsync(staged, bytes);
                // Umbenennen statt ueberschreiben: der Editor laeuft meist gerade (er hat das Update ausgeloest).
                ExecutableSwap.Replace(editorPath, staged);
                Console.WriteLine($"{EditorAssetName} auf {tagName} aktualisiert. Wird beim naechsten Start des Editors aktiv.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
            {
                Console.WriteLine($"Warnung: {EditorAssetName} konnte nicht aktualisiert werden ({ex.Message}).");
            }
        }
    }

    private static Version? ParseVersion(string tagName)
    {
        var cleaned = tagName.TrimStart('v', 'V');
        return Version.TryParse(cleaned, out var v) ? v : null;
    }

    private static string? LoadStoredRepo()
    {
        if (!File.Exists(ConfigPath))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
            return doc.RootElement.TryGetProperty("repo", out var el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveStoredRepo(string repo)
    {
        Directory.CreateDirectory(WallpaperState.StateDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new { repo }));
    }

    public static Version CurrentVersion =>
        typeof(GitHubUpdater).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
}
