namespace Robotron2084.Tuning;

/// <summary>A brain reprogramming a family member into a prog: how long it takes, how the pair shake, and what colours they are drawn in.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRB10.ASM</c> (notes §46 and §47)</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>) and
///             <c>DRAW_PROG_STARTING_TO_SHAKE</c> (<c>$1D37</c>)
///         </item>
///     </list>
///     The ROM runs a loop of 20 passes. Each pass redraws the family member twice, once with its Y lifted by
///     <c>SEED &amp; 7</c> and once with it dropped by the same amount, with a <c>NAP 2</c> between the redraws. For the
///     whole loop the brain and the family member are each drawn in a single blitter colour: the brain as a solid block
///     (<c>BRNON</c> and <c>BLKON</c>, op <c>$12</c>) and the family member as a solid rectangle plus a solid silhouette
///     (<c>HUMON</c> with <c>D = $AABB</c>, op <c>$12</c> then op <c>$1A</c>).
/// </remarks>
public static class ReprogramTuning
{
    /// <summary>The palette index of the block drawn behind the family member while it is reprogrammed.</summary>
    /// <remarks>Blitter colour 1 of the family member, the high byte of <c>$AABB</c>: <c>$AA</c>.</remarks>
    public const int BackgroundPaletteIndex = 0x0A;

    /// <summary>
    ///     How close sideways the brain's and family member's top-left corners must be for the brain to catch the family member, in
    ///     columns.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, <c>ADDA #3 / CMPA #6 / BLS</c>. Disassembly:
    ///     <c>ANIMATE_BRAIN</c> (<c>$1C11</c>) at <c>$1C52</c>.
    /// </remarks>
    public const int CatchReachColumns = 3;

    /// <summary>
    ///     How close up and down the brain's and family member's top-left corners must be for the brain to catch the family member, in
    ///     rows.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c> <c>BRNL1</c>, <c>ADDB #3 / CMPB #$6 / BHI</c>. Disassembly:
    ///     <c>ANIMATE_BRAIN</c> (<c>$1C11</c>) at <c>$1C4A</c>.
    /// </remarks>
    public const int CatchReachRows = 3;

    /// <summary>How many passes the reprogramming loop makes.</summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c>, <c>LDA #20 / STA PD4,U</c>. Disassembly:
    ///     <c>BEGIN_PROGRAMMING_FAMILY_MEMBER</c> (<c>$1CC2</c>), <c>LDA #$14</c> at <c>$1D1C</c>.
    /// </remarks>
    public const int Iterations = 20;

    /// <summary>How many different amounts the family member can be lifted or dropped by while it shakes.</summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c>, <c>SEED &amp; 7</c>, which makes a number from 0 to 7. Disassembly:
    ///     <c>DRAW_PROG_STARTING_TO_SHAKE</c> (<c>$1D37</c>), <c>ANDB #$07</c> at <c>$1D39</c>.
    /// </remarks>
    public const int JitterPixels = 8;

    /// <summary>How many times the family member is redrawn in each pass of the loop.</summary>
    /// <remarks>One redraw lifted by the random amount and one dropped by the same amount.</remarks>
    public const int RedrawsPerIteration = 2;

    /// <summary>The palette index of the figure drawn for the family member, and of the backdrop the brain is drawn on.</summary>
    /// <remarks>
    ///     Blitter colour 2 for the family member, the low byte of <c>$AABB</c>: <c>$BB</c>. For the brain it is a
    ///     backdrop, not a replacement: <c>$1DAF</c> fills the brain's rectangle with it (<c>$DA61</c> and
    ///     <c>BLKON</c>, op <c>$12</c>) and then blits the brain's own sprite over the top (<c>JMP $D018</c>), notes §72.
    ///     Disassembly: <c>DRAW_BRAIN_IN_PROGGING_STATE</c> (<c>$1DAF</c>), <c>LDB #$BB</c> at <c>$1DAF</c>.
    /// </remarks>
    public const int ShapePaletteIndex = 0x0B;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> pass between one
    ///     redraw of the shaking family member and the next.
    /// </summary>
    /// <remarks>Original source: <c>RRB10.ASM</c>, <c>NAP 2</c>. A <c>NAP n</c> waits one more frame than its number.</remarks>
    public const int StepIntervalArcadeFrames = 3;
}
