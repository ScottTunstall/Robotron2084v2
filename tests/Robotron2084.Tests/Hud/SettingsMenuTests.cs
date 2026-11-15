using Robotron2084.Hud.Models;
using Robotron2084.Hud;
using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>The GAME ADJUSTMENT page's logic (notes §131): five rows, Left/Right to change, Enter to activate.</summary>
public sealed class SettingsMenuTests
{
    private static void GoTo(SettingsMenu settingsMenu, int line)
    {
        while (settingsMenu.Line != line) settingsMenu.MoveDown();
    }

    [Fact]
    public void TheCursorWrapsRoundThePage()
    {
        var settingsMenu = new SettingsMenu();

        settingsMenu.MoveUp();
        Assert.Equal(SettingsMenu.HighScoreResetLine, settingsMenu.Line);

        settingsMenu.MoveDown();
        Assert.Equal(SettingsMenu.ExtraLifeLine, settingsMenu.Line);
    }

    [Fact]
    public void LeftAndRightStepTheHighlightedValue()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings();

        settingsMenu.Change(gameSettings, 1);
        Assert.Equal(30, gameSettings.ExtraLifeEvery); // 25 -> 30

        settingsMenu.MoveDown();
        settingsMenu.Change(gameSettings, 1);
        Assert.Equal(4, gameSettings.TurnsPerPlayer);

        settingsMenu.MoveDown();
        settingsMenu.Change(gameSettings, -1);
        Assert.Equal(4, gameSettings.Difficulty);
    }

    [Fact]
    public void AnActionRowIsSetToYesByRightAndNoByLeft()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings();
        GoTo(settingsMenu, SettingsMenu.RestoreFactoryLine);

        Assert.False(settingsMenu.IsArmed(settingsMenu.Line));

        settingsMenu.Change(gameSettings, 1);
        Assert.True(settingsMenu.IsArmed(settingsMenu.Line));

        settingsMenu.Change(gameSettings, -1);
        Assert.False(settingsMenu.IsArmed(settingsMenu.Line));
    }

    [Fact]
    public void ActivateDoesNothingOnAValueRow()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings { ExtraLifeEvery = 50, TurnsPerPlayer = 9, Difficulty = 9 };

        Assert.Equal(SettingsAction.None, settingsMenu.Activate(gameSettings));
        Assert.Equal(50, gameSettings.ExtraLifeEvery);
        Assert.Equal(9, gameSettings.Difficulty);
    }

    [Fact]
    public void ActivateDoesNothingOnAnActionRowThatStillReadsNo()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings();
        GoTo(settingsMenu, SettingsMenu.RestoreFactoryLine);

        Assert.Equal(SettingsAction.None, settingsMenu.Activate(gameSettings));
    }

    [Fact]
    public void RestoreFactorySettingsPutsTheValuesBackAndDisarms()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings
            { ExtraLifeEvery = 0, TurnsPerPlayer = 20, Difficulty = 10, AttractModeSoundEnabled = true };
        GoTo(settingsMenu, SettingsMenu.RestoreFactoryLine);
        settingsMenu.Change(gameSettings, 1);

        Assert.Equal(SettingsAction.RestoreFactorySettings, settingsMenu.Activate(gameSettings));
        Assert.Equal(25, gameSettings.ExtraLifeEvery);
        Assert.Equal(3, gameSettings.TurnsPerPlayer);
        Assert.Equal(5, gameSettings.Difficulty);
        Assert.False(gameSettings.AttractModeSoundEnabled);
        Assert.False(settingsMenu.IsArmed(SettingsMenu.RestoreFactoryLine));
    }

    [Fact]
    public void HighScoreTableResetIsItsOwnAction()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings();
        GoTo(settingsMenu, SettingsMenu.HighScoreResetLine);
        settingsMenu.Change(gameSettings, 1);

        Assert.Equal(SettingsAction.ResetHighScores, settingsMenu.Activate(gameSettings));
        Assert.False(settingsMenu.IsArmed(SettingsMenu.HighScoreResetLine));
    }

    [Fact]
    public void LeftAndRightTurnTheAttractSoundOffAndOn()
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings();
        GoTo(settingsMenu, SettingsMenu.AttractSoundLine);

        Assert.Equal("OFF", settingsMenu.GetValue(gameSettings, SettingsMenu.AttractSoundLine));

        settingsMenu.Change(gameSettings, 1);
        Assert.True(gameSettings.AttractModeSoundEnabled);
        Assert.Equal("ON", settingsMenu.GetValue(gameSettings, SettingsMenu.AttractSoundLine));

        settingsMenu.Change(gameSettings, -1);
        Assert.False(gameSettings.AttractModeSoundEnabled);
    }

    [Theory]
    [InlineData(SettingsMenu.TankShellBugLine)]
    [InlineData(SettingsMenu.BrainsChaseMikeyBugLine)]
    [InlineData(SettingsMenu.BozoModeLine)]
    public void TheBugAndBozoRowsAreOnFromTheFactory_AndLeftTurnsThemOff(int line)
    {
        var settingsMenu = new SettingsMenu();
        var gameSettings = new GameSettings();
        GoTo(settingsMenu, line);

        Assert.Equal("ON", settingsMenu.GetValue(gameSettings, line));
        Assert.Equal("RECOMMENDED", SettingsMenu.GetNote(gameSettings, line));

        settingsMenu.Change(gameSettings, -1);
        Assert.Equal("OFF", settingsMenu.GetValue(gameSettings, line));
        Assert.Equal(string.Empty, SettingsMenu.GetNote(gameSettings, line));

        // Only the highlighted row changed.
        var off = new[] { gameSettings.TankShellBugEnabled, gameSettings.BrainsChaseMikeyBugEnabled, gameSettings.BozoModeEnabled }
            .Count(on => !on);
        Assert.Equal(1, off);

        settingsMenu.Change(gameSettings, 1);
        Assert.Equal("ON", settingsMenu.GetValue(gameSettings, line));
    }

    [Theory]
    [InlineData(0, "0", "NO EXTRA MEN")]
    [InlineData(25, "25000", "RECOMMENDED")]
    [InlineData(50, "50000", "EXTRA CONSERVATIVE")]
    public void TheExtraLifeRowShowsTheArcadesValueAndWord(int value, string text, string note)
    {
        var gameSettings = new GameSettings { ExtraLifeEvery = value };
        var settingsMenu = new SettingsMenu();

        Assert.Equal(text, settingsMenu.GetValue(gameSettings, SettingsMenu.ExtraLifeLine));
        Assert.Equal(note, SettingsMenu.GetNote(gameSettings, SettingsMenu.ExtraLifeLine));
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
            SettingsMenu.GetNote(new GameSettings { Difficulty = value }, SettingsMenu.DifficultyLine));
    }

    [Fact]
    public void TheActionRowsReadNoUntilTheyAreSet()
    {
        var gameSettings = new GameSettings();
        var settingsMenu = new SettingsMenu();

        Assert.Equal("NO", settingsMenu.GetValue(gameSettings, SettingsMenu.RestoreFactoryLine));
        Assert.Equal(string.Empty, settingsMenu.GetActionHint(SettingsMenu.RestoreFactoryLine));

        GoTo(settingsMenu, SettingsMenu.RestoreFactoryLine);
        settingsMenu.Change(gameSettings, 1);

        Assert.Equal("YES", settingsMenu.GetValue(gameSettings, SettingsMenu.RestoreFactoryLine));
        Assert.Equal("PRESS ENTER", settingsMenu.GetActionHint(SettingsMenu.RestoreFactoryLine));
    }
}
