using System.Runtime.InteropServices;

namespace Robotron2084.Core;

[StructLayout(LayoutKind.Sequential)]
internal struct Win32Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int GetWidth()
    {
        return Right - Left;
    }

    public int GetHeight()
    {
        return Bottom - Top;
    }
}
