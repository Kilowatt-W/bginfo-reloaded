using System.Text;

namespace BgInfoReloaded;

/// <summary>
/// Schreibt alles, was die App auf stdout/stderr ausgibt, zusaetzlich mit Zeitstempel in
/// %LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log. Noetig, weil die exe fensterlos ist: im Scheduled-Task-
/// Betrieb gibt es keine Konsole, ein fehlgeschlagener Lauf waere sonst unsichtbar.
/// Logging ist reiner Komfort - ein Schreibfehler darf den eigentlichen Lauf nie verhindern.
/// </summary>
public static class RunLog
{
    private const long MaxBytes = 256 * 1024;

    public static string LogFile => Path.Combine(WallpaperState.StateDirectory, "bginfo.log");

    private static string PreviousLogFile => LogFile + ".old";

    public static void Start(string[] args)
    {
        try
        {
            Directory.CreateDirectory(WallpaperState.StateDirectory);
            Rotate();
        }
        catch
        {
            // Ohne Logverzeichnis laeuft die App normal weiter, nur eben ohne Log.
        }

        Console.SetOut(new TeeWriter(Console.Out, "INFO  "));
        Console.SetError(new TeeWriter(Console.Error, "FEHLER"));
        Append("INFO  ", $"--- Start {Environment.UserDomainName}\\{Environment.UserName}: {(args.Length > 0 ? string.Join(' ', args) : "(ohne Argumente)")}");
    }

    public static void Finish(int exitCode) =>
        Append(exitCode == 0 ? "INFO  " : "FEHLER", $"--- Ende, Exit-Code {exitCode}");

    /// <summary>Haelt die Datei klein: ab 256 KB wird sie zu bginfo.log.old, es bleibt genau eine Vorgaengerdatei.</summary>
    private static void Rotate()
    {
        var info = new FileInfo(LogFile);
        if (info.Exists && info.Length > MaxBytes)
        {
            File.Move(LogFile, PreviousLogFile, overwrite: true);
        }
    }

    private static void Append(string level, string line)
    {
        try
        {
            // Logon- und Refresh-Task koennen sich ueberschneiden - deshalb pro Zeile oeffnen und teilen.
            using var stream = new FileStream(LogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {line}");
        }
        catch
        {
            // siehe Klassenkommentar
        }
    }

    private sealed class TeeWriter(TextWriter inner, string level) : TextWriter
    {
        private readonly StringBuilder _line = new();

        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value)
        {
            inner.Write(value);

            if (value == '\n')
            {
                var text = _line.ToString().TrimEnd('\r');
                _line.Clear();
                if (text.Length > 0)
                {
                    Append(level, text);
                }
            }
            else
            {
                _line.Append(value);
            }
        }

        public override void Flush() => inner.Flush();
    }
}
