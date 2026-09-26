namespace Robotron2084.Tuning;

/// <summary>The spheroid's and quark's death burst (notes §64).</summary>
public static class ScoreBurstTuning
{
    // Spheroid / quark DEATH BURST (notes §64) — the spheroid's `CIRKP`
    // ("DRAW_SPHEROID_IN_DEATH_THROES", RRG23.ASM $12F1) and the quark's
    // `CIRKV`, which is `$1143: JMP $12FA` — the SAME routine entered after its
    // parameter setup. The enemy's own pictures play as a SOLID silhouette, then
    // its "1000" picture is displayed. `LDD #$FFAA` (spheroid) / `LDD #$DDDD`
    // (quark) sets one colour per phase, named by the disassembly's own comment
    // as "the same colour as the player score"; each is a PALETTE SLOT in the
    // doubled-nibble form, so both effects shimmer, because all the slots used
    // here (10, 13, 15) are colour-CYCLING ones.
    public const int SpheroidCount = 7; // `LDA #7` — = the LAST picture's index

    public const int QuarkCount = 8;    // `LDA #8`

    public const int RomFramesPerStep = 2; // `NAP 2`

    public const int PointsSteps = 30;  // `LDA #$1E`

    public const int SpheroidBurstSlot = 0x0A;  // $AA = slot 10

    public const int SpheroidPointsSlot = 0x0F; // $FF = slot 15

    public const int QuarkBurstSlot = 0x0D;     // $DD = slot 13

    public const int QuarkPointsSlot = 0x0D;    // $DD = slot 13

    // `ADDD #$0105` on the blitter's column:row destination: +1 column (2 px) and
    // +5 rows, so the "1000" sits down-right of where the enemy died.
    public const int PointsOffsetXSpecPixels = 2;

    public const int PointsOffsetYSpecPixels = 5;
}
