namespace Robotron2084.Tuning;

/// <summary>The spheroid's and quark's death burst (notes §64).</summary>
public static class ScoreBurstTuning
{
    // `ADDD #$0105` on the blitter's column:row destination: +1 column (2 px) and
    // +5 rows, so the "1000" sits down-right of where the enemy died.
    public const int PointsOffsetXArcadePixels = 2;

    public const int PointsOffsetYArcadePixels = 5;

    public const int PointsSteps = 30;

    public const int QuarkBurstPaletteIndex = 0x0D;

    public const int QuarkCount = 8;

    public const int QuarkPointsPaletteIndex = 0x0D;

    public const int SpheroidBurstPaletteIndex = 0x0A;

    // Spheroid / quark DEATH BURST (notes §64) — the spheroid's `CIRKP`
    // ("DRAW_SPHEROID_IN_DEATH_THROES", RRG23.ASM $12F1) and the quark's
    // `CIRKV`, which is `$1143: JMP $12FA` — the SAME routine entered after its
    // parameter setup. The enemy's own animation frames play as a SOLID silhouette, then
    // its "1000" sprite is displayed. `LDD #$FFAA` (spheroid) / `LDD #$DDDD`
    // (quark) sets one colour per phase, named by the disassembly's own comment
    // as "the same colour as the player score"; each is a PALETTE SLOT in the
    // doubled-nibble form, so both effects shimmer, because all the palette indices used
    // here (10, 13, 15) are colour-CYCLING ones.
    public const int SpheroidCount = 7;

    public const int SpheroidPointsPaletteIndex = 0x0F;
    public const int StepIntervalArcadeFrames = 2;
    // `LDA #7` — = the LAST animation frame's index

    // `LDA #8`

    // `NAP 2`

    // `LDA #$1E`

    // $AA = palette index 10

    // $FF = palette index 15

    // $DD = palette index 13

    // $DD = palette index 13
}
