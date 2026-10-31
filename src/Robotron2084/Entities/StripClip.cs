using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Entities;

/// <summary>The inside of the playfield, in arcade pixels. A strip outside it is not drawn.</summary>
/// <param name="MinX">
///     The left edge, in arcade pixels from the left side of the screen. A strip whose left edge is here is inside.
/// </param>
/// <param name="MaxX">
///     The right edge, in arcade pixels from the left side of the screen. It is the first pixel outside, so a strip may end
///     here but not go past it.
/// </param>
/// <param name="MinY">
///     The top edge, in arcade pixels from the top of the screen. A strip whose top edge is here is inside.
/// </param>
/// <param name="MaxY">
///     The bottom edge, in arcade pixels from the top of the screen. It is the first pixel outside, so a strip may end here
///     but not go past it.
/// </param>
/// <remarks>
///     A strip may touch the left and top edges, and its far edge may touch the right and bottom edges, but nothing may go
///     past them.
///     It is never squeezed to fit.
///     <list type="bullet">
///         <item>Original source: <c>RRF.ASM</c>, the constants that mark the edges of the playfield</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public readonly record struct StripClip(int MinX, int MaxX, int MinY, int MaxY)
{
    /// <summary>Makes the clip for a playfield whose edges are given in port pixels.</summary>
    /// <param name="playfieldBounds">The inside of the playfield wall, in port pixels.</param>
    public static StripClip CreateFromPortPixels(Rectangle playfieldBounds)
    {
        return new StripClip(
            playfieldBounds.Left / ScreenSize.PortPixelsPerArcadePixel,
            playfieldBounds.Right / ScreenSize.PortPixelsPerArcadePixel,
            playfieldBounds.Top / ScreenSize.PortPixelsPerArcadePixel,
            playfieldBounds.Bottom / ScreenSize.PortPixelsPerArcadePixel);
    }
}
