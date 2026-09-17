using Microsoft.Xna.Framework;
using Robotron2084.Rendering;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
/// The 16-slot live palette: CRTAB defaults, slot writes, and the
/// seanriddle.com byte→RGB conversion (RobotronPaletteService port).
/// </summary>
public sealed class GamePaletteTests
{
    [Fact]
    public void DefaultsAreTheCrtabPalette()
    {
        GamePalette palette = new();
        Assert.Equal(0x00, palette.SlotValue(0));
        Assert.Equal(0x07, palette.SlotValue(1));
        Assert.Equal(0x17, palette.SlotValue(2));
        Assert.Equal(0xC7, palette.SlotValue(3));
        Assert.Equal(0x1F, palette.SlotValue(4));
        Assert.Equal(0x3F, palette.SlotValue(5));
        Assert.Equal(0x38, palette.SlotValue(6));
        Assert.Equal(0xC0, palette.SlotValue(7));
        Assert.Equal(0xA4, palette.SlotValue(8));
        Assert.Equal(0xFF, palette.SlotValue(9));
    }

    [Fact]
    public void SetSlotChangesTheLiveColour()
    {
        GamePalette palette = new();
        Assert.NotEqual(palette.Color(11), palette.Color(0));

        palette.SetSlot(11, 0x00);
        Assert.Equal(0x00, palette.SlotValue(11));
        Assert.Equal(new Color(0, 0, 0), palette.Color(11));
    }

    [Theory]
    [InlineData(0x00, 0, 0, 0)]     // black
    [InlineData(0x07, 240, 0, 0)]   // red
    [InlineData(0x17, 240, 64, 0)]  // orange
    [InlineData(0xC7, 240, 0, 240)] // purple
    [InlineData(0x3F, 240, 240, 0)] // yellow
    [InlineData(0x38, 0, 240, 0)]   // green
    [InlineData(0xC0, 0, 0, 240)]   // blue
    [InlineData(0xFF, 240, 240, 240)] // white
    public void ByteToRgbMatchesTheServiceConversion(byte value, int r, int g, int b)
    {
        Assert.Equal(new Color(r, g, b), RobotronColor.FromByte(value));
    }

    [Fact]
    public void AllSixCyclingMarkersAreDistinctFromTheFixedSlots()
    {
        // A marker texel must identify its slot unambiguously: no marker RGB
        // may equal any fixed-slot (0-9) RGB, and all six markers differ.
        var markerColors = GamePalette.CyclingSlotMarkers
            .Select(v => RobotronColor.FromByte(v))
            .ToArray();

        for (int slot = 0; slot < 10; slot++)
        {
            Color fixedColor = RobotronColor.FromByte(GamePalette.DefaultSlots[slot]);
            Assert.DoesNotContain(fixedColor, markerColors);
        }

        Assert.Equal(6, markerColors.Distinct().Count());
    }
}
