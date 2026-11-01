namespace Robotron2084.Tuning;

/// <summary>How a tank is born, how it grows, and how fast it moves.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRTK4.ASM</c> <c>TNKDRP</c> (the birth), <c>MTANK</c> (the growing) and <c>TANK1</c> (the movement), notes §53</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>SPAWN_TANK</c> (<c>$4CAC</c>) and <c>ANIMATE_TANK</c>
///             (<c>$4D99</c>)
///         </item>
///     </list>
/// </remarks>
public static class TankTuning
{
    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of a tank and the next.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TANK6</c>, <c>LDA TNKSPD / LDX #TANKL / JMP SLEEP</c>. <c>TNKSPD</c> is 2
    ///     and is a constant, not a wave value: <c>LDA #2 / STA TNKSPD</c> in the per-wave reset (<c>RRG23.ASM</c> line
    ///     677). The tank's process therefore runs again every <c>TNKSPD</c> vertical blanks, which is a wait of 2 plus the
    ///     frame it runs in. Disassembly: <c>ANIMATE_TANK</c> (<c>$4D99</c>), <c>LDA $EF</c> at <c>$4DEA</c>.
    /// </remarks>
    public const int BeatIntervalArcadeFrames = 3;

    /// <summary>How far right of the quark that dropped it the tank is born, in columns.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TNKDRP</c>, <c>ADDD #$0206</c> on the quark's blitter address. The sideways
    ///     part is two columns, not two pixels. A column is two arcade pixels (notes §53), so the offset is four arcade
    ///     pixels. Disassembly: <c>SPAWN_TANK</c> (<c>$4CAC</c>), <c>$4CDB</c>.
    /// </remarks>
    public const int BirthOffsetColumns = 2;

    /// <summary>How far below the quark the tank is born, in rows, when the quark is not on the top wall.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TNKDRP</c>. The row is decremented first, <c>CMPB #YMIN / BEQ TNKDP1 /
    ///     DECB</c>, which leaves one row less than the offset of the top-wall case. Disassembly: <c>SPAWN_TANK</c>
    ///     (<c>$4CAC</c>), <c>$4CDB</c>.
    /// </remarks>
    public const int BirthOffsetRowsOffTopWall = 5;

    /// <summary>How far below the quark the tank is born, in rows, when the quark is on the top wall.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TNKDRP</c>. The decrement is skipped when the quark is exactly on the top
    ///     wall. Disassembly: <c>SPAWN_TANK</c> (<c>$4CAC</c>), <c>$4CDB</c>.
    /// </remarks>
    public const int BirthOffsetRowsOnTopWall = 6;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> each animation frame
    ///     of the tank's growing shows for.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>MTANK</c>, <c>NAP 12</c>. Disassembly: <c>INITIALISE_ALL_TANKS</c>
    ///     (<c>$4D10</c>), <c>LDA #$0C</c> at <c>$4D83</c>.
    /// </remarks>
    public const int GrowIntervalArcadeFrames = 12;

    /// <summary>How many animation frames the tank shows while it grows.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>MTANK</c>, the animation frames <c>MTNKP1</c> to <c>MTNKP4</c>, which are
    ///     4 by 4, 8 by 7, 8 by 8 and 12 by 12 arcade pixels (notes §53).
    /// </remarks>
    public const int GrowSteps = 4;

    /// <summary>How far the tank goes along each axis on one beat, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TANK1</c>, <c>LDA PD4,U / CLRB / ASRA / RORB / ADDD OX16,X</c>. The
    ///     sideways step is the direction byte halved, as in the player's table, so plus or minus one is half a column,
    ///     which is one arcade pixel. <c>ADDB PD5,U</c> steps the Y by one row, which is also one arcade pixel.
    ///     Disassembly: <c>ANIMATE_TANK</c> (<c>$4D99</c>), <c>$4DA2</c> to <c>$4DAD</c>.
    /// </remarks>
    public const int StepArcadePixels = 1;

    /// <summary>
    ///     How far each animation frame of the tank's growing moves it, as columns sideways and rows up and down,
    ///     one pair for each animation frame.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>MTANK</c>, bytes 4 and 5 of each descriptor <c>MTNKP1</c> to
    ///     <c>MTNKP4</c>, which are <c>$FF,$FF</c>, <c>$00,$FF</c>, <c>$FF,$FE</c> and <c>$00,$FE</c>. <c>MTANK</c> adds
    ///     the current animation frame's pair to the object's address and only then moves on to the next animation frame,
    ///     so the little tank walks up and to the left as it grows. The full-size tank lands centred on the drop point.
    /// </remarks>
    public static readonly (int Columns, int Rows)[] GrowDeltas =
    [
        (-1, -1),
        (0, -1),
        (-1, -2),
        (0, -2)
    ];

    /// <summary>How wide and how tall each animation frame of the tank's growing is, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>MTANK</c>, the descriptors of <c>MTNKP1</c> to <c>MTNKP4</c>
    ///     (<c>FCB 2,4</c>, <c>4,7</c>, <c>4,8</c> and <c>6,12</c>), which give the width in bytes and the height in rows.
    ///     A byte is two arcade pixels. The ROM bounds the tank and tests collisions against the current animation
    ///     frame, so a growing tank is a smaller target than a full one.
    /// </remarks>
    public static readonly (int Width, int Height)[] GrowSizes =
    [
        (4, 4), (8, 7), (8, 8), (12, 12)
    ];
}
