using System.Runtime.InteropServices;

namespace Robotron2084.Core.Models;

/// <summary>A rectangle as Windows gives it: its edges in screen pixels, laid out as the Windows API expects.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Win32Rect
{
    /// <summary>The left edge.</summary>
    public int Left;

    /// <summary>The top edge.</summary>
    public int Top;

    /// <summary>The right edge: the first pixel outside the rectangle.</summary>
    public int Right;

    /// <summary>The bottom edge: the first pixel outside the rectangle.</summary>
    public int Bottom;

    /// <summary>Gets how wide the rectangle is, in pixels.</summary>
    /// <returns>The width.</returns>
    public int GetWidth()
    {
        return Right - Left;
    }

    /// <summary>Gets how tall the rectangle is, in pixels.</summary>
    /// <returns>The height.</returns>
    public int GetHeight()
    {
        return Bottom - Top;
    }
}
