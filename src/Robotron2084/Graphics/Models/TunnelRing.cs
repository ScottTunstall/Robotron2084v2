namespace Robotron2084.Graphics.Models;

/// <summary>One ring as drawn: its corners in ROM screen coordinates and the pair it used.</summary>
/// <param name="Left">The left edge, in arcade screen coordinates.</param>
/// <param name="Top">The top edge, in arcade screen coordinates.</param>
/// <param name="Right">The right edge, in arcade screen coordinates.</param>
/// <param name="Bottom">The bottom edge, in arcade screen coordinates.</param>
/// <param name="PackedColourPair">The two colours the ring was drawn in, packed into one number.</param>
internal readonly record struct TunnelRing(int Left, int Top, int Right, int Bottom, int PackedColourPair);
