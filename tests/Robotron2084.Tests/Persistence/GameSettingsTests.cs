using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>The GAME ADJUSTMENT settings' values and ranges (notes §131), which are the ROM's own.</summary>
public sealed class GameSettingsTests
{
    [Fact]
    public void FactorySettingsAreTheArcardsRecommendedStops()
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();

        Assert.Equal(25, gameSettings.ExtraLifeEvery);
        Assert.Equal(25_000, gameSettings.GetExtraLifeEveryPoints());
        Assert.Equal(3, gameSettings.TurnsPerPlayer);
        Assert.Equal(GameSettings.RecommendedDifficulty, gameSettings.Difficulty);
        Assert.False(gameSettings.AttractModeSoundEnabled); // the port's demo is silent until the operator turns it on
    }

    [Fact]
    public void ExtraLifeEveryStepsThroughTheRomsFiveStopsAndStopsAtTheEnds()
    {
        var gameSettings = GameSettings.CreateFactoryDefaults(); // 25

        gameSettings.BumpExtraLifeEvery(1);
        Assert.Equal(30, gameSettings.ExtraLifeEvery);

        gameSettings.BumpExtraLifeEvery(1);
        Assert.Equal(50, gameSettings.ExtraLifeEvery);

        gameSettings.BumpExtraLifeEvery(1); // already at the top
        Assert.Equal(50, gameSettings.ExtraLifeEvery);

        for (var i = 0; i < 5; i++) gameSettings.BumpExtraLifeEvery(-1);

        Assert.Equal(0, gameSettings.ExtraLifeEvery);
        Assert.Equal(0, gameSettings.GetExtraLifeEveryPoints());
    }

    [Fact]
    public void ExtraLifeEveryUsesTheItemsOwnValuesWhenTheSettingIsOffTheList()
    {
        var gameSettings = new GameSettings { ExtraLifeEvery = 17 };

        gameSettings.BumpExtraLifeEvery(1);

        Assert.Equal(30, gameSettings.ExtraLifeEvery); // steps on from the factory stop, 25
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void TurnsPerPlayerClampsToTheRomsRange(int turns)
    {
        var gameSettings = new GameSettings { TurnsPerPlayer = turns };

        gameSettings.BumpTurnsPerPlayer(turns == 1 ? -1 : 1);
        Assert.Equal(turns, gameSettings.TurnsPerPlayer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void DifficultyClampsToTheRomsRange(int difficulty)
    {
        var gameSettings = new GameSettings { Difficulty = difficulty };

        gameSettings.BumpDifficulty(difficulty == 0 ? -1 : 1);
        Assert.Equal(difficulty, gameSettings.Difficulty);
    }

    [Fact]
    public void ResetToFactoryRestoresEverySetting()
    {
        var gameSettings = new GameSettings
        {
            ExtraLifeEvery = 0,
            TurnsPerPlayer = 20,
            Difficulty = 10,
            AttractModeSoundEnabled = true,
            TankShellBugEnabled = false,
            BrainsChaseMikeyBugEnabled = false,
            BozoModeEnabled = false
        };

        gameSettings.ResetToFactory();

        Assert.Equal(25, gameSettings.ExtraLifeEvery);
        Assert.Equal(3, gameSettings.TurnsPerPlayer);
        Assert.Equal(5, gameSettings.Difficulty);
        Assert.False(gameSettings.AttractModeSoundEnabled);
        Assert.True(gameSettings.TankShellBugEnabled);
        Assert.True(gameSettings.BrainsChaseMikeyBugEnabled);
        Assert.True(gameSettings.BozoModeEnabled);
    }

    [Fact]
    public void AttractModeSoundIsTurnedOffByLeftAndOnByRight()
    {
        var gameSettings = new GameSettings();

        gameSettings.BumpAttractModeSound(-1);
        Assert.False(gameSettings.AttractModeSoundEnabled);

        gameSettings.BumpAttractModeSound(1);
        Assert.True(gameSettings.AttractModeSoundEnabled);
    }

    [Theory]
    [InlineData(true, false, false)] // attract sound on: the attract keeps its sound
    [InlineData(false, false, false)] // ...and a real game is never silenced
    [InlineData(true, true, true)] // attract sound off + an attract screen: silence
    [InlineData(false, true, false)] // ...but a real game still has sound
    public void OnlyAnAttractScreenWithTheSoundSettingOffIsSilent(bool attractShowing, bool soundOff, bool expected)
    {
        var gameSettings = new GameSettings { AttractModeSoundEnabled = !soundOff };

        Assert.Equal(expected, gameSettings.IsAttractSilent(attractShowing));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(20, true)]
    [InlineData(25, true)]
    [InlineData(30, true)]
    [InlineData(50, true)]
    [InlineData(10, false)]
    [InlineData(17, false)]
    [InlineData(100, false)]
    public void OnlyTheRomsFiveStopsAreValidExtraLifeValues(int value, bool expected)
    {
        Assert.Equal(expected, GameSettings.IsExtraLifeEveryValue(value));
    }
}
