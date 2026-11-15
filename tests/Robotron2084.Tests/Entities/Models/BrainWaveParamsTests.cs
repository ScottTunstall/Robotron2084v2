using Robotron2084.Entities.Models;
using Xunit;

namespace Robotron2084.Tests.Entities.Models;

/// <summary>What a wave sets for its brains.</summary>
public sealed class BrainWaveParamsTests
{
    [Fact]
    public void KeepsWhatItIsGiven()
    {
        var settings = new BrainWaveParams(8, 40);

        Assert.Equal(8, settings.BeatWaitArcadeFrames);
        Assert.Equal(40, settings.FireIntervalBeats);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void RefusesANumberBelowZero(int beatWaitArcadeFrames, int fireIntervalBeats)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BrainWaveParams(beatWaitArcadeFrames, fireIntervalBeats));
    }
}
