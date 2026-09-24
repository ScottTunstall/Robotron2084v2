namespace Robotron2084.Entities;

/// <summary>The playfield interior in the strip engine's units: X in art pixels, Y in rows.</summary>
/// <remarks>ROM: RRF.ASM's playfield-edge constants, here expressed as the wall rectangle. A strip
/// outside it is dropped, never scaled.</remarks>
public readonly record struct StripClip(int MinX, int MaxX, int MinY, int MaxY);
