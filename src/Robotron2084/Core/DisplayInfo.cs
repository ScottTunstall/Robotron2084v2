using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Robotron2084.Core;

/// <summary>
/// Desktop work-area size in pixels. MonoGame 3.8.5 exposes no screen-bounds
/// API, so the Windows work area is queried directly (taskbar excluded).
/// </summary>
public static class DisplayInfo
{
    private const int SpiGetWorkArea = 48;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    /// <summary>Available desktop area (work area) as width/height in pixels.</summary>
    public static Point WorkArea
    {
        get
        {
            if (SystemParametersInfo(SpiGetWorkArea, SpiGetWorkArea, out Rect workArea, 0) && workArea.Width > 0 && workArea.Height > 0)
            {
                return new Point(workArea.Width, workArea.Height);
            }

            int width = GetSystemMetrics(SmCxScreen);
            int height = GetSystemMetrics(SmCyScreen);
            return new Point(Math.Max(1, width), Math.Max(1, height));
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
