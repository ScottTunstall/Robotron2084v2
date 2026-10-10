using Microsoft.Xna.Framework;
using Robotron2084.Graphics;
using Robotron2084.Palette;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>
///     The 16-slot live palette: CRTAB defaults, slot writes, and the
///     seanriddle.com byte→RGB conversion (RobotronPaletteService port).
/// </summary>
public sealed class GamePaletteTests
{
    [Fact]
    public void DefaultsAreTheCrtabPalette()
    {
        GamePalette palette = new();
        Assert.Equal(0x00, palette.GetSlotValue(0));
        Assert.Equal(0x07, palette.GetSlotValue(1));
        Assert.Equal(0x17, palette.GetSlotValue(2));
        Assert.Equal(0xC7, palette.GetSlotValue(3));
        Assert.Equal(0x1F, palette.GetSlotValue(4));
        Assert.Equal(0x3F, palette.GetSlotValue(5));
        Assert.Equal(0x38, palette.GetSlotValue(6));
        Assert.Equal(0xC0, palette.GetSlotValue(7));
        Assert.Equal(0xA4, palette.GetSlotValue(8));
        Assert.Equal(0xFF, palette.GetSlotValue(9));
    }

    [Fact]
    public void SetSlotChangesTheLiveColour()
    {
        GamePalette palette = new();
        Assert.NotEqual(palette.GetColour(11), palette.GetColour(0));

        palette.SetSlot(11, 0x00);
        Assert.Equal(0x00, palette.GetSlotValue(11));
        Assert.Equal(new Color(0, 0, 0), palette.GetColour(11));
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
    public void AllSixCyclingMarkersAreDistinctFromTheFixedSlots()
    {
        // A marker texel must identify its slot unambiguously: no marker RGB
        // may equal any fixed-slot (0-9) RGB, and all six markers differ.
        var markerColors = GamePalette.CyclingSlotMarkers
            .Select(v => RobotronColor.CreateFromByte(v))
            .ToArray();

        for (var slot = 0; slot < 10; slot++)
        {
            var fixedColor = RobotronColor.CreateFromByte(GamePalette.DefaultSlotValues[slot]);
            Assert.DoesNotContain(fixedColor, markerColors);
        }

        Assert.Equal(6, markerColors.Distinct().Count());
    }
}
