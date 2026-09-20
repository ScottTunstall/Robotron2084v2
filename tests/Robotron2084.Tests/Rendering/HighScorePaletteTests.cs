using Robotron2084.Rendering;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
/// The high score page's own colour processes — RRTABLE's <c>MAKP LOOPP</c> /
/// <c>MAKP DECAZ</c> / <c>MAKP COLA</c> / <c>MAKP COLC</c> / <c>MAKP COLD</c>
/// (notes §98.5). They are why the arcade's page is never still: the frame's slot
/// (8) walks COLTAB, which drags the headers' slot (7) along behind it, and the two
/// lists and their highlights ramp between dark and white on their own phases.
/// </summary>
public sealed class HighScorePaletteTests
{
    private static (GamePalette Palette, HighScorePalette Cycle) Started()
    {
        var palette = new GamePalette();
        var cycle = new HighScorePalette();
        cycle.Start(palette);
        return (palette, cycle);
    }

    [Fact]
    public void Start_BlacksThePaletteThenWritesEachProcessesCurrentByte()
    {
        (GamePalette palette, _) = Started();

        // FRAMER zeroes PCRAM..PCRAM+15 before anything is drawn (the page comes up
        // from black)...
        Assert.Equal(0x00, palette.SlotValue(0));
        Assert.Equal(0x00, palette.SlotValue(11));

        // ...then each process writes its table's current byte at once (LOOPP's shift
        // register pushes slot 8's old — zero — value down into slot 7).
        Assert.Equal(HighScorePalette.CycleTable[0], palette.SlotValue(8));
        Assert.Equal(0x00, palette.SlotValue(7));
        Assert.Equal(HighScorePalette.RampTable[7], palette.SlotValue(9));   // DECAZ starts at CATAB+7
        Assert.Equal(HighScorePalette.RampTable[0], palette.SlotValue(10));  // COLA starts at CATAB
        Assert.Equal(HighScorePalette.AccentTable[7], palette.SlotValue(12)); // COLC starts at CCTAB+7
        Assert.Equal(HighScorePalette.AccentTable[0], palette.SlotValue(13)); // COLD starts at CCTAB
    }

    [Fact]
    public void Start_TakesSlotsTenTwelveAndThirteenOffTheInGameAnimator()
    {
        (GamePalette palette, _) = Started();

        // The ROM kills the game's colour processes when the game ends; in the port the
        // in-game animator owns 10-15 until this suspends the three the page drives.
        foreach (int slot in HighScorePalette.OwnedSlots)
        {
            Assert.True(palette.IsSlotSuspended(slot), $"slot {slot}");
        }

        Assert.False(palette.IsSlotSuspended(8));
        Assert.False(palette.IsSlotSuspended(11));
        Assert.False(palette.IsSlotSuspended(14));
    }

    [Fact]
    public void TheWallCycle_StepsEveryThreeRomFramesAndWalksTheOldColourDownASlot()
    {
        (GamePalette palette, HighScorePalette cycle) = Started();

        // 3 ROM frames = 18 sixths = 3.6 port ticks, so the first step lands on tick 4.
        for (int tick = 0; tick < 3; tick++)
        {
            cycle.Update(palette);
        }

        Assert.Equal(HighScorePalette.CycleTable[0], palette.SlotValue(8));

        cycle.Update(palette);
        Assert.Equal(HighScorePalette.CycleTable[1], palette.SlotValue(8));
        Assert.Equal(HighScorePalette.CycleTable[0], palette.SlotValue(7));

        for (int tick = 0; tick < 4; tick++)
        {
            cycle.Update(palette);
        }

        Assert.Equal(HighScorePalette.CycleTable[2], palette.SlotValue(8));
        Assert.Equal(HighScorePalette.CycleTable[1], palette.SlotValue(7));
        Assert.Equal(HighScorePalette.CycleTable[0], palette.SlotValue(6));
    }

    [Fact]
    public void TheWallCycle_RunsTheWholeTableAndStartsItAgain()
    {
        (GamePalette palette, HighScorePalette cycle) = Started();

        // 21 entries at 3 ROM frames each = 63 ROM frames = 378 sixths = 75.6 port ticks,
        // so one lap later the process is back at the table's start.
        for (int tick = 0; tick < 76; tick++)
        {
            cycle.Update(palette);
        }

        Assert.Equal(HighScorePalette.CycleTable[0], palette.SlotValue(8));
    }

    [Fact]
    public void TheListRamps_StepEveryFourRomFramesAndWrapAtTheTablesTerminator()
    {
        (GamePalette palette, HighScorePalette cycle) = Started();

        Assert.Equal(0x07, palette.SlotValue(9));
        Assert.Equal(0x07, palette.SlotValue(10));

        // 4 ROM frames = 24 sixths = 4.8 ticks: the first step lands on tick 5.
        for (int tick = 0; tick < 5; tick++)
        {
            cycle.Update(palette);
        }

        Assert.Equal(0x57, palette.SlotValue(9)); // CATAB+8 — DECAZ is a step ahead
        Assert.Equal(0x07, palette.SlotValue(10)); // COLA's table opens with eight $07s

        // DECAZ's walk on from there: $A7 $FF $A7 $57 then the $00 terminator sends it
        // back to the START of CATAB, so its eight $07s come round again — the ramp never
        // lands on black.
        var changes = new List<byte>();
        byte last = (byte)palette.SlotValue(9);
        for (int tick = 0; tick < 400; tick++)
        {
            cycle.Update(palette);
            byte now = (byte)palette.SlotValue(9);
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
        (GamePalette palette, HighScorePalette cycle) = Started();

        for (int tick = 0; tick < 5; tick++)
        {
            cycle.Update(palette);
        }

        Assert.Equal(0xD2, palette.SlotValue(12)); // COLC was started at CCTAB+7 ($E4)
        Assert.Equal(0xFF, palette.SlotValue(13)); // COLD is still in CCTAB's seven $FFs
    }

    [Fact]
    public void Stop_GivesTheDefaultPaletteAndTheCyclingSlotsBack()
    {
        (GamePalette palette, HighScorePalette cycle) = Started();

        for (int tick = 0; tick < 40; tick++)
        {
            cycle.Update(palette);
        }

        cycle.Stop(palette);

        for (int slot = 0; slot <= 15; slot++)
        {
            Assert.Equal(GamePalette.DefaultSlots[slot], palette.SlotValue(slot));
            Assert.False(palette.IsSlotSuspended(slot), $"slot {slot}");
        }
    }
}
