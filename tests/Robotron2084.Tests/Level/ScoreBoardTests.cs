using Robotron2084.Level;
using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The run's score and its EXTRA MAN EVERY threshold (notes §131; the GAME ADJUSTMENT page's first row).</summary>
public sealed class ScoreBoardTests
{
    [Fact]
    public void TheFactoryThresholdAwardsALifeAt25000Points()
    {
        var board = new ScoreBoard(0);

        Assert.False(board.Add(24_999));
        Assert.True(board.Add(1));
        Assert.False(board.Add(24_999));
        Assert.True(board.Add(1));
    }

    [Fact]
    public void AStartingScoreSkipsThresholdsAlreadyPassed()
    {
        var board = new ScoreBoard(30_000);

        Assert.False(board.Add(19_999));
        Assert.True(board.Add(1));
    }

    [Fact]
    public void NoExtraMenMeansTheThresholdIsNeverCrossed()
    {
        var board = new ScoreBoard(0, extraLifeEveryPoints: 0);

        Assert.False(board.Add(500_000));
    }

    [Fact]
    public void TheArcadesOwnStopsScaleTheThreshold()
    {
        Assert.True(new ScoreBoard(0, extraLifeEveryPoints: 20_000).Add(20_000));
        Assert.True(new ScoreBoard(0, extraLifeEveryPoints: 30_000).Add(30_000));
        Assert.True(new ScoreBoard(0, extraLifeEveryPoints: 50_000).Add(50_000));
    }

    [Fact]
    public void TheFactoryExtraManValueIsTheArcardsRecommendedStop()
    {
        Assert.Equal(25_000, GameSettings.FactoryExtraManEveryPoints);
    }
}
