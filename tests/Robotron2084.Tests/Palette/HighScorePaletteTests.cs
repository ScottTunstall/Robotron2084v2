using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
///     The high score page's own colour processes — RRTABLE's <c>MAKP LOOPP</c> /
///     <c>MAKP DECAZ</c> / <c>MAKP COLA</c> / <c>MAKP COLC</c> / <c>MAKP COLD</c>
///     (notes §98.5). They are why the arcade's page is never still: the frame's slot
///     (8) walks COLTAB, which drags the headers' slot (7) along behind it, and the two
///     lists and their highlights ramp between dark and white on their own phases.
/// </summary>
public sealed class HighScorePaletteTests
{
    private static (GamePalette Palette, HighScorePalette Cycle) Started()
    {
        var palette = new GamePalette();
        var cycle = new HighScorePalette();
        cycle.Start(palette);
        cycle.StartRamps(palette);
        return (palette, cycle);
    }

    /// <summary>Started but with the four ramps still to come (the ROM's page order).</summary>
    private static (GamePalette Palette, HighScorePalette Cycle) FrameOnly()
    {
        var palette = new GamePalette();
        var cycle = new HighScorePalette();
        cycle.Start(palette);
        return (palette, cycle);
    }

    [Fact]
    public void Start_BlacksThePaletteAndStartsOnlyTheWallsCycle()
    {
        var (palette, _) = FrameOnly();

        // FRAMER zeroes PCRAM..PCRAM+15 before anything is drawn (the page comes up
        // from black)...
        Assert.Equal(0x00, palette.GetSlotValue(0));
        Assert.Equal(0x00, palette.GetSlotValue(11));

        // ...then MAKP LOOPP's first store shifts slot 8's old — zero — value into slot 7.
        Assert.Equal(HighScorePalette.CycleTable[0], palette.GetSlotValue(8));
        Assert.Equal(0x00, palette.GetSlotValue(7));

        // The four ramps are NOT running yet: the ROM starts them after the page has
        // printed (MAKP DECAZ/COLA/COLC/COLD follow WRD7V), so the printed rows sit in
        // black slots until then.
        Assert.Equal(0x00, palette.GetSlotValue(9));
        Assert.Equal(0x00, palette.GetSlotValue(10));
        Assert.Equal(0x00, palette.GetSlotValue(12));
        Assert.Equal(0x00, palette.GetSlotValue(13));
    }

    [Fact]
    public void StartRamps_BringsTheFourRampsUpInTheirOwnPhases()
    {
        var (palette, cycle) = Started();

        cycle.StartRamps(palette);

        Assert.True(cycle.RampsStarted);
        Assert.Equal(HighScorePalette.RampTable[7], palette.GetSlotValue(9)); // DECAZ starts at CATAB+7
        Assert.Equal(HighScorePalette.RampTable[0], palette.GetSlotValue(10)); // COLA starts at CATAB
        Assert.Equal(HighScorePalette.AccentTable[7], palette.GetSlotValue(12)); // COLC starts at CCTAB+7
        Assert.Equal(HighScorePalette.AccentTable[0], palette.GetSlotValue(13)); // COLD starts at CCTAB

        // Starting them twice is a no-op (the ROM's MAKPs happen once).
        for (var tick = 0; tick < 5; tick++) cycle.Update(palette);

        var stepped = (byte)palette.GetSlotValue(9);
        cycle.StartRamps(palette);
        Assert.Equal(stepped, palette.GetSlotValue(9));
    }

    [Fact]
    public void TheRampsDoNotRunBeforeTheyAreStarted()
    {
        var (palette, cycle) = FrameOnly();

        for (var tick = 0; tick < 100; tick++) cycle.Update(palette);

        Assert.Equal(0x00, palette.GetSlotValue(9));
        Assert.Equal(0x00, palette.GetSlotValue(10));
        Assert.Equal(0x00, palette.GetSlotValue(12));
        Assert.Equal(0x00, palette.GetSlotValue(13));

        // ...the wall's cycle runs regardless (it comes up with the frame).
        Assert.NotEqual(0x00, palette.GetSlotValue(8));
    }

    [Fact]
    public void Start_TakesSlotsTenTwelveAndThirteenOffTheInGameAnimator()
    {
        var (palette, _) = Started();

        // The ROM kills the game's colour processes when the game ends; in the port the
        // in-game animator owns 10-15 until this suspends the three the page drives.
        foreach (var slot in HighScorePalette.OwnedSlots) Assert.True(palette.IsSlotSuspended(slot), $"slot {slot}");

        Assert.False(palette.IsSlotSuspended(8));
        Assert.False(palette.IsSlotSuspended(11));
        Assert.False(palette.IsSlotSuspended(14));
    }

    [Fact]
    public void TheWallCycle_StepsEveryThreeRomFramesAndWalksTheOldColourDownASlot()
    {
        var (palette, cycle) = Started();

        // 3 fiftieths of a second = 18 clock units = 3.6 port ticks, so the first step lands on tick 4.
        for (var tick = 0; tick < 3; tick++) cycle.Update(palette);

        Assert.Equal(HighScorePalette.CycleTable[0], palette.GetSlotValue(8));

        cycle.Update(palette);
        Assert.Equal(HighScorePalette.CycleTable[1], palette.GetSlotValue(8));
        Assert.Equal(HighScorePalette.CycleTable[0], palette.GetSlotValue(7));

        for (var tick = 0; tick < 4; tick++) cycle.Update(palette);

        Assert.Equal(HighScorePalette.CycleTable[2], palette.GetSlotValue(8));
        Assert.Equal(HighScorePalette.CycleTable[1], palette.GetSlotValue(7));
        Assert.Equal(HighScorePalette.CycleTable[0], palette.GetSlotValue(6));
    }

    [Fact]
    public void TheWallCycle_RunsTheWholeTableAndStartsItAgain()
    {
        var (palette, cycle) = Started();

        // 21 entries at 3 fiftieths of a second each = 63 fiftieths of a second = 378 clock units = 75.6 port ticks,
        // so one lap later the process is back at the table's start.
        for (var tick = 0; tick < 76; tick++) cycle.Update(palette);

        Assert.Equal(HighScorePalette.CycleTable[0], palette.GetSlotValue(8));
    }

    [Fact]
    public void TheListRamps_StepEveryFourRomFramesAndWrapAtTheTablesTerminator()
    {
        var (palette, cycle) = Started();

        Assert.Equal(0x07, palette.GetSlotValue(9));
        Assert.Equal(0x07, palette.GetSlotValue(10));

        // 4 fiftieths of a second = 24 clock units = 4.8 ticks: the first step lands on tick 5.
        for (var tick = 0; tick < 5; tick++) cycle.Update(palette);

        Assert.Equal(0x57, palette.GetSlotValue(9)); // CATAB+8 — DECAZ is a step ahead
        Assert.Equal(0x07, palette.GetSlotValue(10)); // COLA's table opens with eight $07s

        // DECAZ's walk on from there: $A7 $FF $A7 $57 then the $00 terminator sends it
        // back to the START of CATAB, so its eight $07s come round again — the ramp never
        // lands on black.
        var changes = new List<byte>();
        var last = (byte)palette.GetSlotValue(9);
        for (var tick = 0; tick < 400; tick++)
        {
            cycle.Update(palette);
            var now = (byte)palette.GetSlotValue(9);
            if (now != last)
            {
                changes.Add(now);
                last = now;
            }
        }

        Assert.Equal(
            new byte[] { 0xA7, 0xFF, 0xA7, 0x57, 0x07, 0x57, 0xA7, 0xFF, 0xA7, 0x57, 0x07, 0x57 },
            changes.Take(12));
    }

    [Fact]
    public void TheHighlightRamps_MatchTheirOwnTableAndPhase()
    {
        var (palette, cycle) = Started();

        for (var tick = 0; tick < 5; tick++) cycle.Update(palette);

        Assert.Equal(0xD2, palette.GetSlotValue(12)); // COLC was started at CCTAB+7 ($E4)
        Assert.Equal(0xFF, palette.GetSlotValue(13)); // COLD is still in CCTAB's seven $FFs
    }

    [Fact]
    public void Stop_GivesTheDefaultPaletteAndTheCyclingSlotsBack()
    {
        var (palette, cycle) = Started();

        for (var tick = 0; tick < 40; tick++) cycle.Update(palette);

        cycle.Stop(palette);

        for (var slot = 0; slot <= 15; slot++)
        {
            Assert.Equal(GamePalette.DefaultSlotValues[slot], palette.GetSlotValue(slot));
            Assert.False(palette.IsSlotSuspended(slot), $"slot {slot}");
        }
    }
}
