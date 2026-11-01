using Robotron2084.Graphics;
using Xunit;

namespace Robotron2084.Tests.Graphics;

/// <summary>Blitter-remap palette index classification (notes §39).</summary>
public class FontPaletteIndicesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void StaticPaletteIndices_DoNotCycle(int paletteIndex)
    {
        Assert.False(FontPaletteIndices.IsCycling(paletteIndex));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void CyclingPaletteIndices_Cycle(int paletteIndex)
    {
        Assert.True(FontPaletteIndices.IsCycling(paletteIndex));
    }

    [Fact]
    public void Range_IsTheSixArcadeCyclingPaletteIndices()
    {
        Assert.Equal(10, FontPaletteIndices.FirstCyclingPaletteIndex);
        Assert.Equal(15, FontPaletteIndices.LastCyclingPaletteIndex);
        Assert.Equal(6, FontPaletteIndices.LastCyclingPaletteIndex - FontPaletteIndices.FirstCyclingPaletteIndex + 1);
    }
}
