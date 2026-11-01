namespace Robotron2084.Tuning;

/// <summary>The colours of a prog and of the fading ghost it leaves behind, and how many ghosts it leaves.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRB10.ASM</c> <c>PROG3</c> and <c>PROG4</c>, <c>RRF.ASM</c> <c>SPSIZE</c></item>
///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>)</item>
///     </list>
///     A prog is not drawn as a sprite. It is drawn as two colour pairs, each made of a block colour and a figure
///     colour, which <c>HUMON</c> maps through <c>BLKON</c> (the block, from the high byte) and <c>MPCTON</c> (the
///     figure, from the low byte). On each beat <c>PROG3</c> (<c>LDD #$EE00 / JSR HUMON</c>) draws the ghost at the
///     place the prog is leaving, and <c>PROG4</c> (<c>LDD #$00AA / JSR HUMON</c>) draws the prog where it is arriving.
///     The two pairs are exact inverses of each other (<c>RRB10.ASM</c> line 356).
/// </remarks>
public static class ProgTuning
{
    /// <summary>The palette index of the block drawn behind the prog itself.</summary>
    /// <remarks>The high byte of the colour pair <c>$00AA</c> in <c>PROG4</c>: black.</remarks>
    public const int BackgroundPaletteIndex = 0x00;

    /// <summary>The palette index of the block drawn behind a ghost.</summary>
    /// <remarks>The high byte of the colour pair <c>$EE00</c> in <c>PROG3</c>.</remarks>
    public const int GhostBackgroundPaletteIndex = 0x0E;

    /// <summary>How many ghosts a prog leaves behind it before the oldest one is erased.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>PD</c> and <c>SPSIZE</c> (line 551 and line 565)</item>
    ///         <item>
    ///             Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>), the trail of pointers written
    ///             at <c>$1EFF</c>
    ///         </item>
    ///     </list>
    ///     The ring of positions starts at <c>PD+8</c> and its entries run <c>PD+10</c>, <c>PD+12</c> and so on,
    ///     wrapping at <c>SPSIZE</c>. <c>PD</c> is 7 and <c>SPSIZE</c> is <c>PSIZE+16</c>, so the entries sit at bytes 17,
    ///     19 and on to 29, which makes seven. <c>PCTOFF</c> erases a ghost when its entry is used again, seven beats
    ///     later.
    /// </remarks>
    public const int GhostCount = 7;

    /// <summary>The palette index of the figure drawn in a ghost.</summary>
    /// <remarks>The low byte of the colour pair <c>$EE00</c> in <c>PROG3</c>: black.</remarks>
    public const int GhostShapePaletteIndex = 0x00;

    /// <summary>The palette index of the prog's own figure.</summary>
    /// <remarks>The low byte of the colour pair <c>$00AA</c> in <c>PROG4</c>.</remarks>
    public const int ShapePaletteIndex = 0x0A;
}
