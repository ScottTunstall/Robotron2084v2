using Robotron2084.Entities.Models;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>How many grunts a Gorf drops.</summary>
public sealed class GorfDropsTests
{
    [Fact]
    public void KeepsWhatItIsGiven()
    {
        var drops = new GorfDrops(10, 4, 7);

        Assert.Equal(10, drops.MaxDropsX2);
        Assert.Equal(4, drops.GruntDropCount);
        Assert.Equal(7, drops.DropIntervalSteps);
    }

    [Fact]
    public void LeavesTheDropsToChanceUnlessToldHowMany()
    {
        var drops = new GorfDrops(10);

        Assert.Equal(0, drops.GruntDropCount);
        Assert.Equal(0, drops.DropIntervalSteps);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(10, -1, 0)]
    [InlineData(10, 0, -1)]
    public void RefusesANumberBelowZero(int maxDropsX2, int gruntDropCount, int dropIntervalSteps)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new GorfDrops(maxDropsX2, gruntDropCount, dropIntervalSteps));
    }
}
