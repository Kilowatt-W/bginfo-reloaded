using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace BgInfoReloaded;

/// <summary>Ein Monitor als Render-Ziel: Lage im virtuellen Desktop, Groesse in echten Pixeln, DPI (96 = 100 %).</summary>
public sealed record MonitorTarget(int Number, int Left, int Top, int Width, int Height, int Dpi, bool IsPrimary)
{
    /// <summary>
    /// Von Taskleiste und angedockten Leisten belegte Streifen an den Monitorraendern, in Pixeln
    /// (Differenz zwischen Monitorflaeche und Arbeitsbereich). Bei automatisch ausgeblendeter
    /// Taskleiste ist alles 0.
    /// </summary>
    public (int Left, int Top, int Right, int Bottom) Reserved { get; init; }

    public override string ToString() =>
        $"Monitor {Number}{(IsPrimary ? " (primaer)" : "")}: {Width}x{Height}, {Dpi * 100 / 96} %";
}

/// <summary>
/// Alles, was mit dem Windows-Wallpaper selbst zu tun hat: aktuellen Pfad auslesen (Registry) und
/// ein neues Bild als Desktop-Hintergrund setzen (SystemParametersInfo - dieselbe Win32-API, die auch
/// die Systemsteuerung benutzt, damit der Wechsel sofort sichtbar ist, ohne Explorer neu zu starten).
/// </summary>
[SupportedOSPlatform("windows")]
public static class WallpaperInterop
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>
    /// Muss ganz am Programmanfang laufen. Ohne DPI-Awareness liefert Windows bei einer Skalierung
    /// ueber 100 % nur die virtuelle (kleinere) Aufloesung - das Wallpaper wuerde dann zu klein
    /// gerendert und von Windows unscharf hochskaliert.
    /// </summary>
    public static void EnableDpiAwareness()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
            {
                return;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // aelter als Windows 10 1703
        }
        SetProcessDPIAware();
    }

    /// <summary>
    /// Aufloesung des primaeren Monitors in echten Pixeln (setzt <see cref="EnableDpiAwareness"/> voraus).
    /// </summary>
    public static (int Width, int Height) GetPrimaryScreenResolution()
    {
        var width = GetSystemMetrics(SM_CXSCREEN);
        var height = GetSystemMetrics(SM_CYSCREEN);
        return (width > 0 && height > 0) ? (width, height) : (1920, 1080);
    }

    /// <summary>
    /// Aktuelle DPI des primaeren Monitors (96 = 100 %). Bewusst pro Aufruf frisch abgefragt statt
    /// GetDpiForSystem: der Systemwert bleibt fuer einen laufenden Prozess (--watch) unveraendert.
    /// </summary>
    public static int GetPrimaryScreenDpi()
    {
        try
        {
            var monitor = MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
            {
                return (int)dpiX;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // aelter als Windows 8.1
        }
        return 96;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITORINFOF_PRIMARY = 1;

    /// <summary>
    /// Alle aktiven Monitore, der primaere zuerst (Nummer 1), die uebrigen von links nach rechts.
    /// Setzt <see cref="EnableDpiAwareness"/> voraus, sonst waeren Groessen und Lagen virtuelle Pixel.
    /// </summary>
    public static List<MonitorTarget> GetMonitors()
    {
        var found = new List<(RECT Rect, RECT Work, bool Primary, int Dpi)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (handle, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(handle, ref info))
            {
                var dpi = 96;
                try
                {
                    if (GetDpiForMonitor(handle, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
                    {
                        dpi = (int)dpiX;
                    }
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
                {
                    // aelter als Windows 8.1
                }
                found.Add((info.rcMonitor, info.rcWork, (info.dwFlags & MONITORINFOF_PRIMARY) != 0, dpi));
            }
            return true;
        }, IntPtr.Zero);

        if (found.Count == 0)
        {
            // Keine Anzeige ermittelbar (z.B. getrennte Sitzung) - mit dem arbeiten, was GetSystemMetrics liefert.
            var (width, height) = GetPrimaryScreenResolution();
            return new List<MonitorTarget> { new(1, 0, 0, width, height, GetPrimaryScreenDpi(), true) };
        }

        return found
            .OrderByDescending(m => m.Primary)
            .ThenBy(m => m.Rect.Left)
            .ThenBy(m => m.Rect.Top)
            .Select((m, index) => new MonitorTarget(index + 1, m.Rect.Left, m.Rect.Top,
                m.Rect.Right - m.Rect.Left, m.Rect.Bottom - m.Rect.Top, m.Dpi, m.Primary)
            {
                Reserved = (Math.Max(0, m.Work.Left - m.Rect.Left), Math.Max(0, m.Work.Top - m.Rect.Top),
                    Math.Max(0, m.Rect.Right - m.Work.Right), Math.Max(0, m.Rect.Bottom - m.Work.Bottom))
            })
            .ToList();
    }

    /// <summary>
    /// Alles, was ein Neu-Rendern noetig macht, wenn es sich aendert - ueber alle Monitore. Dazu gehoert
    /// der von der Taskleiste belegte Rand, weil das Panel ihm ausweicht (panel.avoidTaskbar).
    /// </summary>
    public static string GetDisplayState() =>
        string.Join("; ", GetMonitors().Select(m => $"{m.Width}x{m.Height} @{m.Left},{m.Top} {m.Dpi * 100 / 96} % Rand {m.Reserved}"));

    // --- Pro-Monitor-Wallpaper ueber die Shell (IDesktopWallpaper, ab Windows 8) ---

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

        void SetBackgroundColor(uint color);

        uint GetBackgroundColor();

        void SetPosition(int position);

        int GetPosition();
    }

    [ComImport, Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass
    {
    }

    private const int DWPOS_FILL = 4;
    private const int DWPOS_SPAN = 5;

    /// <summary>
    /// Setzt je Monitor ein eigenes Bild. Die Zuordnung laeuft ueber die Lage im virtuellen Desktop.
    /// Wirft, wenn die Shell-Schnittstelle nicht verfuegbar ist (z.B. Server Core) - der Aufrufer
    /// faellt dann auf <see cref="SetWallpaper"/> zurueck.
    /// </summary>
    public static int SetWallpaperPerMonitor(IReadOnlyList<(MonitorTarget Monitor, string ImagePath)> images)
    {
        var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        try
        {
            // "Uebergreifend" wuerde ein einzelnes Bild ueber alle Monitore ziehen - mit Bildern je Monitor unvereinbar.
            if (wallpaper.GetPosition() == DWPOS_SPAN)
            {
                wallpaper.SetPosition(DWPOS_FILL);
            }

            var assigned = 0;
            var count = wallpaper.GetMonitorDevicePathCount();
            for (uint i = 0; i < count; i++)
            {
                var monitorId = wallpaper.GetMonitorDevicePathAt(i);
                RECT rect;
                try
                {
                    rect = wallpaper.GetMonitorRECT(monitorId);
                }
                catch
                {
                    continue; // bekannter, aber gerade nicht angeschlossener Monitor
                }

                var match = images.FirstOrDefault(x => x.Monitor.Left == rect.Left && x.Monitor.Top == rect.Top);
                if (match.ImagePath != null)
                {
                    wallpaper.SetWallpaper(monitorId, match.ImagePath);
                    assigned++;
                }
            }
            return assigned;
        }
        finally
        {
            Marshal.ReleaseComObject(wallpaper);
        }
    }

    /// <summary>
    /// Liest den Pfad des aktuell gesetzten Wallpapers aus der Registry.
    /// Null, falls keins gesetzt ist (z.B. reine Farbe als Hintergrund).
    /// </summary>
    public static string? GetCurrentWallpaperPath()
    {
        // Zuerst die Shell fragen: nach einem Setzen je Monitor steht in der Registry nur noch die
        // Sammeldatei "TranscodedWallpaper", die Shell kennt dagegen den wirklich gesetzten Pfad.
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            try
            {
                string? fallback = null;
                var count = wallpaper.GetMonitorDevicePathCount();
                for (uint i = 0; i < count; i++)
                {
                    var monitorId = wallpaper.GetMonitorDevicePathAt(i);
                    var path = wallpaper.GetWallpaper(monitorId);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }
                    try
                    {
                        var rect = wallpaper.GetMonitorRECT(monitorId);
                        if (rect.Left == 0 && rect.Top == 0)
                        {
                            return path; // primaerer Monitor
                        }
                    }
                    catch
                    {
                        continue; // nicht angeschlossen
                    }
                    fallback ??= path;
                }
                if (fallback != null)
                {
                    return fallback;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(wallpaper);
            }
        }
        catch
        {
            // Shell-Schnittstelle nicht verfuegbar - Registry genuegt dann.
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var value = key?.GetValue("WallPaper") as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Setzt die angegebene Bilddatei (muss ein absoluter Pfad zu einer BMP/JPG/PNG-Datei sein) als
    /// Desktop-Hintergrund fuer den aktuell angemeldeten Benutzer.
    /// </summary>
    public static void SetWallpaper(string imagePath)
    {
        var ok = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        if (!ok)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SystemParametersInfo(SPI_SETDESKWALLPAPER) fehlgeschlagen (Win32-Fehlercode {error}).");
        }
    }
}
