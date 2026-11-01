namespace Robotron2084.Tuning;

/// <summary>The cruise missile's colours, the size of its mark, the length of its trail, and how many may fly at once.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRB10.ASM</c> <c>CMMOV</c></item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>DRAW_CRUISE_MISSILE</c> (<c>$20E9</c>)
///         </item>
///     </list>
///     <c>CMMOV</c> never draws the <c>CMPIC</c> and <c>CMP1</c> sprites. It writes video memory directly, one 16-bit word
///     for each step: <c>$AAAA</c> (two pixels of palette index 10) at the new coordinate, and <c>$DDDD</c> (two of
///     palette index 13) at the coordinate it has just left. Video addresses are column-major, <c>column*256 + row</c>,
///     which <c>RRG23.ASM</c>'s <c>BORDER</c> loop proves by drawing its vertical border with <c>STA ,X+</c> (one address
///     for each row). A 16-bit word is therefore two vertically adjacent pixels.
/// </remarks>
public static class CruiseMissileTuning
{
    /// <summary>The palette index of the missile's head.</summary>
    /// <remarks>The pixel colour <c>$AA</c> written by <c>LDD #$AAAA</c> at <c>$2101</c>.</remarks>
    public const int HeadPaletteIndex = 0x0A;

    /// <summary>How tall each mark of the missile is, in arcade pixels.</summary>
    /// <remarks>
    ///     A 16-bit write covers two vertically adjacent video addresses, so the mark is two arcade pixels tall.
    /// </remarks>
    public const int MarkArcadeHeight = 2;

    /// <summary>How wide each mark of the missile is, in arcade pixels.</summary>
    /// <remarks>
    ///     <c>LDD #$AAAA / LDY OX16,X / STD ,Y</c> is a 16-bit write at the video address. Video memory is column-major
    ///     with two pixels in each byte (notes §52), so the write paints two adjacent addresses holding two pixels each,
    ///     a block two arcade pixels across and two tall.
    /// </remarks>
    public const int MarkArcadeWidth = 2;

    /// <summary>The most cruise missiles that may fly at once.</summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c> <c>BCMCNT</c>. Disassembly: <c>CREATE_CRUISE_MISSILE</c> (<c>$2006</c>),
    ///     <c>CMPA #$08</c> at <c>$2012</c>.
    /// </remarks>
    public const int Max = 8;

    /// <summary>How many marks the missile's trail holds.</summary>
    /// <remarks>
    ///     <c>CMMOV</c> keeps a ring of coordinates from <c>PD+6</c> up to <c>SPSIZE</c> in steps of 2. <c>SPSIZE</c> is 31
    ///     and the ring starts at <c>PD+6</c>, which is 13, so the entries are 13, 15 and on to 29: nine of them. On every
    ///     step it erases the pixel at the entry it is about to overwrite (<c>LDY #0 / LDA PD5,U / STY [A,U]</c>), which is
    ///     the pixel nine steps back, so the missile drags a rolling tail of that length instead of leaving a snake.
    ///     <c>CMKIL</c> then wipes the rest, so the tail vanishes with the missile. Disassembly:
    ///     <c>DRAW_CRUISE_MISSILE</c> (<c>$20E9</c>), the trail index handling from <c>$20FC</c> to <c>$2116</c>.
    /// </remarks>
    public const int TrailMarks = 9;

    /// <summary>The palette index of the trail.</summary>
    /// <remarks>The pixel colour <c>$DD</c> written by <c>LDY #$DDDD</c> at <c>$20E9</c>.</remarks>
    public const int TrailPaletteIndex = 0x0D;
}
