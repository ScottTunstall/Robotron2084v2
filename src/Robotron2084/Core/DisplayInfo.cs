using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Robotron2084.Core;

/// <summary>The desktop's geometry in pixels: the work area and the monitor's resolution.</summary>
/// <remarks>Port-only. MonoGame 3.8.5 exposes no screen-bounds API, so both are queried from Windows;
/// the work area excludes the taskbar.</remarks>
public static class DisplayInfo
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private const int SpiGetWorkArea = 48;

    /// <summary>The primary monitor's resolution in pixels.</summary>
    /// <remarks>Full screen sets the backbuffer to this, so the game runs at the desktop's own mode.</remarks>
    public static Point GetDesktopResolution() =>
        new(Math.Max(1, GetSystemMetrics(SmCxScreen)), Math.Max(1, GetSystemMetrics(SmCyScreen)));

    /// <summary>Available desktop area (work area) as width/height in pixels.</summary>
    public static Point GetWorkArea()
    {
        if (SystemParametersInfo(SpiGetWorkArea, SpiGetWorkArea, out Rect workArea, 0) && workArea.GetWidth() > 0 && workArea.GetHeight() > 0)
        {
            return new Point(workArea.GetWidth(), workArea.GetHeight());
        }

        return GetDesktopResolution();
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, out Rect pvParam, int fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int GetWidth() => Right - Left;
        public int GetHeight() => Bottom - Top;
    }
}
