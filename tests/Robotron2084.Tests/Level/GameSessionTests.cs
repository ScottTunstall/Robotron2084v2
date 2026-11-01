using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     The arcade's alternating two-player session (notes §59): each player owns
///     their score, lives and wave (ROM PLDATA/ZP2SCR), and a death hands the turn to
///     the other player while they still have lives (ROM PLE1B).
/// </summary>
public class GameSessionTests
{
    private static GameSession NewTwoPlayerGame()
    {
        return GameSession.CreateNewGame(new FakeInputSource(), 2);
    }

    [Fact]
    public void OnePlayerGame_HasOneSlot_NamedPlayerOne()
    {
        var gameSession = GameSession.CreateNewGame(new FakeInputSource(), 1);

        Assert.False(gameSession.IsTwoPlayer());
        Assert.Single(gameSession.PlayerSlots);
        Assert.Equal(1, gameSession.GetCurrent().Number);
        Assert.Equal(PlayerTuning.StartingLives, gameSession.GetCurrent().Lives);
        Assert.Equal(PlayerTuning.StartingWaveNumber, gameSession.GetCurrent().Wave);
    }

    [Fact]
    public void TwoPlayerGame_StartsWithPlayerOne_AndBothHaveTheirOwnState()
    {
        var gameSession = NewTwoPlayerGame();

        Assert.True(gameSession.IsTwoPlayer());
        Assert.Equal(2, gameSession.PlayerSlots.Count);
        Assert.Equal(1, gameSession.GetCurrent().Number);

        gameSession.PlayerSlots[1].Score = 4200;
        gameSession.PlayerSlots[1].Wave = 7;
        Assert.Equal(0, gameSession.PlayerSlots[0].Score);
        Assert.Equal(PlayerTuning.StartingWaveNumber, gameSession.PlayerSlots[0].Wave);
    }

    [Fact]
    public void SpareMen_ExcludesTheLifeInPlay()
    {
        // The ROM's p1_men is the lives counter AFTER PLSTRT decremented it: with
        // 3 lives the HUD shows 2 icons during the first life.
        var gameSession = GameSession.CreateNewGame(new FakeInputSource(), 1);

        Assert.Equal(3, gameSession.GetCurrent().Lives);
        Assert.Equal(2, gameSession.GetCurrent().GetSpareLives());

        gameSession.GetCurrent().Lives = 1;
        Assert.Equal(0, gameSession.GetCurrent().GetSpareLives());

        gameSession.GetCurrent().Lives = 0;
        Assert.Equal(0, gameSession.GetCurrent().GetSpareLives());
    }

    [Fact]
    public void DisplayedMen_IsCappedAtSeven()
    {
        // ROM MANDSV: "DISPLAY MEN LEFT / MAX OF 7".
        var gameSession = GameSession.CreateNewGame(new FakeInputSource(), 1);

        gameSession.GetCurrent().Lives = 9; // 8 spare
        Assert.Equal(8, gameSession.GetCurrent().GetSpareLives());
        Assert.Equal(HudLayout.HudMaxSpareLives, gameSession.GetCurrent().GetDisplayedSpareLives());

        gameSession.GetCurrent().Lives = 4;
        Assert.Equal(3, gameSession.GetCurrent().GetDisplayedSpareLives());
    }

    [Fact]
    public void SwitchToPlayerWithMen_AlternatesWhileBothAreAlive()
    {
        var gameSession = NewTwoPlayerGame();

        Assert.True(gameSession.SwitchToPlayerWithLives());
        Assert.Equal(2, gameSession.GetCurrent().Number);

        Assert.True(gameSession.SwitchToPlayerWithLives());
        Assert.Equal(1, gameSession.GetCurrent().Number);
    }

    [Fact]
    public void SwitchToPlayerWithMen_StaysWhenTheOtherIsOut()
    {
        // ROM PLE1: EORA #3 / PLDX / LDB PLAS,X / BEQ PLE1 — an out player is
        // skipped, and if nobody else has lives the turn does not move.
        var gameSession = NewTwoPlayerGame();
        gameSession.PlayerSlots[1].Lives = 0;

        Assert.False(gameSession.SwitchToPlayerWithLives());
        Assert.Equal(1, gameSession.GetCurrent().Number);
    }

    [Fact]
    public void SwitchToPlayerWithMen_NeverMovesInAOnePlayerGame()
    {
        var gameSession = GameSession.CreateNewGame(new FakeInputSource(), 1);

        Assert.False(gameSession.SwitchToPlayerWithLives());
        Assert.Equal(1, gameSession.GetCurrent().Number);
    }

    [Fact]
    public void AnyMenLeft_IsFalseOnlyWhenEverybodyIsOut()
    {
        var gameSession = NewTwoPlayerGame();

        Assert.True(gameSession.AnyPlayerSlotHasLives());

        gameSession.PlayerSlots[0].Lives = 0;
        Assert.True(gameSession.AnyPlayerSlotHasLives());

        gameSession.PlayerSlots[1].Lives = 0;
        Assert.False(gameSession.AnyPlayerSlotHasLives());
    }

    [Fact]
    public void ScoresHighestFirst_OrdersBothPlayers()
    {
        var gameSession = NewTwoPlayerGame();
        gameSession.PlayerSlots[0].Score = 1500;
        gameSession.PlayerSlots[1].Score = 9800;

        Assert.Equal([9800, 1500], gameSession.GetScoresHighestFirst());
    }

    [Fact]
    public void NewGame_RejectsThreePlayers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameSession.CreateNewGame(new FakeInputSource(), 3));
    }

    [Fact]
    public void NewGame_TakesTheStartingLivesFromTurnsPerPlayer()
    {
        var gameSettings = new GameSettings { TurnsPerPlayer = 5 };

        var gameSession = GameSession.CreateNewGame(GameMode.OnePlayer, new FakeInputSource(), gameSettings: gameSettings);

        Assert.Equal(5, gameSession.GetCurrent().Lives);
        Assert.Same(gameSettings, gameSession.GameSettings);
    }

    [Fact]
    public void NewGame_WithNoSettings_UsesTheFactoryOnes()
    {
        var gameSession = GameSession.CreateNewGame(new FakeInputSource(), 1);

        Assert.Equal(GameSettings.FactoryTurnsPerPlayer, gameSession.GetCurrent().Lives);
    }
}
