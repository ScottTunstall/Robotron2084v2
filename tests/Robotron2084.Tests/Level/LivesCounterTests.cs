using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>One player's count of lives.</summary>
public sealed class LivesCounterTests
{
    [Fact]
    public void StartsWithTheLivesItIsGiven()
    {
        Assert.Equal(3, new LivesCounter(3).Lives);
    }

    [Fact]
    public void AddLifeAddsOne_AndLoseLifeTakesOneOff()
    {
        var livesCounter = new LivesCounter(3);

        livesCounter.AddLife();
        Assert.Equal(4, livesCounter.Lives);

        livesCounter.LoseLife();
        livesCounter.LoseLife();
        Assert.Equal(2, livesCounter.Lives);
    }

    [Fact]
    public void ACountOfNoLivesStaysAtNone()
    {
        var livesCounter = new LivesCounter(0);

        livesCounter.LoseLife();

        Assert.Equal(0, livesCounter.Lives);
    }

    [Fact]
    public void SetLivesPutsTheCountAtANewValue()
    {
        var livesCounter = new LivesCounter(3);

        livesCounter.SetLives(7);

        Assert.Equal(7, livesCounter.Lives);
    }

    [Fact]
    public void ACountBelowZeroIsRefused()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LivesCounter(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LivesCounter(3).SetLives(-1));
    }

    [Fact]
    public void ThePlayerOnAFieldChangesTheCountItIsGiven()
    {
        var livesCounter = new LivesCounter(3);
        var playField = new PlayFieldBuilder().WithLivesCounter(livesCounter).WithPlayerInvincible(false).Build();

        playField.KillPlayer();

        Assert.Equal(2, livesCounter.Lives);
        Assert.Equal(2, playField.Player.Lives);
    }
}
