using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
/// The arcade's alternating two-player session (notes §59): each player owns
/// their score, lives and wave (ROM PLDATA/ZP2SCR), and a death hands the turn to
/// the other player while they still have men (ROM PLE1B).
/// </summary>
public class GameSessionTests
{
    private static GameSession NewTwoPlayerGame() => GameSession.NewGame(new FakeInputSource(), playerCount: 2);

    [Fact]
    public void OnePlayerGame_HasOneSlot_NamedPlayerOne()
    {
        GameSession session = GameSession.NewGame(new FakeInputSource(), playerCount: 1);

        Assert.False(session.IsTwoPlayer);
        Assert.Single(session.Players);
        Assert.Equal(1, session.Current.Number);
        Assert.Equal(GameplayConstants.StartingLives, session.Current.Lives);
        Assert.Equal(GameplayConstants.StartingLevelNumber, session.Current.Wave);
    }

    [Fact]
    public void TwoPlayerGame_StartsWithPlayerOne_AndBothHaveTheirOwnState()
    {
        GameSession session = NewTwoPlayerGame();

        Assert.True(session.IsTwoPlayer);
        Assert.Equal(2, session.Players.Count);
        Assert.Equal(1, session.Current.Number);

        session.Players[1].Score = 4200;
        session.Players[1].Wave = 7;
        Assert.Equal(0, session.Players[0].Score);
        Assert.Equal(GameplayConstants.StartingLevelNumber, session.Players[0].Wave);
    }

    [Fact]
    public void SpareMen_ExcludesTheLifeInPlay()
    {
        // The ROM's p1_men is the lives counter AFTER PLSTRT decremented it: with
        // 3 ships the HUD shows 2 icons during the first life.
        GameSession session = GameSession.NewGame(new FakeInputSource(), playerCount: 1);

        Assert.Equal(3, session.Current.Lives);
        Assert.Equal(2, session.Current.SpareMen);

        session.Current.Lives = 1;
        Assert.Equal(0, session.Current.SpareMen);

        session.Current.Lives = 0;
        Assert.Equal(0, session.Current.SpareMen);
    }

    [Fact]
    public void DisplayedMen_IsCappedAtSeven()
    {
        // ROM MANDSV: "DISPLAY MEN LEFT / MAX OF 7".
        GameSession session = GameSession.NewGame(new FakeInputSource(), playerCount: 1);

        session.Current.Lives = 9; // 8 spare
        Assert.Equal(8, session.Current.SpareMen);
        Assert.Equal(GameplayConstants.HudMaxMen, session.Current.DisplayedMen);

        session.Current.Lives = 4;
        Assert.Equal(3, session.Current.DisplayedMen);
    }

    [Fact]
    public void SwitchToPlayerWithMen_AlternatesWhileBothAreAlive()
    {
        GameSession session = NewTwoPlayerGame();

        Assert.True(session.SwitchToPlayerWithMen());
        Assert.Equal(2, session.Current.Number);

        Assert.True(session.SwitchToPlayerWithMen());
        Assert.Equal(1, session.Current.Number);
    }

    [Fact]
    public void SwitchToPlayerWithMen_StaysWhenTheOtherIsOut()
    {
        // ROM PLE1: EORA #3 / PLDX / LDB PLAS,X / BEQ PLE1 — an out player is
        // skipped, and if nobody else has men the turn does not move.
        GameSession session = NewTwoPlayerGame();
        session.Players[1].Lives = 0;

        Assert.False(session.SwitchToPlayerWithMen());
        Assert.Equal(1, session.Current.Number);
    }

    [Fact]
    public void SwitchToPlayerWithMen_NeverMovesInAOnePlayerGame()
    {
        GameSession session = GameSession.NewGame(new FakeInputSource(), playerCount: 1);

        Assert.False(session.SwitchToPlayerWithMen());
        Assert.Equal(1, session.Current.Number);
    }

    [Fact]
    public void AnyMenLeft_IsFalseOnlyWhenEverybodyIsOut()
    {
        GameSession session = NewTwoPlayerGame();

        Assert.True(session.AnyMenLeft);

        session.Players[0].Lives = 0;
        Assert.True(session.AnyMenLeft);

        session.Players[1].Lives = 0;
        Assert.False(session.AnyMenLeft);
    }

    [Fact]
    public void ScoresHighestFirst_OrdersBothPlayers()
    {
        GameSession session = NewTwoPlayerGame();
        session.Players[0].Score = 1500;
        session.Players[1].Score = 9800;

        Assert.Equal([9800, 1500], session.ScoresHighestFirst());
    }

    [Fact]
    public void NewGame_RejectsThreePlayers()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => GameSession.NewGame(new FakeInputSource(), playerCount: 3));
    }
}
