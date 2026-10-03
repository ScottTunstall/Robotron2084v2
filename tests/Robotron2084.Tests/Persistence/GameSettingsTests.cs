using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>The GAME ADJUSTMENT settings' values and ranges (notes §131), which are the ROM's own.</summary>
public sealed class GameSettingsTests
{
    [Fact]
    public void FactorySettingsAreTheArcardsRecommendedStops()
    {
        GameSettings settings = GameSettings.CreateFactoryDefaults();

        Assert.Equal(25, settings.ExtraManEvery);
        Assert.Equal(25_000, settings.ExtraManEveryPoints);
        Assert.Equal(3, settings.TurnsPerPlayer);
        Assert.Equal(GameSettings.RecommendedDifficulty, settings.Difficulty);
        Assert.True(settings.AttractModeSound);
    }

    [Fact]
    public void ExtraManEveryStepsThroughTheRomsFiveStopsAndStopsAtTheEnds()
    {
        GameSettings settings = GameSettings.CreateFactoryDefaults(); // 25

        settings.BumpExtraManEvery(1);
        Assert.Equal(30, settings.ExtraManEvery);

        settings.BumpExtraManEvery(1);
        Assert.Equal(50, settings.ExtraManEvery);

        settings.BumpExtraManEvery(1); // already at the top
        Assert.Equal(50, settings.ExtraManEvery);

        for (int i = 0; i < 5; i++)
        {
            settings.BumpExtraManEvery(-1);
        }

        Assert.Equal(0, settings.ExtraManEvery);
        Assert.Equal(0, settings.ExtraManEveryPoints);
    }

    [Fact]
    public void ExtraManEveryUsesTheItemsOwnValuesWhenTheSettingIsOffTheList()
    {
        var settings = new GameSettings { ExtraManEvery = 17 };

        settings.BumpExtraManEvery(1);

        Assert.Equal(30, settings.ExtraManEvery); // steps on from the factory stop, 25
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void TurnsPerPlayerClampsToTheRomsRange(int turns)
    {
        var settings = new GameSettings { TurnsPerPlayer = turns };

        settings.BumpTurnsPerPlayer(turns == 1 ? -1 : 1);
        Assert.Equal(turns, settings.TurnsPerPlayer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void DifficultyClampsToTheRomsRange(int difficulty)
    {
        var settings = new GameSettings { Difficulty = difficulty };

        settings.BumpDifficulty(difficulty == 0 ? -1 : 1);
        Assert.Equal(difficulty, settings.Difficulty);
    }

    [Fact]
    public void ResetToFactoryRestoresEverySetting()
    {
        var settings = new GameSettings { ExtraManEvery = 0, TurnsPerPlayer = 20, Difficulty = 10, AttractModeSound = false };

        settings.ResetToFactory();

        Assert.Equal(25, settings.ExtraManEvery);
        Assert.Equal(3, settings.TurnsPerPlayer);
        Assert.Equal(5, settings.Difficulty);
        Assert.True(settings.AttractModeSound);
    }

    [Fact]
    public void AttractModeSoundIsTurnedOffByLeftAndOnByRight()
    {
        var settings = new GameSettings();

        settings.BumpAttractModeSound(-1);
        Assert.False(settings.AttractModeSound);

        settings.BumpAttractModeSound(1);
        Assert.True(settings.AttractModeSound);
    }

    [Theory]
    [InlineData(true, false, false)]  // attract sound on: the attract keeps its sound
    [InlineData(false, false, false)] // ...and a real game is never silenced
    [InlineData(true, true, true)]    // attract sound off + an attract screen: silence
    [InlineData(false, true, false)]  // ...but a real game still has sound
    public void OnlyAnAttractScreenWithTheSoundSettingOffIsSilent(bool attractShowing, bool soundOff, bool expected)
    {
        var settings = new GameSettings { AttractModeSound = !soundOff };

        Assert.Equal(expected, settings.AttractIsSilent(attractShowing));
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
    public void OnlyTheRomsFiveStopsAreValidExtraManValues(int value, bool expected) =>
        Assert.Equal(expected, GameSettings.IsExtraManEveryValue(value));
}
