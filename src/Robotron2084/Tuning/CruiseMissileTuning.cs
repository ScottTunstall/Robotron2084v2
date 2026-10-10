namespace Robotron2084.Tuning;

/// <summary>The cruise missile's colours, trail and cap (RRB10 CMMOV).</summary>
public static class CruiseMissileTuning
{
    // CMMOV never blits the CMPIC/CMP1 sprites; it writes VIDEO MEMORY
    // directly, one 16-bit word per step: `$AAAA` (two pixels of slot 10) at
    // the new coordinate and `$DDDD` (two of slot 13) at the coordinate it just
    // left. The video address is COLUMN-MAJOR — `column*256 + row`, proved by
    // RRG23's BORDER loop, which draws its vertical border with `STA ,X+` (one
    // address per ROW) — so a 16-bit word is TWO VERTICALLY ADJACENT PIXELS:
    // the mark is 1px wide and 2px tall.
    public const int HeadSlot = 0x0A;

    public const int MarkArcadeHeight = 2;

    /// <summary>
    ///     ROM CMMOV's mark: `LDD #$AAAA / LDY OX16,X / STD ,Y` — a 16-BIT write at
    ///     the video address. The video is column-major (`column*256 + row`) with 2 px
    ///     per byte (notes §52), so that paints TWO vertically adjacent addresses,
    ///     each holding 2 px: a **2x2 arcade px** block. The port drew 1x2.
    /// </summary>
    public const int MarkArcadeWidth = 2;

    /// <summary>ROM BCMCNT cap: a brain fires only while fewer than 8 missiles fly.</summary>
    public const int Max = 8;

    // $AA

    /// <summary>
    ///     The trail's length in marks — and the thing that stops it being a snake.
    ///     CMMOV keeps a ring of coordinates at `PD+6`..`SPSIZE` stepping by 2
    ///     (`SPSIZE` 31, initialised to `PD+6` = 13 → 13,15,..,29 = NINE entries)
    ///     and, EVERY STEP, erases the screen pixel at the entry it is about to
    ///     overwrite (`LDY #0 / LDA PD5,U / STY [A,U]`): the pixel nine steps back.
    ///     So the missile drags a rolling NINE-MARK tail. `CMKIL` then wipes the
    ///     remaining nine, so the tail vanishes with the missile.
    /// </summary>
    public const int TrailMarks = 9;

    public const int TrailSlot = 0x0D; // $DD
}
