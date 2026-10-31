using Microsoft.Xna.Framework;
using Robotron2084.Graphics;
using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
///     The 16-index live palette: CRTAB defaults, palette index writes, and the
///     seanriddle.com byte→RGB conversion (RobotronPaletteService port).
/// </summary>
public sealed class GamePaletteTests
{
    [Fact]
    public void DefaultsAreTheCrtabPalette()
    {
        GamePalette gamePalette = new();
        Assert.Equal(0x00, gamePalette.GetPaletteIndexValue(0));
        Assert.Equal(0x07, gamePalette.GetPaletteIndexValue(1));
        Assert.Equal(0x17, gamePalette.GetPaletteIndexValue(2));
        Assert.Equal(0xC7, gamePalette.GetPaletteIndexValue(3));
        Assert.Equal(0x1F, gamePalette.GetPaletteIndexValue(4));
        Assert.Equal(0x3F, gamePalette.GetPaletteIndexValue(5));
        Assert.Equal(0x38, gamePalette.GetPaletteIndexValue(6));
        Assert.Equal(0xC0, gamePalette.GetPaletteIndexValue(7));
        Assert.Equal(0xA4, gamePalette.GetPaletteIndexValue(8));
        Assert.Equal(0xFF, gamePalette.GetPaletteIndexValue(9));
    }

    [Fact]
    public void SetPaletteIndexValueChangesTheLiveColour()
    {
        GamePalette gamePalette = new();
        Assert.NotEqual(gamePalette.GetColour(11), gamePalette.GetColour(0));

        gamePalette.SetPaletteIndexValue(11, 0x00);
        Assert.Equal(0x00, gamePalette.GetPaletteIndexValue(11));
        Assert.Equal(new Color(0, 0, 0), gamePalette.GetColour(11));
    }

    [Theory]
    [InlineData(0x00, 0, 0, 0)] // black
    [InlineData(0x07, 240, 0, 0)] // red
    [InlineData(0x17, 240, 64, 0)] // orange
    [InlineData(0xC7, 240, 0, 240)] // purple
    [InlineData(0x3F, 240, 240, 0)] // yellow
    [InlineData(0x38, 0, 240, 0)] // green
    [InlineData(0xC0, 0, 0, 240)] // blue
    [InlineData(0xFF, 240, 240, 240)] // white
    public void ByteToRgbMatchesTheServiceConversion(byte value, int r, int g, int b)
    {
        Assert.Equal(new Color(r, g, b), RobotronColor.CreateFromByte(value));
    }

    [Fact]
    public void AllSixCyclingMarkersAreDistinctFromTheFixedPaletteIndices()
    {
        // A marker texel must identify its palette index unambiguously: no marker RGB
        // may equal any fixed palette index (0-9) RGB, and all six markers differ.
        var markerColors = GamePalette.CyclingPaletteIndexMarkers
            .Select(v => RobotronColor.CreateFromByte(v))
            .ToArray();

        for (var paletteIndex = 0; paletteIndex < 10; paletteIndex++)
        {
            var fixedColor = RobotronColor.CreateFromByte(GamePalette.DefaultPaletteValues[paletteIndex]);
            Assert.DoesNotContain(fixedColor, markerColors);
        }

        Assert.Equal(6, markerColors.Distinct().Count());
    }
}
