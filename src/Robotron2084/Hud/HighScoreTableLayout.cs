namespace Robotron2084.Hud;

/// <summary>
/// The arcade high score table's geometry (notes §98.3), read out of RRTABLE's
/// <c>TABLE</c>/<c>PRJNK</c> calls. Cursors are the ROM's own (column, row)
/// units — a column is two pixels — and every number here is a ROM constant:
/// <list type="bullet">
/// <item>TODAY'S list: LARGE font at <c>LDX #$1A35</c> = column 26, row 53,
/// <c>$502</c> = 5 per column × 2 columns, <c>$934</c> = 9 rows / 52 columns apart;</item>
/// <item>ALL-TIME list: SMALL font at <c>LDX #$1488</c> = column 20, row 136,
/// <c>$C03</c> = 12 per column × 3 columns, <c>$728</c> = 7 rows / 40 columns apart;</item>
/// <item>the headers (<c>SCRMEP</c>) at column 53, rows 37 and 110;</item>
/// <item>the operator's top entry at <c>LDX #$157A</c> = column 21, row 122.</item>
/// </list>
/// A row is printed as " N) " (message 111 <c>INDMEP</c>), then the three
/// initials, then the score at a FIXED offset from the post-rank cursor —
/// 11 columns in the large font, 12 in the small one (RRTABLE's
/// <c>LDA PD,U / LDB #3 / MUL / LSRB / INCB</c>, in column units).
/// </summary>
public static class HighScoreTableLayout
{
    // Above this many entries the ROM's extra "5 ENTRIES MAXIMUM" rule applies
    // (notes §98.4); the display itself only ever shows the first N.
    public const int TodayColumn = 26;
    public const int TodayRow = 53;
    public const int TodayPerColumn = 5;
    public const int TodayColumns = 2;
    public const int TodayRowStep = 9;
    public const int TodayColumnStep = 52;
    public const int TodayScoreOffsetColumns = 11;

    public const int AllTimeColumn = 20;
    public const int AllTimeRow = 136;
    public const int AllTimePerColumn = 12;
    public const int AllTimeColumns = 3;
    public const int AllTimeRowStep = 7;
    public const int AllTimeColumnStep = 40;
    public const int AllTimeScoreOffsetColumns = 12;

    public const int HeaderColumn = 53;
    public const int TodayHeaderRow = 37;
    public const int AllTimeHeaderRow = 110;

    public const int TopColumn = 21;
    public const int TopRow = 122;

    /// <summary>Rows the screen has room for (the ROM prints exactly these many).</summary>
    public const int TodayRows = TodayPerColumn * TodayColumns;
    public const int AllTimeRows = AllTimePerColumn * AllTimeColumns;

    // ---- the frame (`FRAMER` → `MARQ`, notes §98.5) -------------------------------
    // FRAMER hands MARQ two inclusive corners and walks them apart, two strokes a ROM
    // frame: stroke 0 is (col 62, row 125)-(col 89, row 127), and every step takes the
    // upper-left up-left by one column and two rows while the lower-right goes the same
    // way out, so a stroke is always 28 columns wide and 3 rows tall. Pass 1 stops when
    // the upper-left reaches the terminal point `$060D` = (col 6, row 13); the erase pass
    // restarts at stroke 0 with flavour 0 (black) and stops at `$0E1D` = (col 14, row 29).
    // What is left visible is the band between those two — 8 columns and 16 rows thick.
    public const int FrameStartColumn = 62;
    public const int FrameStartRow = 125;
    public const int FrameHalfWidthColumns = 27;
    public const int FrameStrokesPerRomFrame = 2;
    public const int FrameFirstStroke = 0;
    public const int FrameLastStroke = 56;       // (col 6, row 13)
    public const int FrameEraseLastStroke = 48;  // (col 14, row 29)
    public const int FrameStrokeCount = FrameLastStroke + 1;
    public const int FrameEraseStrokeCount = FrameEraseLastStroke + 1;

    /// <summary>
    /// Stroke <paramref name="stroke"/>'s rectangle in ARCADE PIXELS, inclusive: MARQ's
    /// corners are (column, row) units and one column is two pixels.
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) FrameStroke(int stroke) =>
    (
        2 * (FrameStartColumn - stroke),
        FrameStartRow - (2 * stroke),
        2 * (FrameStartColumn + FrameHalfWidthColumns + stroke) + 1,
        FrameStartRow + 2 + (2 * stroke)
    );

    /// <summary>
    /// MARQ's hatch: of every pair of pixels its two horizontal passes and its two
    /// vertical passes light exactly ONE — the outer pixel taking the flavour's high
    /// nibble, the inner one the low.
    /// </summary>
    public static bool FramePixelIsLit(int arcadeX, int arcadeY) => ((arcadeX + arcadeY) & 1) != 0;

    /// <summary>
    /// The palette slot stroke <paramref name="stroke"/> is drawn in. MARQ's flavour is a
    /// packed byte whose nibbles are the outer and inner pixel's slot, and FRAMER walks it
    /// DOWN a stroke at a time: `GETA`'s <c>SUBA #$11</c> subtracts IN PLACE (the `$88` is
    /// only reloaded once that subtraction reaches zero), so the growing pass lays down
    /// <c>$11, $88, $77, $66, $55, $44, $33, $22, $11 …</c> — A SLOT PER STROKE, eight of
    /// them repeating. That is why <c>LOOPP</c> shifts a register of exactly EIGHT slots, and
    /// why the arcade's wall reads as several colours at once (author: "the arcade wall is
    /// split into multiple different cycling colours"): the eight visible strokes are slots
    /// eight to one, each showing a COLTAB colour three frames apart in the walk.
    /// </summary>
    public static int FrameStrokeSlot(int stroke) =>
        stroke <= 0 ? 1 : 8 - ((stroke - 1) % 8);

    /// <summary>The (column, row) of the ROM's cursor for today's rank <paramref name="index"/> (1-based).</summary>
    public static (int Column, int Row) TodayPosition(int index)
    {
        int zero = index - 1;
        return (
            TodayColumn + (zero / TodayPerColumn * TodayColumnStep),
            TodayRow + (zero % TodayPerColumn * TodayRowStep));
    }

    /// <summary>The (column, row) of the ROM's cursor for all-time rank <paramref name="index"/> (1-based; rank 1 is the top entry).</summary>
    public static (int Column, int Row) AllTimePosition(int index)
    {
        int zero = index - 2; // the list starts at rank 2 (rank 1 is the top entry)
        return (
            AllTimeColumn + (zero / AllTimePerColumn * AllTimeColumnStep),
            AllTimeRow + (zero % AllTimePerColumn * AllTimeRowStep));
    }
}
