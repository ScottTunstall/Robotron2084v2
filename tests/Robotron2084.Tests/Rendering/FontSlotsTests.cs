using Robotron2084.Rendering;
using Xunit;

namespace Robotron2084.Tests.Rendering;

/// <summary>Blitter-remap slot classification (notes §39).</summary>
public class FontSlotsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void StaticSlots_DoNotCycle(int slot)
    {
        Assert.False(FontSlots.IsCycling(slot));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void CyclingSlots_Cycle(int slot)
    {
        Assert.True(FontSlots.IsCycling(slot));
    }

    [Fact]
    public void Range_IsTheSixArcadeCyclingSlots()
    {
        Assert.Equal(10, FontSlots.FirstCyclingSlot);
        Assert.Equal(15, FontSlots.LastCyclingSlot);
        Assert.Equal(6, FontSlots.LastCyclingSlot - FontSlots.FirstCyclingSlot + 1);
    }
}
