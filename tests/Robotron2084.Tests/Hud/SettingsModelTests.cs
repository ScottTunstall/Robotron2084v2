using Robotron2084.Hud;
using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>The GAME ADJUSTMENT page's logic (notes §131): five rows, Left/Right to change, Enter to activate.</summary>
public sealed class SettingsModelTests
{
    private static void GoTo(SettingsModel model, int line)
    {
        while (model.Line != line)
        {
            model.MoveDown();
        }
    }

    [Fact]
    public void TheCursorWrapsRoundThePage()
    {
        var model = new SettingsModel();

        model.MoveUp();
        Assert.Equal(SettingsModel.HighScoreResetLine, model.Line);

        model.MoveDown();
        Assert.Equal(SettingsModel.ExtraManLine, model.Line);
    }

    [Fact]
    public void LeftAndRightStepTheHighlightedValue()
    {
        var model = new SettingsModel();
        var settings = new GameSettings();

        model.Change(settings, 1);
        Assert.Equal(30, settings.ExtraManEvery); // 25 -> 30

        model.MoveDown();
        model.Change(settings, 1);
        Assert.Equal(4, settings.TurnsPerPlayer);

        model.MoveDown();
        model.Change(settings, -1);
        Assert.Equal(4, settings.Difficulty);
    }

    [Fact]
    public void AnActionRowIsSetToYesByRightAndNoByLeft()
    {
        var model = new SettingsModel();
        var settings = new GameSettings();
        GoTo(model, SettingsModel.RestoreFactoryLine);

        Assert.False(model.IsArmed(model.Line));

        model.Change(settings, 1);
        Assert.True(model.IsArmed(model.Line));

        model.Change(settings, -1);
        Assert.False(model.IsArmed(model.Line));
    }

    [Fact]
    public void ActivateDoesNothingOnAValueRow()
    {
        var model = new SettingsModel();
        var settings = new GameSettings { ExtraManEvery = 50, TurnsPerPlayer = 9, Difficulty = 9 };

        Assert.Equal(SettingsAction.None, model.Activate(settings));
        Assert.Equal(50, settings.ExtraManEvery);
        Assert.Equal(9, settings.Difficulty);
    }

    [Fact]
    public void ActivateDoesNothingOnAnActionRowThatStillReadsNo()
    {
        var model = new SettingsModel();
        var settings = new GameSettings();
        GoTo(model, SettingsModel.RestoreFactoryLine);

        Assert.Equal(SettingsAction.None, model.Activate(settings));
    }

    [Fact]
    public void RestoreFactorySettingsPutsTheValuesBackAndDisarms()
    {
        var model = new SettingsModel();
        var settings = new GameSettings { ExtraManEvery = 0, TurnsPerPlayer = 20, Difficulty = 10, AttractModeSoundEnabled = true };
        GoTo(model, SettingsModel.RestoreFactoryLine);
        model.Change(settings, 1);

        Assert.Equal(SettingsAction.RestoreFactorySettings, model.Activate(settings));
        Assert.Equal(25, settings.ExtraManEvery);
        Assert.Equal(3, settings.TurnsPerPlayer);
        Assert.Equal(5, settings.Difficulty);
        Assert.False(settings.AttractModeSoundEnabled);
        Assert.False(model.IsArmed(SettingsModel.RestoreFactoryLine));
    }

    [Fact]
    public void HighScoreTableResetIsItsOwnAction()
    {
        var model = new SettingsModel();
        var settings = new GameSettings();
        GoTo(model, SettingsModel.HighScoreResetLine);
        model.Change(settings, 1);

        Assert.Equal(SettingsAction.ResetHighScores, model.Activate(settings));
        Assert.False(model.IsArmed(SettingsModel.HighScoreResetLine));
    }

    [Fact]
    public void LeftAndRightTurnTheAttractSoundOffAndOn()
    {
        var model = new SettingsModel();
        var settings = new GameSettings();
        GoTo(model, SettingsModel.AttractSoundLine);

        Assert.Equal("OFF", model.GetValue(settings, SettingsModel.AttractSoundLine));

        model.Change(settings, 1);
        Assert.True(settings.AttractModeSoundEnabled);
        Assert.Equal("ON", model.GetValue(settings, SettingsModel.AttractSoundLine));

        model.Change(settings, -1);
        Assert.False(settings.AttractModeSoundEnabled);
    }

    [Theory]
    [InlineData(SettingsModel.TankShellBugLine)]
    [InlineData(SettingsModel.BrainsChaseMikeyBugLine)]
    [InlineData(SettingsModel.BozoModeLine)]
    public void TheBugAndBozoRowsAreOnFromTheFactory_AndLeftTurnsThemOff(int line)
    {
        var model = new SettingsModel();
        var settings = new GameSettings();
        GoTo(model, line);

        Assert.Equal("ON", model.GetValue(settings, line));
        Assert.Equal("RECOMMENDED", SettingsModel.GetNote(settings, line));

        model.Change(settings, -1);
        Assert.Equal("OFF", model.GetValue(settings, line));
        Assert.Equal(string.Empty, SettingsModel.GetNote(settings, line));

        // Only the highlighted row changed.
        int off = new[] { settings.TankShellBugEnabled, settings.BrainsChaseMikeyBugEnabled, settings.BozoModeEnabled }.Count(on => !on);
        Assert.Equal(1, off);

        model.Change(settings, 1);
        Assert.Equal("ON", model.GetValue(settings, line));
    }

    [Theory]
    [InlineData(0, "0", "NO EXTRA MEN")]
    [InlineData(25, "25000", "RECOMMENDED")]
    [InlineData(50, "50000", "EXTRA CONSERVATIVE")]
    public void TheExtraManRowShowsTheArcadesValueAndWord(int value, string text, string note)
    {
        var settings = new GameSettings { ExtraManEvery = value };
        var model = new SettingsModel();

        Assert.Equal(text, model.GetValue(settings, SettingsModel.ExtraManLine));
        Assert.Equal(note, SettingsModel.GetNote(settings, SettingsModel.ExtraManLine));
    }

    [Theory]
    [InlineData(0, "EXTRA LIBERAL")]
    [InlineData(3, "LIBERAL")]
    [InlineData(5, "RECOMMENDED")]
    [InlineData(7, "CONSERVATIVE")]
    [InlineData(10, "EXTRA CONSERVATIVE")]
    public void TheDifficultyRowUsesTheRomsWordBands(int value, string note) =>
        Assert.Equal(note, SettingsModel.GetNote(new GameSettings { Difficulty = value }, SettingsModel.DifficultyLine));

    [Fact]
    public void TheActionRowsReadNoUntilTheyAreSet()
    {
        var settings = new GameSettings();
        var model = new SettingsModel();

        Assert.Equal("NO", model.GetValue(settings, SettingsModel.RestoreFactoryLine));
        Assert.Equal(string.Empty, model.GetActionHint(SettingsModel.RestoreFactoryLine));

        GoTo(model, SettingsModel.RestoreFactoryLine);
        model.Change(settings, 1);

        Assert.Equal("YES", model.GetValue(settings, SettingsModel.RestoreFactoryLine));
        Assert.Equal("PRESS ENTER", model.GetActionHint(SettingsModel.RestoreFactoryLine));
    }
}
