namespace Robotron2084.Tuning;

/// <summary>A brain reprogramming a human (notes §46, §47).</summary>
public static class ReprogramTuning
{
    /// <summary>Blitter colour 1 for the reprogramming human ($AA = palette slot 10).</summary>
    public const int BackgroundSlot = 0x0A;

    /// <summary>How close sideways the brain's and human's top-left corners must be for the brain to catch the human, in columns.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, <c>ADDA #3 / CMPA #6 / BLS</c>. Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>) at <c>$1C52</c>.</remarks>
    public const int CatchReachColumns = 3;

    /// <summary>How close up and down the brain's and human's top-left corners must be for the brain to catch the human, in rows.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, <c>ADDB #3 / CMPB #$6 / BHI</c>. Disassembly: <c>ANIMATE_BRAIN</c> (<c>$1C11</c>) at <c>$1C4A</c>.</remarks>
    public const int CatchReachRows = 3;

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
