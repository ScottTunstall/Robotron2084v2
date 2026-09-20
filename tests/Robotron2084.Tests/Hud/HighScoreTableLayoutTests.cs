using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// The arcade high score table's geometry (notes §98.3) — the cursors RRTABLE's
/// <c>TABLE</c> passes to <c>PRJNK</c>: today's list at (26, 53) in the LARGE font,
/// 5 per column × 2 columns, 9 rows / 52 columns apart; the all-time list at
/// (20, 136) in the SMALL font, 12 per column × 3 columns, 7 rows / 40 columns
/// apart; the headers at column 53, rows 37 and 110; the top entry at (21, 122).
/// </summary>
public sealed class HighScoreTableLayoutTests
{
    [Fact]
    public void TodayList_MatchesTheRomsCursorAndSpacing()
    {
        Assert.Equal(10, HighScoreTableLayout.TodayRows);
        Assert.Equal((26, 53), HighScoreTableLayout.TodayPosition(1));
        Assert.Equal((26, 89), HighScoreTableLayout.TodayPosition(5));   // 4 rows down: +4 x 9
        Assert.Equal((78, 53), HighScoreTableLayout.TodayPosition(6));   // second column: +52
        Assert.Equal((78, 89), HighScoreTableLayout.TodayPosition(10));
    }

    [Fact]
    public void AllTimeList_MatchesTheRomsCursorAndSpacing()
    {
        Assert.Equal(36, HighScoreTableLayout.AllTimeRows);
        Assert.Equal((20, 136), HighScoreTableLayout.AllTimePosition(2));  // rank 1 is the top entry
        Assert.Equal((20, 213), HighScoreTableLayout.AllTimePosition(13)); // 11 rows down: +11 x 7
        Assert.Equal((60, 136), HighScoreTableLayout.AllTimePosition(14)); // second column: +40
        Assert.Equal((100, 136), HighScoreTableLayout.AllTimePosition(26)); // third column
        Assert.Equal((100, 213), HighScoreTableLayout.AllTimePosition(37));
    }

    [Fact]
    public void TheTablesRows_StayInsideTheFrame()
    {
        // The ROM's frame runs (col 6, row 13) to (col 145, row 239); every row the
        // screen prints must sit inside it, which is what these cursors are chosen for.
        (int column, int row) = HighScoreTableLayout.AllTimePosition(37);
        Assert.True(column > 6 && column < 145, $"last row at column {column}");
        Assert.True(row < 239, $"last row at row {row}");
        Assert.Equal((21, 122), (HighScoreTableLayout.TopColumn, HighScoreTableLayout.TopRow));
        Assert.True(HighScoreTableLayout.TodayHeaderRow < HighScoreTableLayout.TodayRow);
        Assert.True(HighScoreTableLayout.AllTimeHeaderRow < HighScoreTableLayout.AllTimeRow);
    }

    /// <summary>
    /// FRAMER's strokes, in arcade pixels: stroke 0 is (col 62, row 125)-(col 89, row 127)
    /// and every step takes the upper-left up-left a column and two rows while the
    /// lower-right goes the same way out, so a stroke is always 28 columns wide and 3
    /// rows tall. The two terminal points are the ROM's own: $060D = (6, 13) for the
    /// growing pass and $0E1D = (14, 29) for the erase one.
    /// </summary>
    [Fact]
    public void TheFramesStrokes_MatchTheRomsTerminalPoints()
    {
        Assert.Equal((124, 125, 179, 127), HighScoreTableLayout.FrameStroke(HighScoreTableLayout.FrameFirstStroke));
        Assert.Equal((12, 13, 291, 239), HighScoreTableLayout.FrameStroke(HighScoreTableLayout.FrameLastStroke));
        Assert.Equal((28, 29, 275, 223), HighScoreTableLayout.FrameStroke(HighScoreTableLayout.FrameEraseLastStroke));

        Assert.Equal(57, HighScoreTableLayout.FrameStrokeCount);
        Assert.Equal(49, HighScoreTableLayout.FrameEraseStrokeCount);
    }

    /// <summary>
    /// What the two passes leave is the band between the terminal points — eight columns
    /// (sixteen pixels) wide and sixteen rows tall all the way round, which is the
    /// author's "hatched wall" (notes §98.5).
    /// </summary>
    [Fact]
    public void TheBandLeftByTheErasePass_IsSixteenPixelsThickAllRound()
    {
        (int left, int top, int right, int bottom) = HighScoreTableLayout.FrameStroke(HighScoreTableLayout.FrameLastStroke);
        (int innerLeft, int innerTop, int innerRight, int innerBottom) = HighScoreTableLayout.FrameStroke(HighScoreTableLayout.FrameEraseLastStroke);

        Assert.Equal(16, innerLeft - left);
        Assert.Equal(16, innerTop - top);
        Assert.Equal(16, right - innerRight);
        Assert.Equal(16, bottom - innerBottom);
    }

    /// <summary>
    /// MARQ lights exactly one pixel of every packed pair — the flavour's high nibble on
    /// one and its low nibble on the next — which over the whole frame is this single
    /// checkerboard (notes §98.5).
    /// </summary>
    [Fact]
    public void TheFramesHatch_LightsOnePixelOfEveryPairBothWays()
    {
        Assert.False(HighScoreTableLayout.FramePixelIsLit(0, 0));
        Assert.True(HighScoreTableLayout.FramePixelIsLit(1, 0));
        Assert.True(HighScoreTableLayout.FramePixelIsLit(0, 1));
        Assert.False(HighScoreTableLayout.FramePixelIsLit(1, 1));
        Assert.Equal(HighScoreTableLayout.FramePixelIsLit(200, 200), !HighScoreTableLayout.FramePixelIsLit(200, 201));
    }
}
