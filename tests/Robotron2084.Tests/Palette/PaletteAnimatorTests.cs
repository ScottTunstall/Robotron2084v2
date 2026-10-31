using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
///     The six ROM colour processes (arcade-fidelity-notes §4.12, §65) replayed over
///     a <see cref="GamePalette" /> — exact tables, exact ARCADE FRAME timings, wrap
///     loops.
///     The timings are the ROM's frame counts converted through the clock-unit rule
///     of §52 (a fiftieth of a second is 6/5 of a port tick), so a 1-frame process steps every
///     1.2 ticks, a 2-frame one every 2.4 and an 8-frame one every 9.6. These tests
///     therefore pin where a step really lands (the 3rd, 5th, 8th … tick), which is
///     what distinguishes them from the old 20%-fast tick counts.
/// </summary>
public sealed class PaletteAnimatorTests
{
    private static (GamePalette GamePalette, PaletteAnimator PaletteAnimator) NewPair()
    {
        GamePalette gamePalette = new();
        // Seeded so the LF random-hue steps are deterministic.
        return (gamePalette, new PaletteAnimator(gamePalette, new Random(1234)));
    }

    private static void Tick(PaletteAnimator paletteAnimator, int ticks)
    {
        for (var i = 0; i < ticks; i++) paletteAnimator.Update();
    }

    [Fact]
    public void PaletteIndicesStartAtTheCrtabDefaults()
    {
        var (gamePalette, _) = NewPair();
        Assert.Equal(0x00, gamePalette.GetPaletteIndexValue(0));
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(1));
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(9));
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(10));
        Assert.Equal(0x17, gamePalette.GetPaletteIndexValue(11));
        Assert.Equal(0xCC, gamePalette.GetPaletteIndexValue(12));
        Assert.Equal(0x81, gamePalette.GetPaletteIndexValue(13));
        Assert.Equal(0x81, gamePalette.GetPaletteIndexValue(14));
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(15));
    }

    [Fact]
    public void RgbProcessStepsEveryEightArcadeFrames_WhichIs9Point6Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 9);
        Assert.Equal(0x17, gamePalette.GetPaletteIndexValue(11)); // not yet — the CRTAB default

        Tick(paletteAnimator, 1); // tick 10 = 50 clock units >= 48
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(11)); // table[0]

        Tick(paletteAnimator, 10); // tick 20
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(11)); // table[1]

        Tick(paletteAnimator, 9); // tick 29 (the carried 4 clock units catch up)
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(11)); // table[2]

        Tick(paletteAnimator, 10); // wraps back to the start of the table
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(11)); // table[0] again
    }

    [Fact]
    public void DecayProcessStepsEveryTwoArcadeFrames_WhichIs2Point4Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 3);
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(12)); // table[0]

        Tick(paletteAnimator, 2);
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(12)); // table[1]

        Tick(paletteAnimator, 3);
        Assert.Equal(0xD0, gamePalette.GetPaletteIndexValue(12)); // table[2]
    }

    [Fact]
    public void BluePurpleRedProcessStepsEveryArcadeFrame_WhichIs1Point2Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 1);
        Assert.Equal(0x81, gamePalette.GetPaletteIndexValue(14)); // 5 clock units: still the default

        Tick(paletteAnimator, 1); // tick 2 = 10 clock units >= 6
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(14)); // table[0]

        Tick(paletteAnimator, 4); // one step a tick from here (6 clock units per step)
        Assert.Equal(0xC4, gamePalette.GetPaletteIndexValue(14));
    }

    [Fact]
    public void RedGoldProcessStepsEverySixArcadeFrames_WhichIs7Point2Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        // Entries 0 and 1 are both 0x07 — the same value palette index 15 starts on — so
        // the step clock is only OBSERVABLE at the third entry (0x2F), which the
        // ROM's 6-frame period puts on tick 22 (steps at 8, 15 and 22).
        Tick(paletteAnimator, 21);
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(15));

        Tick(paletteAnimator, 1);
        Assert.Equal(0x2F, gamePalette.GetPaletteIndexValue(15)); // table[2]
    }

    [Fact]
    public void LaserProcessStepsEveryTwoArcadeFrames_WhichIs2Point4Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 3);
        Assert.Equal(PaletteAnimator.LaserTab[0], gamePalette.GetPaletteIndexValue(13));

        Tick(paletteAnimator, 2);
        Assert.Equal(PaletteAnimator.LaserTab[1], gamePalette.GetPaletteIndexValue(13));

        Tick(paletteAnimator, 3);
        Assert.Equal(PaletteAnimator.LaserTab[2], gamePalette.GetPaletteIndexValue(13));
    }

    [Fact]
    public void LaserFlashIsWhiteEverySecondArcadeFrameAndRandomEverySixthArcadeFrame()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 2);
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(10)); // 5 and 10 clock units: no write yet

        Tick(paletteAnimator, 1); // tick 3 = 15 clock units: the first (white) flash
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10));

        Tick(paletteAnimator, 2); // tick 5: white again — the flash period is 2 fiftieths of a second
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10));

        Tick(paletteAnimator, 3); // tick 8: every 3rd flash is a random COLTAB hue
        Assert.Contains((byte)gamePalette.GetPaletteIndexValue(10), PaletteAnimator.LaserTab);

        Tick(paletteAnimator, 2); // tick 10: back to white
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10));
    }

    [Fact]
    public void ASuspendedPaletteIndex_IsNotWrittenByItsProcess()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        // The ROM's KILL OFF DECAY (notes §66): the player death stops palette index 12's
        // DECAY process and then drives palette index 12 itself, byte by byte.
        gamePalette.SuspendPaletteIndex(12);
        Tick(paletteAnimator, 40);
        Assert.Equal(0xCC, gamePalette.GetPaletteIndexValue(12)); // still the CRTAB default

        gamePalette.SetPaletteIndexValue(12, 0x55); // a fade byte, written directly
        Tick(paletteAnimator, 40);
        Assert.Equal(0x55, gamePalette.GetPaletteIndexValue(12)); // the process never overwrote it

        // COLST: the process comes back, and restarts at its table's first entry.
        gamePalette.ResumePaletteIndex(12);
        Tick(paletteAnimator, 3);
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(12));
    }
}
