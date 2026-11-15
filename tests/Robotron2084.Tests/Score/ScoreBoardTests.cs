using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Score;
using Xunit;

namespace Robotron2084.Tests.Score;

/// <summary>The run's score and its EXTRA MAN EVERY threshold (notes §131; the GAME ADJUSTMENT page's first row).</summary>
public sealed class ScoreBoardTests
{
    [Fact]
    public void TheFactoryThresholdAwardsALifeAt25000Points()
    {
        var scoreBoard = new ScoreBoard(0);

        Assert.False(scoreBoard.Add(24_999));
        Assert.True(scoreBoard.Add(1));
        Assert.False(scoreBoard.Add(24_999));
        Assert.True(scoreBoard.Add(1));
    }

    [Fact]
    public void AStartingScoreSkipsThresholdsAlreadyPassed()
    {
        var scoreBoard = new ScoreBoard(30_000);

        Assert.False(scoreBoard.Add(19_999));
        Assert.True(scoreBoard.Add(1));
    }

    [Fact]
    public void NoExtraMenMeansTheThresholdIsNeverCrossed()
    {
        var scoreBoard = new ScoreBoard(0, 0);

        Assert.False(scoreBoard.Add(500_000));
    }

    [Fact]
    public void TheArcadesOwnStopsScaleTheThreshold()
    {
        Assert.True(new ScoreBoard(0, 20_000).Add(20_000));
        Assert.True(new ScoreBoard(0, 30_000).Add(30_000));
        Assert.True(new ScoreBoard(0, 50_000).Add(50_000));
    }

    [Fact]
    public void TheFactoryExtraLifeValueIsTheArcardsRecommendedStop()
    {
        Assert.Equal(25_000, GameSettings.FactoryExtraLifeEveryPoints);
    }

    [Fact]
    public void SetScoreDoesNotGiveTheSpareLivesItPasses_AndTheNextComesAtTheNextMultiple()
    {
        var scoreBoard = new ScoreBoard(0, 20_000);

        scoreBoard.SetScore(45_000);

        Assert.Equal(45_000, scoreBoard.Score);
        Assert.False(scoreBoard.Add(14_999));
        Assert.True(scoreBoard.Add(1));
    }

    [Fact]
    public void SetScoreRefusesAScoreBelowZero()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreBoard(0).SetScore(-1));
    }

    [Fact]
    public void ASpareLifeIsGivenOnceAcrossTwoFieldsThatShareTheBoard()
    {
        // A player's board lives through every field they play on, so the life at 20,000 comes once however the
        // points are split between the fields.
        var scoreBoard = new ScoreBoard(0, 20_000);
        var firstField = new PlayFieldBuilder().WithScoreBoard(scoreBoard).Build();
        var secondField = new PlayFieldBuilder().WithScoreBoard(scoreBoard).Build();

        Assert.False(firstField.ScoreBoard.Add(15_000));
        Assert.True(secondField.ScoreBoard.Add(5_000));
        Assert.False(secondField.ScoreBoard.Add(5_000));
        Assert.Same(scoreBoard, secondField.ScoreBoard);
    }
}
