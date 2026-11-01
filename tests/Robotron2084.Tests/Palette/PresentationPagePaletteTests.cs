using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Palette;

/// <summary>
///     The Williams presentation page's own colour set (notes §106): the seven entries of <c>LCTAB</c> (`$8A70` in the
///     disassembly) written into palette indices 1-7 by <c>CSET</c> (`$8A3A`), the WHITE flash <c>COL1</c>/<c>COLL</c>
///     (`$8A4F`/`$8A68`) runs through them every three fiftieths of a second, and the border logos' colour cycle
///     (<c>FORMX</c>'s <c>SUBA #$11</c>, `$89DA`: `$77 → $66 → … → $11 → $77`) that the port's traced wordmark borrows.
/// </summary>
public sealed class PresentationPagePaletteTests
{
    /// <summary>A fiftieth of a second is 6/5 of a port tick (notes §52): n frames land on ceil(n × 6/5).</summary>
    private static int Ticks(int arcadeFrames)
    {
        return (int)Math.Ceiling(arcadeFrames * 6.0 / 5.0);
    }

    private static (GamePalette GamePalette, PresentationPagePalette PresentationPagePalette) Started()
    {
        var gamePalette = new GamePalette();
        var presentationPagePalette = new PresentationPagePalette();
        presentationPagePalette.Start(gamePalette);
        return (gamePalette, presentationPagePalette);
    }

    /// <summary>The single entry the white flash is whitening, or 0 when no entry is white.</summary>
    private static int WhitePaletteIndex(GamePalette gamePalette)
    {
        return Enumerable.Range(PresentationPagePalette.FirstPaletteIndex, 7)
            .SingleOrDefault(paletteIndex => gamePalette.GetPaletteIndexValue(paletteIndex) == 0xFF);
    }

    [Fact]
    public void Start_WritesThePagesSevenColoursAndLeavesTheRestOnCrtab()
    {
        var (gamePalette, _) = Started();

        for (var paletteIndex = PresentationPagePalette.FirstPaletteIndex; paletteIndex <= PresentationPagePalette.LastPaletteIndex; paletteIndex++)
            Assert.Equal(PresentationPagePalette.PageColours[paletteIndex - 1], gamePalette.GetPaletteIndexValue(paletteIndex));

        // The other nine entries are untouched — this page owns palette indices 1-7 and nothing else.
        foreach (var paletteIndex in new[] { 0, 8, 9, 10, 11, 12, 13, 14, 15 })
            Assert.Equal(GamePalette.DefaultPaletteValues[paletteIndex], gamePalette.GetPaletteIndexValue(paletteIndex));

        // The text palette index is ORANGE in the page's own table — the colour the author sees.
        Assert.Equal(0x1F, gamePalette.GetPaletteIndexValue(PresentationPagePalette.TextPaletteIndex));

        // CSET ($8A3A) runs before the white flash task COL1 exists, and COLL ($8A4F) advances BEFORE it whitens: the page
        // comes up with no white on it at all.
        Assert.Equal(0, WhitePaletteIndex(gamePalette));
    }

    [Fact]
    public void TheWhiteFlash_CyclesOnePaletteIndexEveryThreeArcadeFrames_StartingAtTwo()
    {
        var (gamePalette, presentationPagePalette) = Started();
        var moves = new List<(int PaletteIndex, int Tick)>();
        var current = 0;

        for (var tick = 1; tick <= 200 && moves.Count < 9; tick++)
        {
            presentationPagePalette.Update(gamePalette);

            // Once the white flash has started, exactly one entry is white at a time — and it walks
            // 2,3,…,7,1 and round.
            var white = WhitePaletteIndex(gamePalette);
            if (current != 0) Assert.NotEqual(0, white);

            if (white != current && white != 0)
            {
                moves.Add((white, tick));
                current = white;
            }
        }

        int[] expected = [2, 3, 4, 5, 6, 7, 1, 2, 3];
        Assert.Equal(expected, moves.Select(move => move.PaletteIndex));

        // Three fiftieths of a second is 18 clock units and the clock advances 5 clock units a tick, so a cycle lands on
        // the third or the fourth tick — never earlier and never later.
        var gaps = moves.Zip(moves.Skip(1), (first, second) => second.Tick - first.Tick).ToArray();
        Assert.All(gaps, gap => Assert.InRange(gap, 3, 4));
    }

    [Fact]
    public void EveryOtherEntryIsBackOnTheTableOnEachCycle()
    {
        var (gamePalette, presentationPagePalette) = Started();

        for (var cycle = 0; cycle < 15; cycle++)
        {
            for (var tick = 0; tick < 4; tick++) presentationPagePalette.Update(gamePalette);

            var white = WhitePaletteIndex(gamePalette);
            for (var paletteIndex = PresentationPagePalette.FirstPaletteIndex; paletteIndex <= PresentationPagePalette.LastPaletteIndex; paletteIndex++)
                if (paletteIndex != white)
                    Assert.Equal(PresentationPagePalette.PageColours[paletteIndex - 1], gamePalette.GetPaletteIndexValue(paletteIndex));
        }
    }

    [Fact]
    public void TheTextPaletteIndexIsWhiteForOneFrameInEverySeven()
    {
        var (gamePalette, presentationPagePalette) = Started();
        const int Ticks = 7 * 100; // a hundred white flash laps' worth
        var white = 0;

        for (var tick = 0; tick < Ticks; tick++)
        {
            if (gamePalette.GetPaletteIndexValue(PresentationPagePalette.TextPaletteIndex) == 0xFF) white++;

            presentationPagePalette.Update(gamePalette);
        }

        // One cycle in seven whitens entry 6, so the text flashes for a seventh of the time — the
        // orange/white alternation the author reported.
        Assert.InRange((double)white / Ticks, 1.0 / 7.0 - 0.02, 1.0 / 7.0 + 0.02);
    }

    [Fact]
    public void TheLargeTitleLettersColourCyclesDownTheSevenEntriesOnceEveryTwentyEightFrames()
    {
        var (gamePalette, presentationPagePalette) = Started();

        // FORMX's SUBA #$11 ($89DA) walks the operand $77 → $66 → … → $11 and then resets to $77, so the
        // palette index walks 7 → 6 → … → 1 → 7, and the rim is the entry one place behind it.
        (int PaletteIndex, int Rim)[] expected =
        [
            (7, 6), (6, 5), (5, 4), (4, 3), (3, 2), (2, 1), (1, 7)
        ];

        Assert.Equal(expected[0].PaletteIndex, presentationPagePalette.GetLargeTitleLettersPaletteIndex());
        Assert.Equal(expected[0].Rim, presentationPagePalette.GetLargeTitleLettersRimPaletteIndex());

        for (var cycle = 1; cycle < expected.Length; cycle++)
        {
            for (var tick = 0; tick < Ticks(28); tick++) presentationPagePalette.Update(gamePalette);

            Assert.Equal(expected[cycle].PaletteIndex, presentationPagePalette.GetLargeTitleLettersPaletteIndex());
            Assert.Equal(expected[cycle].Rim, presentationPagePalette.GetLargeTitleLettersRimPaletteIndex());
        }

        // At the wrap the pair is the reference screenshot's own: a red body (palette index 1, $07) on a
        // yellow rim (palette index 7, $3F).
        Assert.Equal(0x07, PresentationPagePalette.PageColours[presentationPagePalette.GetLargeTitleLettersPaletteIndex() - 1]);
        Assert.Equal(0x3F, PresentationPagePalette.PageColours[presentationPagePalette.GetLargeTitleLettersRimPaletteIndex() - 1]);
    }

    [Fact]
    public void Stop_StandsCrtabBackUpOverThePagesSevenEntries()
    {
        var (gamePalette, presentationPagePalette) = Started();
        presentationPagePalette.Stop(gamePalette);

        for (var paletteIndex = 0; paletteIndex <= 15; paletteIndex++)
            Assert.Equal(GamePalette.DefaultPaletteValues[paletteIndex], gamePalette.GetPaletteIndexValue(paletteIndex));
    }
}
