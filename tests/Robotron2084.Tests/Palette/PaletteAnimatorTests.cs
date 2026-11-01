using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Palette;

/// <summary>
///     The six ROM colour processes (arcade-fidelity-notes §4.12, §65) replayed over a <see cref="GamePalette" />: exact
///     tables, exact ARCADE FRAME timings, wrap loops.
///     As the ROM's <c>TABDRI</c> does, each table-driven process writes its first colour straight away and then waits
///     one cycle for each next one. The timings are the ROM's frame counts converted through the clock-unit rule of §52
///     (a fiftieth of a second is 6/5 of a port tick, so 5 clock units a tick and 6 a frame). These tests pin the tick
///     each colour really lands on.
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
    public void RgbProcessWritesAtOnceThenCyclesEveryEightArcadeFrames_WhichIs9Point6Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 1);
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(11)); // table[0], straight away (the CRTAB default was 0x17)

        Tick(paletteAnimator, 8); // tick 9: 45 clock units, less than 48
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(11));

        Tick(paletteAnimator, 1); // tick 10: 50 clock units >= 48
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(11)); // table[1]

        Tick(paletteAnimator, 10); // tick 20: 100 clock units >= 96
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(11)); // table[2]

        Tick(paletteAnimator, 9); // tick 29: 145 clock units >= 144, wrapping back to the start of the table
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(11)); // table[0] again
    }

    [Fact]
    public void DecayProcessWritesAtOnceThenCyclesEveryTwoArcadeFrames_WhichIs2Point4Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 1);
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(12)); // table[0], straight away (the CRTAB default was 0xCC)

        Tick(paletteAnimator, 3); // tick 4: table[1] landed on tick 3, and it is the same colour
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(12));

        Tick(paletteAnimator, 1); // tick 5: 25 clock units >= 24
        Assert.Equal(0xD0, gamePalette.GetPaletteIndexValue(12)); // table[2]

        Tick(paletteAnimator, 2); // tick 7: 35 clock units, less than 36
        Assert.Equal(0xD0, gamePalette.GetPaletteIndexValue(12));

        Tick(paletteAnimator, 1); // tick 8: 40 clock units >= 36
        Assert.Equal(0xE0, gamePalette.GetPaletteIndexValue(12)); // table[3]
    }

    [Fact]
    public void BluePurpleRedProcessWritesAtOnceThenCyclesEveryArcadeFrame_WhichIs1Point2Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 1);
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(14)); // table[0], straight away (the CRTAB default was 0x81)

        Tick(paletteAnimator, 1); // tick 2: 10 clock units >= 6
        Assert.Equal(0xC1, gamePalette.GetPaletteIndexValue(14)); // table[1]

        Tick(paletteAnimator, 4); // tick 6: 30 clock units >= 30
        Assert.Equal(0xC5, gamePalette.GetPaletteIndexValue(14)); // table[5]

        Tick(paletteAnimator, 1); // tick 7: 35 clock units, less than 36
        Assert.Equal(0xC5, gamePalette.GetPaletteIndexValue(14));

        Tick(paletteAnimator, 1); // tick 8: 40 clock units >= 36
        Assert.Equal(0xC6, gamePalette.GetPaletteIndexValue(14)); // table[6]
    }

    [Fact]
    public void RedGoldProcessWritesAtOnceThenCyclesEverySixArcadeFrames_WhichIs7Point2Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        // Entries 0 and 1 are both 0x07, the same value palette index 15 starts on, so the cycle clock is only OBSERVABLE
        // at the third entry (0x2F). Its cycles land on ticks 1, 8, 15 and 22.
        Tick(paletteAnimator, 14);
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(15));

        Tick(paletteAnimator, 1); // tick 15: 75 clock units >= 72
        Assert.Equal(0x2F, gamePalette.GetPaletteIndexValue(15)); // table[2]

        Tick(paletteAnimator, 6); // tick 21: 105 clock units, less than 108
        Assert.Equal(0x2F, gamePalette.GetPaletteIndexValue(15));

        Tick(paletteAnimator, 1); // tick 22: 110 clock units >= 108, wrapping back to the start of the table
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(15)); // table[0] again
    }

    [Fact]
    public void LaserProcessWritesAtOnceThenCyclesEveryTwoArcadeFrames_WhichIs2Point4Ticks()
    {
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 1);
        Assert.Equal(PaletteAnimator.LaserTab[0], gamePalette.GetPaletteIndexValue(13)); // straight away

        Tick(paletteAnimator, 2); // tick 3: 15 clock units >= 12
        Assert.Equal(PaletteAnimator.LaserTab[1], gamePalette.GetPaletteIndexValue(13));

        Tick(paletteAnimator, 2); // tick 5: 25 clock units >= 24
        Assert.Equal(PaletteAnimator.LaserTab[2], gamePalette.GetPaletteIndexValue(13));
    }

    [Fact]
    public void LaserFlash_IsWhiteForTwoArcadeFramesThenRandomForSix_AndRepeatsEveryEight()
    {
        // ROM RRS22 LF: LDA #$FF / STA PCRAM+$A / NAP 2,LF1, then a random COLTAB colour / NAP 6,LF. Each tick is 5 clock
        // units and each arcade frame is 6, so the changes land on ticks 1 (white), 3 (random), 10 (white), 12 (random)
        // and 20 (white).
        var (gamePalette, paletteAnimator) = NewPair();

        Tick(paletteAnimator, 1);
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10)); // white straight away, as the ROM does at its start

        Tick(paletteAnimator, 1);
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10)); // tick 2: still white

        Tick(paletteAnimator, 1); // tick 3: two arcade frames after the start
        var randomColour = gamePalette.GetPaletteIndexValue(10);
        Assert.Contains((byte)randomColour, PaletteAnimator.LaserTab);

        for (var tick = 4; tick <= 9; tick++)
        {
            Tick(paletteAnimator, 1);
            Assert.Equal(randomColour, gamePalette.GetPaletteIndexValue(10)); // the random colour holds for six arcade frames
        }

        Tick(paletteAnimator, 1); // tick 10: eight arcade frames after the start
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10));

        Tick(paletteAnimator, 1);
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10)); // tick 11: still white

        Tick(paletteAnimator, 1); // tick 12: ten arcade frames after the start
        Assert.Contains((byte)gamePalette.GetPaletteIndexValue(10), PaletteAnimator.LaserTab);
        Assert.NotEqual(0xFF, gamePalette.GetPaletteIndexValue(10));

        Tick(paletteAnimator, 8); // tick 20: sixteen arcade frames after the start
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(10));
    }

    [Fact]
    public void LaserFlash_PicksItsRandomColourFromTheFirstThirtyTwoEntriesOfTheTable()
    {
        // ROM RRS22 LF1: LDA SEED / ANDA #$1F / LDA A,X, so the place is 0 to 31 and the last four entries are never used.
        GamePalette gamePalette = new();
        MaxRecordingRandom random = new();
        PaletteAnimator paletteAnimator = new(gamePalette, random);

        Tick(paletteAnimator, 3); // the first random colour is picked on tick 3

        Assert.Equal(32, random.LargestMaxValue);
    }

    [Fact]
    public void DecayProcessNeverShowsTheTableEndMarkerAsAColour()
    {
        // ROM RRS22 TABDRI: a $00 ends the table and sends the process back to its first colour at once, so $00 is never
        // written to the palette.
        var (gamePalette, paletteAnimator) = NewPair();

        for (var tick = 1; tick <= 400; tick++)
        {
            Tick(paletteAnimator, 1);
            Assert.NotEqual(0x00, gamePalette.GetPaletteIndexValue(12));
        }
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

        // COLST: the process comes back, and writes its table's first entry at once.
        gamePalette.ResumePaletteIndex(12);
        Tick(paletteAnimator, 1);
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(12));
    }

    private sealed class MaxRecordingRandom : Random
    {
        public int LargestMaxValue { get; private set; }

        public override int Next(int maxValue)
        {
            LargestMaxValue = Math.Max(LargestMaxValue, maxValue);
            return 0;
        }
    }
}
