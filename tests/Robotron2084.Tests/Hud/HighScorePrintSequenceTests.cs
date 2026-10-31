using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
///     RRTABLE's printing order (notes §98.6) — the page BUILDS ITSELF: <c>MAKP LOOPP</c>
///     and <c>JSR FRAMER</c> put the wall on an empty screen, then <c>PRJNK</c> prints the
///     lists FOUR ROWS A ARCADE FRAME (<c>LDA #4 / STA PD+17,U</c> + <c>NAP 1</c>), with the top
///     entry between them and the headers last, and only then do the ramp processes start.
/// </summary>
public sealed class HighScorePrintSequenceTests
{
    private const int Today = 10;
    private const int AllTime = 36;

    [Fact]
    public void NothingIsPrinted_UntilTheFramesTwoPassesHaveFinished()
    {
        var highScorePrintSequence = new HighScorePrintSequence();

        for (var tick = 0; tick < 200; tick++) highScorePrintSequence.Tick(false, Today, AllTime);

        Assert.False(highScorePrintSequence.IsPrinting());
        Assert.Equal(0, highScorePrintSequence.TodayRows);
        Assert.Equal(0, highScorePrintSequence.AllTimeRows);
        Assert.False(highScorePrintSequence.TopPrinted);
        Assert.False(highScorePrintSequence.HeadersPrinted);
        Assert.False(highScorePrintSequence.IsDone());
    }

    [Fact]
    public void TheFirstGroupPrintsWithTheFramesLastStroke()
    {
        var highScorePrintSequence = new HighScorePrintSequence();

        highScorePrintSequence.Tick(true, Today, AllTime);

        // PRJNK's first group prints the moment it is called (TOD44 falls into TOD33).
        Assert.Equal(4, highScorePrintSequence.TodayRows);
        Assert.True(highScorePrintSequence.IsPrinting());
    }

    [Fact]
    public void FourRowsPrintPerArcadeFrame_AndThePageEndsWithTheHeaders()
    {
        var highScorePrintSequence = new HighScorePrintSequence();
        highScorePrintSequence.Tick(true, Today, AllTime);
        Assert.Equal(4, highScorePrintSequence.TodayRows);

        // A fiftieth of a second is 6/5 of a port tick.
        highScorePrintSequence.Tick(false, Today, AllTime);
        Assert.Equal(4, highScorePrintSequence.TodayRows); // 5 clock units — not a frame yet

        highScorePrintSequence.Tick(false, Today, AllTime);
        Assert.Equal(8, highScorePrintSequence.TodayRows); // 10 clock units — the second group

        // Today's list runs out on its third group, and the top entry plus the FIRST
        // all-time group print straight after it, with no sleep in between: TOD22 leaves
        // PRJNK without sleeping, and TABLE's NOINTS starts the second list at once.
        TickUntil(highScorePrintSequence, p => p.TodayRows == Today);
        Assert.True(highScorePrintSequence.TopPrinted);
        Assert.Equal(4, highScorePrintSequence.AllTimeRows);

        TickUntil(highScorePrintSequence, p => p.HeadersPrinted);
        Assert.Equal(AllTime, highScorePrintSequence.AllTimeRows);
        Assert.True(highScorePrintSequence.IsDone());

        // The page is a still screen once printed.
        TickFrames(highScorePrintSequence, 10);
        Assert.Equal(AllTime, highScorePrintSequence.AllTimeRows);
        Assert.True(highScorePrintSequence.IsDone());
    }

    [Fact]
    public void TheWholePagePrintsInAboutTenArcadeFrames()
    {
        // 10 rows = 3 groups (2 sleeps) and 36 rows = 9 groups (8 sleeps): ten fiftieths of a second
        // = twelve ticks, a fifth of a second of printing after the frame's own 53.
        var highScorePrintSequence = new HighScorePrintSequence();
        var ticks = 0;
        while (!highScorePrintSequence.IsDone() && ticks < 200)
        {
            highScorePrintSequence.Tick(true, Today, AllTime);
            ticks++;
        }

        Assert.True(highScorePrintSequence.IsDone());
        Assert.InRange(ticks, 11, 14);
    }

    [Fact]
    public void AnEmptyPageFinishesAsSoonAsItStarts()
    {
        var highScorePrintSequence = new HighScorePrintSequence();

        highScorePrintSequence.Tick(true, 0, 0);

        Assert.True(highScorePrintSequence.HeadersPrinted);
        Assert.True(highScorePrintSequence.IsDone());
        Assert.Equal(0, highScorePrintSequence.TodayRows);
    }

    private static void TickUntil(HighScorePrintSequence highScorePrintSequence, Func<HighScorePrintSequence, bool> condition)
    {
        for (var tick = 0; tick < 200 && !condition(highScorePrintSequence); tick++) highScorePrintSequence.Tick(false, Today, AllTime);

        Assert.True(condition(highScorePrintSequence), "the page never reached the expected state");
    }

    /// <summary>Ticks <paramref name="frames" /> fiftieths of a second' worth of printing.</summary>
    private static void TickFrames(HighScorePrintSequence highScorePrintSequence, int frames)
    {
        for (var tick = 0; tick < (frames * 6 + 4) / 5; tick++) highScorePrintSequence.Tick(false, Today, AllTime);
    }
}
