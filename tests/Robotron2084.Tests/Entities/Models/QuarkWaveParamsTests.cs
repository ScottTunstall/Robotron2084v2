using Robotron2084.Entities.Models;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>What a wave sets for its quarks.</summary>
public sealed class QuarkWaveParamsTests
{
    [Fact]
    public void KeepsWhatItIsGiven()
    {
        var settings = new QuarkWaveParams(4, 20, 30);

        Assert.Equal(4, settings.MaxDropsX2);
        Assert.Equal(20, settings.DropIntervalMaxBeats);
        Assert.Equal(30, settings.MaxSpeed);
    }

    [Fact]
    public void UsesTheDefaultsForWhatIsNotGiven()
    {
        var settings = new QuarkWaveParams();

        Assert.Equal(QuarkWaveParams.DefaultMaxDropsX2, settings.MaxDropsX2);
        Assert.Equal(QuarkWaveParams.DefaultDropIntervalMaxBeats, settings.DropIntervalMaxBeats);
        Assert.Equal(QuarkWaveParams.DefaultMaxSpeed, settings.MaxSpeed);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void RefusesANumberBelowZero(int maxDropsX2, int dropIntervalMaxBeats, int maxSpeed)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new QuarkWaveParams(maxDropsX2, dropIntervalMaxBeats, maxSpeed));
    }
}
