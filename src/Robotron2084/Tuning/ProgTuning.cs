namespace Robotron2084.Tuning;

/// <summary>The prog's own colours (RRB10 PROG3/PROG4).</summary>
public static class ProgTuning
{
    public const int BackgroundPaletteIndex = 0x00;

    public const int GhostBackgroundPaletteIndex = 0x0E;

    // A prog is drawn as TWO blitter colour pairs, not as a sprite. Each beat:
    //   PROG3: `LDD #$EE00 / JSR HUMON` at the position it is LEAVING
    //   PROG4: `LDD #$00AA / JSR HUMON` at the position it is entering
    // i.e. a ghost (palette index 14 block, black shape) behind it and a black block
    // with a palette index 10 shape where it is. HUMON maps A (the high byte) to the
    // BLOCK through BLKON and B (the low byte) to the FIGURE through MPCTON, so
    // $EE00 really is a palette index 14 card carrying a black figure, and $00AA is its
    // exact inverse (RRB10:356).
    // The shadow ring: PD+8 is the index and the entries run PD+10, PD+12, ...
    // wrapping at SPSIZE = 31. PD = 7 (RRF.ASM:551) and SPSIZE = PSIZE+16 = 31
    // (RRF.ASM:565), so those offsets are bytes 17, 19 ... 29 — SEVEN entries,
    // and a ghost is erased by PCTOFF as its entry is reused 7 beats later.
    public const int GhostCount = 7;

    // $00 — black

    public const int GhostShapePaletteIndex = 0x00;
    public const int ShapePaletteIndex = 0x0A; // $AA

    // $EE

    // $00 — black
}
