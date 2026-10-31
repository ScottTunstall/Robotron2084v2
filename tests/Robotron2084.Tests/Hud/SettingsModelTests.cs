using Robotron2084.Hud;
using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>The GAME ADJUSTMENT page's logic (notes §131): five rows, Left/Right to change, Enter to activate.</summary>
public sealed class SettingsModelTests
{
    private static void GoTo(SettingsModel settingsModel, int line)
    {
        while (settingsModel.Line != line) settingsModel.MoveDown();
    }

    [Fact]
    public void TheCursorWrapsRoundThePage()
    {
        var settingsModel = new SettingsModel();

        settingsModel.MoveUp();
        Assert.Equal(SettingsModel.HighScoreResetLine, settingsModel.Line);

        settingsModel.MoveDown();
        Assert.Equal(SettingsModel.ExtraLifeLine, settingsModel.Line);
    }

    [Fact]
    public void LeftAndRightStepTheHighlightedValue()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings();

        settingsModel.Change(gameSettings, 1);
        Assert.Equal(30, gameSettings.ExtraLifeEvery); // 25 -> 30

        settingsModel.MoveDown();
        settingsModel.Change(gameSettings, 1);
        Assert.Equal(4, gameSettings.TurnsPerPlayer);

        settingsModel.MoveDown();
        settingsModel.Change(gameSettings, -1);
        Assert.Equal(4, gameSettings.Difficulty);
    }

    [Fact]
    public void AnActionRowIsSetToYesByRightAndNoByLeft()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings();
        GoTo(settingsModel, SettingsModel.RestoreFactoryLine);

        Assert.False(settingsModel.IsArmed(settingsModel.Line));

        settingsModel.Change(gameSettings, 1);
        Assert.True(settingsModel.IsArmed(settingsModel.Line));

        settingsModel.Change(gameSettings, -1);
        Assert.False(settingsModel.IsArmed(settingsModel.Line));
    }

    [Fact]
    public void ActivateDoesNothingOnAValueRow()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings { ExtraLifeEvery = 50, TurnsPerPlayer = 9, Difficulty = 9 };

        Assert.Equal(SettingsAction.None, settingsModel.Activate(gameSettings));
        Assert.Equal(50, gameSettings.ExtraLifeEvery);
        Assert.Equal(9, gameSettings.Difficulty);
    }

    [Fact]
    public void ActivateDoesNothingOnAnActionRowThatStillReadsNo()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings();
        GoTo(settingsModel, SettingsModel.RestoreFactoryLine);

        Assert.Equal(SettingsAction.None, settingsModel.Activate(gameSettings));
    }

    [Fact]
    public void RestoreFactorySettingsPutsTheValuesBackAndDisarms()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings
            { ExtraLifeEvery = 0, TurnsPerPlayer = 20, Difficulty = 10, AttractModeSoundEnabled = true };
        GoTo(settingsModel, SettingsModel.RestoreFactoryLine);
        settingsModel.Change(gameSettings, 1);

        Assert.Equal(SettingsAction.RestoreFactorySettings, settingsModel.Activate(gameSettings));
        Assert.Equal(25, gameSettings.ExtraLifeEvery);
        Assert.Equal(3, gameSettings.TurnsPerPlayer);
        Assert.Equal(5, gameSettings.Difficulty);
        Assert.False(gameSettings.AttractModeSoundEnabled);
        Assert.False(settingsModel.IsArmed(SettingsModel.RestoreFactoryLine));
    }

    [Fact]
    public void HighScoreTableResetIsItsOwnAction()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings();
        GoTo(settingsModel, SettingsModel.HighScoreResetLine);
        settingsModel.Change(gameSettings, 1);

        Assert.Equal(SettingsAction.ResetHighScores, settingsModel.Activate(gameSettings));
        Assert.False(settingsModel.IsArmed(SettingsModel.HighScoreResetLine));
    }

    [Fact]
    public void LeftAndRightTurnTheAttractSoundOffAndOn()
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings();
        GoTo(settingsModel, SettingsModel.AttractSoundLine);

        Assert.Equal("OFF", settingsModel.GetValue(gameSettings, SettingsModel.AttractSoundLine));

        settingsModel.Change(gameSettings, 1);
        Assert.True(gameSettings.AttractModeSoundEnabled);
        Assert.Equal("ON", settingsModel.GetValue(gameSettings, SettingsModel.AttractSoundLine));

        settingsModel.Change(gameSettings, -1);
        Assert.False(gameSettings.AttractModeSoundEnabled);
    }

    [Theory]
    [InlineData(SettingsModel.TankShellBugLine)]
    [InlineData(SettingsModel.BrainsChaseMikeyBugLine)]
    [InlineData(SettingsModel.BozoModeLine)]
    public void TheBugAndBozoRowsAreOnFromTheFactory_AndLeftTurnsThemOff(int line)
    {
        var settingsModel = new SettingsModel();
        var gameSettings = new GameSettings();
        GoTo(settingsModel, line);

        Assert.Equal("ON", settingsModel.GetValue(gameSettings, line));
        Assert.Equal("RECOMMENDED", SettingsModel.GetNote(gameSettings, line));

        settingsModel.Change(gameSettings, -1);
        Assert.Equal("OFF", settingsModel.GetValue(gameSettings, line));
        Assert.Equal(string.Empty, SettingsModel.GetNote(gameSettings, line));

        // Only the highlighted row changed.
        var off = new[] { gameSettings.TankShellBugEnabled, gameSettings.BrainsChaseMikeyBugEnabled, gameSettings.BozoModeEnabled }
            .Count(on => !on);
        Assert.Equal(1, off);

        settingsModel.Change(gameSettings, 1);
        Assert.Equal("ON", settingsModel.GetValue(gameSettings, line));
    }

    [Theory]
    [InlineData(0, "0", "NO EXTRA MEN")]
    [InlineData(25, "25000", "RECOMMENDED")]
    [InlineData(50, "50000", "EXTRA CONSERVATIVE")]
    public void TheExtraLifeRowShowsTheArcadesValueAndWord(int value, string text, string note)
    {
        var gameSettings = new GameSettings { ExtraLifeEvery = value };
        var settingsModel = new SettingsModel();

        Assert.Equal(text, settingsModel.GetValue(gameSettings, SettingsModel.ExtraLifeLine));
        Assert.Equal(note, SettingsModel.GetNote(gameSettings, SettingsModel.ExtraLifeLine));
    }

    [Theory]
    [InlineData(0, "EXTRA LIBERAL")]
    [InlineData(3, "LIBERAL")]
    [InlineData(5, "RECOMMENDED")]
    [InlineData(7, "CONSERVATIVE")]
    [InlineData(10, "EXTRA CONSERVATIVE")]
    public void TheDifficultyRowUsesTheRomsWordBands(int value, string note)
    {
        Assert.Equal(note,
            SettingsModel.GetNote(new GameSettings { Difficulty = value }, SettingsModel.DifficultyLine));
    }

    [Fact]
    public void TheActionRowsReadNoUntilTheyAreSet()
    {
        var gameSettings = new GameSettings();
        var settingsModel = new SettingsModel();

        Assert.Equal("NO", settingsModel.GetValue(gameSettings, SettingsModel.RestoreFactoryLine));
        Assert.Equal(string.Empty, settingsModel.GetActionHint(SettingsModel.RestoreFactoryLine));

        GoTo(settingsModel, SettingsModel.RestoreFactoryLine);
        settingsModel.Change(gameSettings, 1);

        Assert.Equal("YES", settingsModel.GetValue(gameSettings, SettingsModel.RestoreFactoryLine));
        Assert.Equal("PRESS ENTER", settingsModel.GetActionHint(SettingsModel.RestoreFactoryLine));
    }
}
