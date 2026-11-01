namespace Robotron2084.Tuning;

/// <summary>The burst of colour and the points value shown when a spheroid or a quark is killed.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>CIRKP</c> for the spheroid, and <c>CIRKV</c> for the quark, which is
///             <c>$1143: JMP $12FA</c> and so the same routine entered after its own setup (notes §64)
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>DRAW_SPHEROID_IN_DEATH_THROES</c> (<c>$12F1</c>) and
///             <c>DRAW_SPHEROID_POINTS_VALUE</c> (<c>$131E</c>)
///         </item>
///     </list>
///     The enemy's own animation frames play as a solid silhouette, and then its points sprite is shown. One colour is
///     set for each phase (<c>LDD #$FFAA</c> for the spheroid, <c>LDD #$DDDD</c> for the quark), which the
///     disassembly's comment names as the same colour as the player score. Each is a palette slot in doubled-nibble
///     form. All the palette indices used here are colour-cycling ones, so both effects shimmer.
/// </remarks>
public static class ScoreBurstTuning
{
    /// <summary>How far right of where the enemy died the points sprite is drawn, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>ADDD #$0105</c> on the blitter's column and row destination, which is one column to the
    ///     right. Disassembly: <c>DRAW_SPHEROID_POINTS_VALUE</c> (<c>$131E</c>), <c>$1320</c>.
    /// </remarks>
    public const int PointsOffsetXArcadePixels = 2;

    /// <summary>How far below where the enemy died the points sprite is drawn, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>ADDD #$0105</c> on the blitter's column and row destination, which is five rows down.
    ///     Disassembly: <c>DRAW_SPHEROID_POINTS_VALUE</c> (<c>$131E</c>), <c>$1320</c>.
    /// </remarks>
    public const int PointsOffsetYArcadePixels = 5;

    /// <summary>How many steps the points sprite stays on the screen.</summary>
    /// <remarks>
    ///     Original source: <c>LDA #$1E</c>. Disassembly: <c>DRAW_SPHEROID_POINTS_VALUE</c> (<c>$131E</c>), <c>$1325</c>.
    /// </remarks>
    public const int PointsSteps = 30;

    /// <summary>The palette index of the quark's silhouette.</summary>
    /// <remarks>The colour <c>$DD</c> from <c>LDD #$DDDD</c>.</remarks>
    public const int QuarkBurstPaletteIndex = 0x0D;

    /// <summary>The number of steps the quark's silhouette is shown for.</summary>
    /// <remarks>Original source: <c>LDA #8</c>.</remarks>
    public const int QuarkCount = 8;

    /// <summary>The palette index of the quark's points sprite.</summary>
    /// <remarks>The colour <c>$DD</c> from <c>LDD #$DDDD</c>.</remarks>
    public const int QuarkPointsPaletteIndex = 0x0D;

    /// <summary>The palette index of the spheroid's silhouette.</summary>
    /// <remarks>The colour <c>$AA</c> from <c>LDD #$FFAA</c>.</remarks>
    public const int SpheroidBurstPaletteIndex = 0x0A;

    /// <summary>The number of steps the spheroid's silhouette is shown for.</summary>
    /// <remarks>
    ///     Original source: <c>LDA #7</c>, which is the index of the last animation frame. Disassembly:
    ///     <c>DRAW_SPHEROID_IN_DEATH_THROES</c> (<c>$12F1</c>), <c>$12F6</c>.
    /// </remarks>
    public const int SpheroidCount = 7;

    /// <summary>The palette index of the spheroid's points sprite.</summary>
    /// <remarks>The colour <c>$FF</c> from <c>LDD #$FFAA</c>.</remarks>
    public const int SpheroidPointsPaletteIndex = 0x0F;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     step of the burst and the next.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>NAP 2</c>. Disassembly: <c>DRAW_SPHEROID_IN_DEATH_THROES</c> (<c>$12F1</c>), <c>$1316</c>.
    /// </remarks>
    public const int StepIntervalArcadeFrames = 2;
}
