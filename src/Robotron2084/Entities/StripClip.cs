using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Entities;

/// <summary>The inside of the playfield, measured the way strips are: in arcade pixels across and in rows down. A strip outside it is not drawn.</summary>
/// <remarks>ROM: RRF.ASM's playfield-edge constants, given here as the rectangle inside the wall. A strip
/// outside it is left out. It is never squeezed to fit.</remarks>
public readonly record struct StripClip(int MinX, int MaxX, int MinY, int MaxY)
{
    /// <summary>Makes the clip for a playfield whose edges are given in port pixels.</summary>
    /// <param name="playfieldBounds">The inside of the playfield wall, in port pixels.</param>
    public static StripClip CreateFromPortPixels(Rectangle playfieldBounds) => new(
        playfieldBounds.Left / ScreenSize.SpecScale,
        playfieldBounds.Right / ScreenSize.SpecScale,
        playfieldBounds.Top / ScreenSize.SpecScale,
        playfieldBounds.Bottom / ScreenSize.SpecScale);
}
