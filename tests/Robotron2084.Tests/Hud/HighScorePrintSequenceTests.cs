using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// RRTABLE's printing order (notes §98.6) — the page BUILDS ITSELF: <c>MAKP LOOPP</c>
/// and <c>JSR FRAMER</c> put the wall on an empty screen, then <c>PRJNK</c> prints the
/// lists FOUR ROWS A ROM FRAME (<c>LDA #4 / STA PD+17,U</c> + <c>NAP 1</c>), with the top
/// entry between them and the headers last, and only then do the ramp processes start.
/// </summary>
public sealed class HighScorePrintSequenceTests
{
    private const int Today = 10;
    private const int AllTime = 36;

    [Fact]
    public void NothingIsPrinted_UntilTheFramesTwoPassesHaveFinished()
    {
        var print = new HighScorePrintSequence();

        for (int tick = 0; tick < 200; tick++)
        {
            print.Tick(frameFinished: false, Today, AllTime);
        }

        Assert.False(print.IsPrinting);
        Assert.Equal(0, print.TodayRows);
        Assert.Equal(0, print.AllTimeRows);
        Assert.False(print.TopPrinted);
        Assert.False(print.HeadersPrinted);
        Assert.False(print.IsDone);
    }

    [Fact]
    public void TheFirstGroupPrintsWithTheFramesLastStroke()
    {
        var print = new HighScorePrintSequence();

        print.Tick(frameFinished: true, Today, AllTime);

        // PRJNK's first group prints the moment it is called (TOD44 falls into TOD33).
        Assert.Equal(4, print.TodayRows);
        Assert.True(print.IsPrinting);
    }

    [Fact]
    public void FourRowsPrintPerRomFrame_AndThePageEndsWithTheHeaders()
    {
        var print = new HighScorePrintSequence();
        print.Tick(frameFinished: true, Today, AllTime);
        Assert.Equal(4, print.TodayRows);

        // A ROM frame is 6/5 of a port tick.
        print.Tick(false, Today, AllTime);
        Assert.Equal(4, print.TodayRows); // 5 sixths — not a frame yet

        print.Tick(false, Today, AllTime);
        Assert.Equal(8, print.TodayRows); // 10 sixths — the second group

        // Today's list runs out on its third group, and the top entry plus the FIRST
        // all-time group print straight after it, with no sleep in between: TOD22 leaves
        // PRJNK without sleeping, and TABLE's NOINTS starts the second list at once.
        TickUntil(print, p => p.TodayRows == Today);
        Assert.True(print.TopPrinted);
        Assert.Equal(4, print.AllTimeRows);

        TickUntil(print, p => p.HeadersPrinted);
        Assert.Equal(AllTime, print.AllTimeRows);
        Assert.True(print.IsDone);

        // The page is a still picture once printed.
        TickFrames(print, 10);
        Assert.Equal(AllTime, print.AllTimeRows);
        Assert.True(print.IsDone);
    }

    [Fact]
    public void TheWholePagePrintsInAboutTenRomFrames()
    {
        // 10 rows = 3 groups (2 sleeps) and 36 rows = 9 groups (8 sleeps): ten ROM frames
        // = twelve ticks, a fifth of a second of printing after the frame's own 53.
        var print = new HighScorePrintSequence();
        int ticks = 0;
        while (!print.IsDone && ticks < 200)
        {
            print.Tick(frameFinished: true, Today, AllTime);
            ticks++;
        }

        Assert.True(print.IsDone);
        Assert.InRange(ticks, 11, 14);
    }

    [Fact]
    public void AnEmptyPageFinishesAsSoonAsItStarts()
    {
        var print = new HighScorePrintSequence();

        print.Tick(frameFinished: true, 0, 0);

        Assert.True(print.HeadersPrinted);
        Assert.True(print.IsDone);
        Assert.Equal(0, print.TodayRows);
    }

    private static void TickUntil(HighScorePrintSequence print, Func<HighScorePrintSequence, bool> condition)
    {
        for (int tick = 0; tick < 200 && !condition(print); tick++)
        {
            print.Tick(false, Today, AllTime);
        }

        Assert.True(condition(print), "the page never reached the expected state");
    }

    /// <summary>Ticks <paramref name="frames"/> ROM frames' worth of printing.</summary>
    private static void TickFrames(HighScorePrintSequence print, int frames)
    {
        for (int tick = 0; tick < ((frames * 6) + 4) / 5; tick++)
        {
            print.Tick(false, Today, AllTime);
        }
    }
}
