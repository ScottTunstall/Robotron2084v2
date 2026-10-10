using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
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
    private static GameSession NewTwoPlayerGame() => GameSession.CreateNewGame(new FakeInputSource(), playerCount: 2);

    [Fact]
    public void OnePlayerGame_HasOneSlot_NamedPlayerOne()
    {
        GameSession session = GameSession.CreateNewGame(new FakeInputSource(), playerCount: 1);

        Assert.False(session.IsTwoPlayer());
        Assert.Single(session.Players);
        Assert.Equal(1, session.GetCurrent().Number);
        Assert.Equal(PlayerTuning.StartingLives, session.GetCurrent().Lives);
        Assert.Equal(PlayerTuning.StartingLevelNumber, session.GetCurrent().Wave);
    }

    [Fact]
    public void TwoPlayerGame_StartsWithPlayerOne_AndBothHaveTheirOwnState()
    {
        GameSession session = NewTwoPlayerGame();

        Assert.True(session.IsTwoPlayer());
        Assert.Equal(2, session.Players.Count);
        Assert.Equal(1, session.GetCurrent().Number);

        session.Players[1].Score = 4200;
        session.Players[1].Wave = 7;
        Assert.Equal(0, session.Players[0].Score);
        Assert.Equal(PlayerTuning.StartingLevelNumber, session.Players[0].Wave);
    }

    [Fact]
    public void SpareMen_ExcludesTheLifeInPlay()
    {
        // The ROM's p1_men is the lives counter AFTER PLSTRT decremented it: with
        // 3 ships the HUD shows 2 icons during the first life.
        GameSession session = GameSession.CreateNewGame(new FakeInputSource(), playerCount: 1);

        Assert.Equal(3, session.GetCurrent().Lives);
        Assert.Equal(2, session.GetCurrent().GetSpareMen());

        session.GetCurrent().Lives = 1;
        Assert.Equal(0, session.GetCurrent().GetSpareMen());

        session.GetCurrent().Lives = 0;
        Assert.Equal(0, session.GetCurrent().GetSpareMen());
    }

    [Fact]
    public void DisplayedMen_IsCappedAtSeven()
    {
        // ROM MANDSV: "DISPLAY MEN LEFT / MAX OF 7".
        GameSession session = GameSession.CreateNewGame(new FakeInputSource(), playerCount: 1);

        session.GetCurrent().Lives = 9; // 8 spare
        Assert.Equal(8, session.GetCurrent().GetSpareMen());
        Assert.Equal(HudLayout.HudMaxMen, session.GetCurrent().GetDisplayedMen());

        session.GetCurrent().Lives = 4;
        Assert.Equal(3, session.GetCurrent().GetDisplayedMen());
    }

    [Fact]
    public void SwitchToPlayerWithMen_AlternatesWhileBothAreAlive()
    {
        GameSession session = NewTwoPlayerGame();

        Assert.True(session.SwitchToPlayerWithMen());
        Assert.Equal(2, session.GetCurrent().Number);

        Assert.True(session.SwitchToPlayerWithMen());
        Assert.Equal(1, session.GetCurrent().Number);
    }

    [Fact]
    public void SwitchToPlayerWithMen_StaysWhenTheOtherIsOut()
    {
        // ROM PLE1: EORA #3 / PLDX / LDB PLAS,X / BEQ PLE1 — an out player is
        // skipped, and if nobody else has men the turn does not move.
        GameSession session = NewTwoPlayerGame();
        session.Players[1].Lives = 0;

        Assert.False(session.SwitchToPlayerWithMen());
        Assert.Equal(1, session.GetCurrent().Number);
    }

    [Fact]
    public void SwitchToPlayerWithMen_NeverMovesInAOnePlayerGame()
    {
        GameSession session = GameSession.CreateNewGame(new FakeInputSource(), playerCount: 1);

        Assert.False(session.SwitchToPlayerWithMen());
        Assert.Equal(1, session.GetCurrent().Number);
    }

    [Fact]
    public void AnyMenLeft_IsFalseOnlyWhenEverybodyIsOut()
    {
        GameSession session = NewTwoPlayerGame();

        Assert.True(session.AnyPlayerSlotHasMen());

        session.Players[0].Lives = 0;
        Assert.True(session.AnyPlayerSlotHasMen());

        session.Players[1].Lives = 0;
        Assert.False(session.AnyPlayerSlotHasMen());
    }

    [Fact]
    public void ScoresHighestFirst_OrdersBothPlayers()
    {
        GameSession session = NewTwoPlayerGame();
        session.Players[0].Score = 1500;
        session.Players[1].Score = 9800;

        Assert.Equal([9800, 1500], session.GetScoresHighestFirst());
    }

    [Fact]
    public void NewGame_RejectsThreePlayers()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => GameSession.CreateNewGame(new FakeInputSource(), playerCount: 3));
    }

    [Fact]
    public void NewGame_TakesTheStartingLivesFromTurnsPerPlayer()
    {
        var settings = new GameSettings { TurnsPerPlayer = 5 };

        GameSession session = GameSession.CreateNewGame(GameMode.OnePlayer, new FakeInputSource(), settings: settings);

        Assert.Equal(5, session.GetCurrent().Lives);
        Assert.Same(settings, session.GameSettings);
    }

    [Fact]
    public void NewGame_WithNoSettings_UsesTheFactoryOnes()
    {
        GameSession session = GameSession.CreateNewGame(new FakeInputSource(), playerCount: 1);

        Assert.Equal(GameSettings.FactoryTurnsPerPlayer, session.GetCurrent().Lives);
    }
}
