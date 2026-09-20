using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Robotron2084.Core;

/// <summary>The desktop's geometry in pixels: the work area and the monitor's resolution.</summary>
/// <remarks>Port-only. MonoGame 3.8.5 exposes no screen-bounds API, so both are queried from Windows;
/// the work area excludes the taskbar.</remarks>
public static class DisplayInfo
{
    private const int SpiGetWorkArea = 48;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    /// <summary>The primary monitor's resolution in pixels.</summary>
    /// <remarks>Full screen sets the backbuffer to this, so the game runs at the desktop's own mode.</remarks>
    public static Point DesktopResolution =>
        new(Math.Max(1, GetSystemMetrics(SmCxScreen)), Math.Max(1, GetSystemMetrics(SmCyScreen)));

    /// <summary>Available desktop area (work area) as width/height in pixels.</summary>
    public static Point WorkArea
    {
        get
        {
            if (SystemParametersInfo(SpiGetWorkArea, SpiGetWorkArea, out Rect workArea, 0) && workArea.Width > 0 && workArea.Height > 0)
            {
                return new Point(workArea.Width, workArea.Height);
            }

            return DesktopResolution;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, out Rect pvParam, int fWinIni);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
