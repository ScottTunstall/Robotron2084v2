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
