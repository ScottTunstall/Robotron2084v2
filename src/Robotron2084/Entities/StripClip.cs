using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Entities;

/// <summary>The playfield interior in the strip engine's units: X in arcade pixels, Y in rows.</summary>
/// <remarks>ROM: RRF.ASM's playfield-edge constants, here expressed as the wall rectangle. A strip
/// outside it is dropped, never scaled.</remarks>
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
