using Robotron2084.Graphics;
using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Graphics;

/// <summary>
///     The DEFINE INPUTS page's selected-line colour cycle (notes §115): palette index 8 on the page's
///     GREEN, with a WHITE flash landing one step in seven on the presentation page's own 3-arcade frame
///     chase clock (notes §106) — the same cycling the intro pages run on their text.
/// </summary>
public sealed class DefineInputsHighlightTests
{
    private static (GamePalette GamePalette, DefineInputsHighlight DefineInputsHighlight) Started()
    {
        var gamePalette = new GamePalette();
        var defineInputsHighlight = new DefineInputsHighlight();
        defineInputsHighlight.Start(gamePalette);
        return (gamePalette, defineInputsHighlight);
    }

    [Fact]
    public void Start_PutsThePaletteIndexOnThePagesGreenAndLeavesEveryOtherPaletteIndexAlone()
    {
        var (gamePalette, _) = Started();

        // The page comes up green with no white on it, as the presentation page does: its table
        // copy (ROM $8A3A) runs before its first chase step.
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(DefineInputsHighlight.PaletteIndex));

        foreach (var paletteIndex in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15 })
            Assert.Equal(GamePalette.DefaultPaletteValues[paletteIndex], gamePalette.GetPaletteIndexValue(paletteIndex));
    }

    [Fact]
    public void TheWhiteFlash_ReturnsOnceInSevenSteps_ThreeArcadeFramesApart()
    {
        var (gamePalette, defineInputsHighlight) = Started();
        var flashTicks = new List<int>();
        var wasWhite = false;

        for (var tick = 1; tick <= 300; tick++)
        {
            defineInputsHighlight.Update(gamePalette);
            var isWhite = gamePalette.GetPaletteIndexValue(DefineInputsHighlight.PaletteIndex) == 0xFF;
            if (isWhite && !wasWhite) flashTicks.Add(tick);

            wasWhite = isWhite;
        }

        Assert.True(flashTicks.Count >= 10);

        // Seven steps of three fiftieths of a second (3.6 ticks each) put a flash back roughly every 25 ticks.
        var gaps = flashTicks.Zip(flashTicks.Skip(1), (first, second) => second - first).ToArray();
        Assert.All(gaps, gap => Assert.InRange(gap, 24, 27));
    }

    [Fact]
    public void ThePaletteIndexSitsOnGreenBetweenTheFlashes()
    {
        var (gamePalette, defineInputsHighlight) = Started();

        for (var tick = 0; tick < 300; tick++)
        {
            defineInputsHighlight.Update(gamePalette);
            var value = gamePalette.GetPaletteIndexValue(DefineInputsHighlight.PaletteIndex);
            Assert.True(value is 0x38 or 0xFF, $"the palette index held {value:X2}");
        }
    }

    [Fact]
    public void ThePaletteIndexIsWhiteForOneStepInSeven()
    {
        var (gamePalette, defineInputsHighlight) = Started();
        const int Ticks = 700;
        var white = 0;

        for (var tick = 0; tick < Ticks; tick++)
        {
            if (gamePalette.GetPaletteIndexValue(DefineInputsHighlight.PaletteIndex) == 0xFF) white++;

            defineInputsHighlight.Update(gamePalette);
        }

        // One step in seven is white, so the label flashes for a seventh of the time — the same
        // duty as the presentation page's text palette index (notes §106).
        Assert.InRange((double)white / Ticks, 1.0 / 7.0 - 0.02, 1.0 / 7.0 + 0.02);
    }

    [Fact]
    public void TheChase_TouchesOnlyItsOwnPaletteIndex()
    {
        var (gamePalette, defineInputsHighlight) = Started();

        for (var tick = 0; tick < 300; tick++) defineInputsHighlight.Update(gamePalette);

        foreach (var paletteIndex in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15 })
            Assert.Equal(GamePalette.DefaultPaletteValues[paletteIndex], gamePalette.GetPaletteIndexValue(paletteIndex));
    }
}
