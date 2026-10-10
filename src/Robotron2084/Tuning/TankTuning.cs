namespace Robotron2084.Tuning;

/// <summary>The tank's birth, growth and movement tuning.</summary>
public static class TankTuning
{
    /// <summary>
    ///     ROM `TNKSPD` = **2**, and it is a CONSTANT, not a wave value: `LDA #2 / STA
    ///     TNKSPD` in the per-level reset (RRG23:677). `TANK6` does `LDA TNKSPD / LDX
    ///     #TANKL / JMP SLEEP`, so the tank's process re-runs every TNKSPD vblanks — a
    ///     beat of 2 vblanks plus the frame it runs in = **3 fiftieths of a second**.
    /// </summary>
    public const int BeatIntervalRomFrames = 3;

    // $4CAC (`TNKDRP`): the tank's blitter address = the quark's + `ADDD #$0206`,
    // i.e. **+2 COLUMNS and +6 ROWS — not +2 px**. A column is 2 px (notes §53),
    // so the X offset is 4 arcade px = 8 screen units. The ROW is decremented
    // first UNLESS the quark sits exactly on the
    // top wall (`CMPB #YMIN / BEQ TNKDP1 / DECB`), so a tank normally lands 5 rows
    // below its quark and 6 on the top wall.
    /// <summary>ROM `TNKDRP`: the tank lands 2 COLUMNS right of its quark.</summary>
    public const int BirthOffsetColumns = 2;

    /// <summary>ROM `TNKDRP`: 5 rows below the quark on the `DECB` path (quark not on the top wall).</summary>
    public const int BirthOffsetRowsOffTopWall = 5;

    /// <summary>ROM `TNKDRP`: 6 rows below the quark when it sits on the top wall (the `DECB` path is skipped).</summary>
    public const int BirthOffsetRowsOnTopWall = 6;

    /// <summary>ROM `MTANK`: `NAP 12` per grow step.</summary>
    public const int GrowIntervalRomFrames = 12;

    /// <summary>ROM `MTANK`: grow animation frames `MTNKP1..4` — 4x4, 8x7, 8x8, 12x12 arcade px (notes §53).</summary>
    public const int GrowSteps = 4;

    /// <summary>
    ///     ROM `TANK1`: `LDA PD4,U / CLRB / ASRA / RORB / ADDD OX16,X` — the X step is
    ///     the direction byte HALVED, exactly like the player's table, so ±1 means 0.5
    ///     COLUMNS = **1 arcade px**; `ADDB PD5,U` steps Y by 1 ROW = 1 px. Both axes
    ///     therefore move one pixel per BEAT (the port moved one unit per tick, ~1.8x
    ///     too fast).
    /// </summary>
    public const int StepArcadePixels = 1;

    /// <summary>
    ///     ROM `MTANK`: each grow animation frame's own (dx,dy) — descriptor bytes 4 and 5 of
    ///     `MTNKP1..4`, in COLUMNS and ROWS. `MTANK` adds the CURRENT animation frame's delta
    ///     to the object's address and only then advances the pointer, so the mini tank
    ///     walks up-left as it grows and the full 14x16 tank lands centred on the drop
    ///     point (a total of -2 columns, -6 rows).
    /// </summary>
    public static readonly (int Columns, int Rows)[] GrowDeltas =
    [
        (-1, -1), // MTNKP1  $FF,$FF
        (0, -1), // MTNKP2  $00,$FF
        (-1, -2), // MTNKP3  $FF,$FE
        (0, -2) // MTNKP4  $00,$FE
    ];

    /// <summary>
    ///     The birth animation frames' sizes in arcade px, from the `MTNKP1..4` descriptors
    ///     (`FCB 2,4` / `4,7` / `4,8` / `6,12` — bytes wide x rows, and a byte is 2 px).
    ///     The ROM bounds and collides against the CURRENT animation frame, so a growing tank
    ///     is a smaller target than a full one.
    /// </summary>
    public static readonly (int Width, int Height)[] GrowSizes =
    [
        (4, 4), (8, 7), (8, 8), (12, 12)
    ];
}
