namespace Robotron2084.Graphics;

/// <summary>One ring as drawn: its corners in ROM screen coordinates and the pair it used.</summary>
internal readonly record struct TunnelRing(int Left, int Top, int Right, int Bottom, int PackedColourPair);
