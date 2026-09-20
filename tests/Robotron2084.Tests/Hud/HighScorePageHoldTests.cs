using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// RRTABLE's <c>TABLE</c> hold and exit (notes §98.8). The ROM plays the page for its
/// full 600 frames with NO switch check at all (<c>TAB888</c>), and only then leaves —
/// and it leaves when the switches are CLEAR (<c>TAB999</c>'s <c>BEQ</c>), so a held
/// switch DELAYS the exit rather than shortening the page. The switch bits are active
/// high, which the movement table at $3031 proves.
/// </summary>
public sealed class HighScorePageHoldTests
{
    [Fact]
    public void NothingShortensTheHold_NotEvenASwitchHeldDownThroughIt()
    {
        var hold = new HighScorePageHold();

        // 600 ROM frames = 720 port ticks.
        for (int tick = 0; tick < 719; tick++)
        {
            Assert.False(hold.Tick(true), $"the page left during the hold (tick {tick})");
            Assert.False(hold.HoldIsOver);
        }

        Assert.False(hold.Tick(true));
        Assert.True(hold.HoldIsOver); // the 600 frames ran out on this tick
    }

    [Fact]
    public void WithNothingHeld_ThePageLeavesOneNapFourAfterTheHold()
    {
        var hold = new HighScorePageHold();
        for (int tick = 0; tick < 720; tick++)
        {
            hold.Tick(false);
        }

        // TAB777's NAP 4 — 4 ROM frames ≈ 4.8 ticks — is the first switch read.
        int ticks = 0;
        while (!hold.Tick(false))
        {
            ticks++;
            Assert.True(ticks < 8, "the page never left an idle cabinet");
        }

        Assert.InRange(ticks, 3, 5);
        Assert.Equal(0, hold.ChecksWithSwitchDown);
    }

    [Fact]
    public void AHeldSwitchDelaysTheExit_ForTheRoms255Checks()
    {
        var hold = new HighScorePageHold();
        for (int tick = 0; tick < 720; tick++)
        {
            hold.Tick(true);
        }

        int ticks = 0;
        while (!hold.Tick(true))
        {
            ticks++;
            Assert.True(ticks < 2000, "a held switch never let the page go");
        }

        // 255 checks of four frames each ≈ 1224 ticks, plus the hold.
        Assert.InRange(ticks, 1200, 1250);
        Assert.Equal(255, hold.ChecksWithSwitchDown);
    }

    [Fact]
    public void ReleasingTheSwitchesLeavesAtTheNextCheck()
    {
        var hold = new HighScorePageHold();
        for (int tick = 0; tick < 720; tick++)
        {
            hold.Tick(false);
        }

        // Hold a switch through five checks, then let go.
        while (hold.ChecksWithSwitchDown < 5)
        {
            Assert.False(hold.Tick(true));
        }

        int ticks = 0;
        while (!hold.Tick(false))
        {
            ticks++;
            Assert.True(ticks < 8, "the page did not leave after the switches cleared");
        }

        Assert.Equal(5, hold.ChecksWithSwitchDown);
    }
}
