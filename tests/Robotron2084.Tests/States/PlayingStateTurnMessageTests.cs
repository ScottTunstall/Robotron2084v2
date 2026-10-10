using Robotron2084.Level;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>
///     The "PLAYER n" message of a two-player game (notes §143), from `RRG23.ASM` `PLS000` to `PLS0B` (R5 $27E7 to $281D).
///     The arcade shows it only when `PCFLG` is set, which is when a game starts and after a death (`LDA PCFLG / BNE
///     PLS00C`;
///     "NO DEATH.. NO MESSIE POOH"), and only when there are two players (`LDA PLRCNT / DECA / BEQ PLS0A`). It comes
///     before
///     the wave is set up (`NAP 115,PLS0B`, and only then `PLS0A`).
/// </summary>
public sealed class PlayingStateTurnMessageTests
{
    private static PlayingState CreateState(int playerCount, bool isStartOfTurn)
    {
        return new PlayingState(
            TestSprites.Shared,
            null!,
            GameSession.CreateNewGame(new FakeInputSource(), playerCount, new FakeInputSource()),
            isStartOfTurn);
    }

    [Fact]
    public void ATwoPlayerGame_ShowsWhoseTurnItIs_WhenATurnStarts()
    {
        Assert.True(CreateState(2, true).IsAnnouncingTurn());
    }

    [Fact]
    public void ATwoPlayerGame_DoesNotShowIt_WhenAPlayerGoesOnToTheNextWave()
    {
        Assert.False(CreateState(2, false).IsAnnouncingTurn());
    }

    [Fact]
    public void AOnePlayerGame_NeverShowsIt()
    {
        Assert.False(CreateState(1, true).IsAnnouncingTurn());
    }
}
