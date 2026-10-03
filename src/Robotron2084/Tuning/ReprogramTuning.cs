namespace Robotron2084.Tuning;

/// <summary>A brain reprogramming a human (notes §46, §47).</summary>
public static class ReprogramTuning
{
    /// <summary>Blitter colour 1 for the reprogramming human ($AA = palette slot 10).</summary>
    public const int BackgroundSlot = 0x0A;

    /// <summary>ROM BRNL1's catch reach: brain and human top-left corners within this many arcade px on both axes.</summary>
    public const int CatchReachArcadePixels = 3;

    // The ROM's 20-iteration loop, each iteration doing TWO redraws of the
    // human (one with its Y lifted by SEED & 7, one with it dropped by the
    // same) separated by NAP 2. Both the brain and the human are drawn in a
    // single blitter colour for the duration: the brain as a solid block
    // (BRNON/BLKON, op $12) and the human as a solid rectangle plus a solid
    // silhouette (HUMON with D = $AABB, op $12 then op $1A).
    public const int Iterations = 20;   // ROM: `LDA #20 / STA PD4,U`

    public const int JitterPixels = 8;
    public const int RedrawsPerIteration = 2;

    // ROM: SEED & 7, i.e. 0..7
    /// <summary>
    /// Blitter colour 2 for the reprogramming human AND the BACKDROP the brain is drawn
    /// on ($BB = slot 11). For the brain this is a backdrop, not a replacement: ROM
    /// $1DAF fills the brain's rectangle with it ($DA61 / BLKON, op $12) and then blits
    /// the brain's own sprite over the top (JMP $D018) — notes §72.
    /// </summary>
    public const int ShapeSlot = 0x0B;

    public const int StepRomFrames = 3;  // NAP 2 + the beat vblank
}
