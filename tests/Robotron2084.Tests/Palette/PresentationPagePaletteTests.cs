using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
///     The Williams presentation page's own colour set (notes §106): the seven entries at ROM `$8A70`
///     written into palette indices 1-7 (`$8A3A`), the WHITE flash the page chases through them every
///     three fiftieths of a second (`$8A4F`/`$8A68`), and the border logos' colour step (`$89DA`'s `$77 → $66 → … → $11 →
///     $77`) that the port's traced wordmark borrows.
/// </summary>
public sealed class PresentationPagePaletteTests
{
    /// <summary>A fiftieth of a second is 6/5 of a port tick (notes §52): n frames land on ceil(n × 6/5).</summary>
    private static int Ticks(int arcadeFrames)
    {
        return (int)Math.Ceiling(arcadeFrames * 6.0 / 5.0);
    }

    private static (GamePalette Palette, PresentationPagePalette Page) Started()
    {
        var palette = new GamePalette();
        var page = new PresentationPagePalette();
        page.Start(palette);
        return (palette, page);
    }

    /// <summary>The single entry the chase is whitening, or 0 when no entry is white.</summary>
    private static int WhitePaletteIndex(GamePalette palette)
    {
        return Enumerable.Range(PresentationPagePalette.FirstPaletteIndex, 7)
            .SingleOrDefault(paletteIndex => palette.GetPaletteIndexValue(paletteIndex) == 0xFF);
    }

    [Fact]
    public void Start_WritesThePagesSevenColoursAndLeavesTheRestOnCrtab()
    {
        var (palette, _) = Started();

        for (var paletteIndex = PresentationPagePalette.FirstPaletteIndex; paletteIndex <= PresentationPagePalette.LastPaletteIndex; paletteIndex++)
            Assert.Equal(PresentationPagePalette.PageColors[paletteIndex - 1], palette.GetPaletteIndexValue(paletteIndex));

        // The other nine entries are untouched — this page owns palette indices 1-7 and nothing else.
        foreach (var paletteIndex in new[] { 0, 8, 9, 10, 11, 12, 13, 14, 15 })
            Assert.Equal(GamePalette.DefaultPaletteValues[paletteIndex], palette.GetPaletteIndexValue(paletteIndex));

        // The text palette index is ORANGE in the page's own table — the colour the author sees.
        Assert.Equal(0x1F, palette.GetPaletteIndexValue(PresentationPagePalette.TextPaletteIndex));

        // $8A3A runs before the chase task exists, and $8A4F advances BEFORE it whitens: the page
        // comes up with no white on it at all.
        Assert.Equal(0, WhitePaletteIndex(palette));
    }

    [Fact]
    public void TheWhiteFlash_StepsOnePaletteIndexEveryThreeArcadeFrames_StartingAtTwo()
    {
        var (palette, page) = Started();
        var moves = new List<(int PaletteIndex, int Tick)>();
        var current = 0;

        for (var tick = 1; tick <= 200 && moves.Count < 9; tick++)
        {
            page.Update(palette);

            // Once the chase has started, exactly one entry is white at a time — and it walks
            // 2,3,…,7,1 and round.
            var white = WhitePaletteIndex(palette);
            if (current != 0) Assert.NotEqual(0, white);

            if (white != current && white != 0)
            {
                moves.Add((white, tick));
                current = white;
            }
        }

        int[] expected = [2, 3, 4, 5, 6, 7, 1, 2, 3];
        Assert.Equal(expected, moves.Select(move => move.PaletteIndex));

        // Three fiftieths of a second is 18 clock units and the clock advances 5 clock units a tick, so a step lands on
        // the third or the fourth tick — never earlier and never later.
        var gaps = moves.Zip(moves.Skip(1), (first, second) => second.Tick - first.Tick).ToArray();
        Assert.All(gaps, gap => Assert.InRange(gap, 3, 4));
    }

    [Fact]
    public void EveryOtherEntryIsBackOnTheTableOnEachStep()
    {
        var (palette, page) = Started();

        for (var step = 0; step < 15; step++)
        {
            for (var tick = 0; tick < 4; tick++) page.Update(palette);

            var white = WhitePaletteIndex(palette);
            for (var paletteIndex = PresentationPagePalette.FirstPaletteIndex; paletteIndex <= PresentationPagePalette.LastPaletteIndex; paletteIndex++)
                if (paletteIndex != white)
                    Assert.Equal(PresentationPagePalette.PageColors[paletteIndex - 1], palette.GetPaletteIndexValue(paletteIndex));
        }
    }

    [Fact]
    public void TheTextPaletteIndexIsWhiteForOneFrameInEverySeven()
    {
        var (palette, page) = Started();
        const int Ticks = 7 * 100; // a hundred chase laps' worth
        var white = 0;

        for (var tick = 0; tick < Ticks; tick++)
        {
            if (palette.GetPaletteIndexValue(PresentationPagePalette.TextPaletteIndex) == 0xFF) white++;

            page.Update(palette);
        }

        // One step in seven whitens entry 6, so the text flashes for a seventh of the time — the
        // orange/white alternation the author reported.
        Assert.InRange((double)white / Ticks, 1.0 / 7.0 - 0.02, 1.0 / 7.0 + 0.02);
    }

    [Fact]
    public void TheWordmarkColourStepsDownTheSevenEntriesOnceEveryTwentyEightFrames()
    {
        var (palette, page) = Started();

        // $89DA's SUBA #$11 walks the operand $77 → $66 → … → $11 and then resets to $77, so the
        // palette index walks 7 → 6 → … → 1 → 7, and the rim is the entry one step behind it.
        (int PaletteIndex, int Rim)[] expected =
        [
            (7, 6), (6, 5), (5, 4), (4, 3), (3, 2), (2, 1), (1, 7)
        ];

        Assert.Equal(expected[0].PaletteIndex, page.GetWordmarkPaletteIndex());
        Assert.Equal(expected[0].Rim, page.GetWordmarkRimPaletteIndex());

        for (var step = 1; step < expected.Length; step++)
        {
            for (var tick = 0; tick < Ticks(28); tick++) page.Update(palette);

            Assert.Equal(expected[step].PaletteIndex, page.GetWordmarkPaletteIndex());
            Assert.Equal(expected[step].Rim, page.GetWordmarkRimPaletteIndex());
        }

        // At the wrap the pair is the reference screenshot's own: a red body (palette index 1, $07) on a
        // yellow rim (palette index 7, $3F).
        Assert.Equal(0x07, PresentationPagePalette.PageColors[page.GetWordmarkPaletteIndex() - 1]);
        Assert.Equal(0x3F, PresentationPagePalette.PageColors[page.GetWordmarkRimPaletteIndex() - 1]);
    }

    [Fact]
    public void Stop_StandsCrtabBackUpOverThePagesSevenEntries()
    {
        var (palette, page) = Started();
        page.Stop(palette);

        for (var paletteIndex = 0; paletteIndex <= 15; paletteIndex++)
            Assert.Equal(GamePalette.DefaultPaletteValues[paletteIndex], palette.GetPaletteIndexValue(paletteIndex));
    }
}
