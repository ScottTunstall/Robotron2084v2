using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
/// The six ROM colour processes (arcade-fidelity-notes §4.12, §65) replayed over
/// a <see cref="GamePalette"/> — exact tables, exact ROM-FRAME timings, wrap
/// loops.
///
/// The timings are the ROM's frame counts converted through the clock-unit rule
/// of §52 (a fiftieth of a second is 6/5 of a port tick), so a 1-frame process steps every
/// 1.2 ticks, a 2-frame one every 2.4 and an 8-frame one every 9.6. These tests
/// therefore pin where a step really lands (the 3rd, 5th, 8th … tick), which is
/// what distinguishes them from the old 20%-fast tick counts.
/// </summary>
public sealed class PaletteAnimatorTests
{
    private static (GamePalette Palette, PaletteAnimator Animator) NewPair()
    {
        GamePalette palette = new();
        // Seeded so the LF random-hue steps are deterministic.
        return (palette, new PaletteAnimator(palette, new Random(1234)));
    }

    private static void Tick(PaletteAnimator animator, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            animator.Update();
        }
    }

    [Fact]
    public void SlotsStartAtTheCrtabDefaults()
    {
        (GamePalette palette, _) = NewPair();
        Assert.Equal(0x00, palette.GetSlotValue(0));
        Assert.Equal(0x07, palette.GetSlotValue(1));
        Assert.Equal(0xFF, palette.GetSlotValue(9));
        Assert.Equal(0x38, palette.GetSlotValue(10));
        Assert.Equal(0x17, palette.GetSlotValue(11));
        Assert.Equal(0xCC, palette.GetSlotValue(12));
        Assert.Equal(0x81, palette.GetSlotValue(13));
        Assert.Equal(0x81, palette.GetSlotValue(14));
        Assert.Equal(0x07, palette.GetSlotValue(15));
    }

    [Fact]
    public void RgbProcessStepsEveryEightRomFrames_WhichIs9Point6Ticks()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        Tick(animator, 9);
        Assert.Equal(0x17, palette.GetSlotValue(11)); // not yet — the CRTAB default

        Tick(animator, 1); // tick 10 = 50 clock units >= 48
        Assert.Equal(0x38, palette.GetSlotValue(11)); // table[0]

        Tick(animator, 10); // tick 20
        Assert.Equal(0x07, palette.GetSlotValue(11)); // table[1]

        Tick(animator, 9); // tick 29 (the carried 4 clock units catch up)
        Assert.Equal(0xC0, palette.GetSlotValue(11)); // table[2]

        Tick(animator, 10); // wraps back to the start of the table
        Assert.Equal(0x38, palette.GetSlotValue(11)); // table[0] again
    }

    [Fact]
    public void DecayProcessStepsEveryTwoRomFrames_WhichIs2Point4Ticks()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        Tick(animator, 3);
        Assert.Equal(0xC0, palette.GetSlotValue(12)); // table[0]

        Tick(animator, 2);
        Assert.Equal(0xC0, palette.GetSlotValue(12)); // table[1]

        Tick(animator, 3);
        Assert.Equal(0xD0, palette.GetSlotValue(12)); // table[2]
    }

    [Fact]
    public void BluePurpleRedProcessStepsEveryRomFrame_WhichIs1Point2Ticks()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        Tick(animator, 1);
        Assert.Equal(0x81, palette.GetSlotValue(14)); // 5 clock units: still the default

        Tick(animator, 1); // tick 2 = 10 clock units >= 6
        Assert.Equal(0xC0, palette.GetSlotValue(14)); // table[0]

        Tick(animator, 4); // one step a tick from here (6 clock units per step)
        Assert.Equal(0xC4, palette.GetSlotValue(14));
    }

    [Fact]
    public void RedGoldProcessStepsEverySixRomFrames_WhichIs7Point2Ticks()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        // Entries 0 and 1 are both 0x07 — the same value slot 15 starts on — so
        // the step clock is only OBSERVABLE at the third entry (0x2F), which the
        // ROM's 6-frame period puts on tick 22 (steps at 8, 15 and 22).
        Tick(animator, 21);
        Assert.Equal(0x07, palette.GetSlotValue(15));

        Tick(animator, 1);
        Assert.Equal(0x2F, palette.GetSlotValue(15)); // table[2]
    }

    [Fact]
    public void LaserProcessStepsEveryTwoRomFrames_WhichIs2Point4Ticks()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        Tick(animator, 3);
        Assert.Equal(PaletteAnimator.LaserTab[0], palette.GetSlotValue(13));

        Tick(animator, 2);
        Assert.Equal(PaletteAnimator.LaserTab[1], palette.GetSlotValue(13));

        Tick(animator, 3);
        Assert.Equal(PaletteAnimator.LaserTab[2], palette.GetSlotValue(13));
    }

    [Fact]
    public void LaserFlashIsWhiteEverySecondRomFrameAndRandomEverySixthRomFrame()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        Tick(animator, 2);
        Assert.Equal(0x38, palette.GetSlotValue(10)); // 5 and 10 clock units: no write yet

        Tick(animator, 1); // tick 3 = 15 clock units: the first (white) flash
        Assert.Equal(0xFF, palette.GetSlotValue(10));

        Tick(animator, 2); // tick 5: white again — the flash period is 2 fiftieths of a second
        Assert.Equal(0xFF, palette.GetSlotValue(10));

        Tick(animator, 3); // tick 8: every 3rd flash is a random COLTAB hue
        Assert.Contains((byte)palette.GetSlotValue(10), PaletteAnimator.LaserTab);

        Tick(animator, 2); // tick 10: back to white
        Assert.Equal(0xFF, palette.GetSlotValue(10));
    }

    [Fact]
    public void ASuspendedSlot_IsNotWrittenByItsProcess()
    {
        (GamePalette palette, PaletteAnimator animator) = NewPair();

        // The ROM's KILL OFF DECAY (notes §66): the player death stops slot 12's
        // DECAY process and then drives slot 12 itself, byte by byte.
        palette.SuspendSlot(12);
        Tick(animator, 40);
        Assert.Equal(0xCC, palette.GetSlotValue(12)); // still the CRTAB default

        palette.SetSlot(12, 0x55); // a fade byte, written directly
        Tick(animator, 40);
        Assert.Equal(0x55, palette.GetSlotValue(12)); // the process never overwrote it

        // COLST: the process comes back, and restarts at its table's first entry.
        palette.ResumeSlot(12);
        Tick(animator, 3);
        Assert.Equal(0xC0, palette.GetSlotValue(12));
    }
}
