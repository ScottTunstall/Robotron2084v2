using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// RRTABLE's <c>FRAMER</c> (notes §98.5) — the high score page's frame is DRAWN: two
/// strokes a ROM frame grow it from (col 62, row 125)-(col 89, row 127) out to the
/// terminal point $060D = (col 6, row 13), and then the same walk runs again in flavour
/// 0 (black) up to $0E1D = (col 14, row 29), leaving the hatched band between them.
/// Both passes draw their first two strokes before the first sleep.
/// </summary>
public sealed class HighScoreFrameAnimationTests
{
    [Fact]
    public void AtEntry_TwoStrokesAreAlreadyDrawn()
    {
        var frame = new HighScoreFrameAnimation();

        Assert.Equal(1, frame.DrawnStroke);
        Assert.Equal(-1, frame.ErasedStroke);
        Assert.Equal(HighScoreTableLayout.FrameStroke(1), HighScoreTableLayout.FrameStroke(frame.DrawnStroke));
        Assert.False(frame.IsFinished);
    }

    [Fact]
    public void TwoStrokesAreDrawnPerRomFrame()
    {
        var frame = new HighScoreFrameAnimation();

        // A ROM frame is 6/5 of a port tick, so the first pair lands on tick 2 — and
        // never more than two strokes a tick, which is what the ROM's `LDA #2` counter
        // and its one-frame `NAP` add up to.
        frame.Tick();
        Assert.Equal(1, frame.DrawnStroke); // 5 sixths — not a frame yet

        frame.Tick();
        Assert.Equal(3, frame.DrawnStroke); // two strokes

        frame.Tick();
        frame.Tick();
        frame.Tick();
        Assert.Equal(9, frame.DrawnStroke); // six strokes
    }

    [Fact]
    public void TheExpandAndErasePasses_TakeAboutASecond()
    {
        // 57 growing strokes + 49 erase strokes at two a ROM frame = 53 ROM frames
        // = 1.06 s — which at 6/5 of a tick each is about 64 port ticks.
        var frame = new HighScoreFrameAnimation();

        int ticks = 0;
        while (!frame.IsFinished)
        {
            frame.Tick();
            ticks++;
            Assert.True(ticks < 500, "the frame never finished");
        }

        Assert.InRange(ticks, 58, 72);
    }

    [Fact]
    public void TheErasePass_StopsLeavingTheEightStrokeBandAndStaysThere()
    {
        var frame = new HighScoreFrameAnimation();
        while (!frame.IsFinished)
        {
            frame.Tick();
        }

        Assert.Equal(HighScoreTableLayout.FrameLastStroke, frame.DrawnStroke);
        Assert.Equal(HighScoreTableLayout.FrameEraseLastStroke, frame.ErasedStroke);

        for (int tick = 0; tick < 100; tick++)
        {
            frame.Tick();
        }

        Assert.Equal(HighScoreTableLayout.FrameLastStroke, frame.DrawnStroke);
        Assert.Equal(HighScoreTableLayout.FrameEraseLastStroke, frame.ErasedStroke);
    }

    [Fact]
    public void TheVisibleBand_IsEightStrokesOfEightDifferentSlots()
    {
        // MARQ's flavour walks down by $11 a stroke, so the eight strokes the erase pass
        // leaves behind are slots 8…1 — the eight simultaneously cycling colours the
        // author sees on the cabinet.
        var frame = new HighScoreFrameAnimation();
        while (!frame.IsFinished)
        {
            frame.Tick();
        }

        var slots = new int[frame.DrawnStroke - frame.ErasedStroke];
        for (int stroke = frame.ErasedStroke + 1; stroke <= frame.DrawnStroke; stroke++)
        {
            slots[stroke - frame.ErasedStroke - 1] = HighScoreTableLayout.FrameStrokeSlot(stroke);
        }

        Assert.Equal(new[] { 8, 7, 6, 5, 4, 3, 2, 1 }, slots);

        // …and the walk repeats, so the whole page's outer edge came back to $11's slot.
        Assert.Equal(1, HighScoreTableLayout.FrameStrokeSlot(HighScoreTableLayout.FrameLastStroke));
        Assert.Equal(8, HighScoreTableLayout.FrameStrokeSlot(HighScoreTableLayout.FrameLastStroke - 7));
    }
}
