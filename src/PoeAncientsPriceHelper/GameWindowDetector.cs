using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PoeAncientsPriceHelper;

/// <summary>
/// Detects the Path of Exile 2 game window and returns its bounds.
/// Uses Win32 FindWindow / GetWindowRect to locate the game.
/// </summary>
internal static class GameWindowDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    // Common PoE2 window titles (process name or window title)
    private static readonly string[] ProcessNames = ["PathofExile2", "Path of Exile 2", "poe2"];
    private static readonly string[] WindowTitles = ["Path of Exile 2", "PathofExile2"];

    /// <summary>
    /// Tries to find the PoE2 game window and return its screen bounds.
    /// Returns null if the game is not running or not found.
    /// </summary>
    public static Rectangle? FindGameWindow()
    {
        try
        {
            // Method 1: Find by window title
            foreach (var title in WindowTitles)
            {
                var hWnd = FindWindow(null, title);
                if (hWnd != IntPtr.Zero && IsWindow(hWnd))
                {
                    if (GetWindowRect(hWnd, out var rect))
                    {
                        var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                        if (bounds.Width > 100 && bounds.Height > 100)
                            return bounds;
                    }
                }
            }

            // Method 2: Find by process name
            foreach (var name in ProcessNames)
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var p in procs)
                {
                    if (p.MainWindowHandle != IntPtr.Zero && IsWindow(p.MainWindowHandle))
                    {
                        if (GetWindowRect(p.MainWindowHandle, out var rect))
                        {
                            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                            if (bounds.Width > 100 && bounds.Height > 100)
                                return bounds;
                        }
                    }
                }
            }

            // Method 3: Any process with "poe" in the name
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.Contains("poe", StringComparison.OrdinalIgnoreCase) &&
                        p.MainWindowHandle != IntPtr.Zero && IsWindow(p.MainWindowHandle))
                    {
                        if (GetWindowRect(p.MainWindowHandle, out var rect))
                        {
                            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                            if (bounds.Width > 100 && bounds.Height > 100)
                                return bounds;
                        }
                    }
                }
                catch { /* process may have exited */ }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Returns the monitor that contains the game window, or the primary monitor.
    /// </summary>
    public static Screen GetGameMonitor()
    {
        var gameRect = FindGameWindow();
        if (gameRect is { } r)
            return Screen.FromRectangle(r);
        return Screen.PrimaryScreen!;
    }
}
