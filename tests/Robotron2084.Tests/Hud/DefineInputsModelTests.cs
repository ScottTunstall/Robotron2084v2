using Microsoft.Xna.Framework.Input;
using Robotron2084.Hud;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// The DEFINE INPUTS page's logic (notes §101): one column — player 1's eight stick
/// lines, then player 2's, then PAUSE — of which eight are on screen at a time, and the
/// author's two-step flow: Enter arms a line, the next input becomes its binding, and
/// the highlight moves on.
/// </summary>
public sealed class DefineInputsModelTests
{
    [Fact]
    public void ItStartsOnPlayerOnesFirstLine_WithNothingArmed()
    {
        var model = new DefineInputsModel();

        Assert.Equal(0, model.Line);
        Assert.Equal(0, model.FirstVisibleLine);
        Assert.False(model.IsArmed);
        Assert.Equal(InputAction.MoveUp, model.HighlightedAction);
    }

    [Fact]
    public void ThePageIsSeventeenLines_BothPlayersThenPause()
    {
        Assert.Equal(8, InputActions.All.Length);
        Assert.Equal(8, DefineInputsModel.LinesPerPlayer);
        Assert.Equal(16, DefineInputsModel.PauseLine);
        Assert.Equal(17, DefineInputsModel.LineCount);

        Assert.Equal(0, DefineInputsModel.PlayerOf(0));
        Assert.Equal(1, DefineInputsModel.PlayerOf(15));
        Assert.Equal(InputAction.MoveUp, DefineInputsModel.ActionOf(0));
        Assert.Equal(InputAction.ShootRight, DefineInputsModel.ActionOf(7));
        Assert.Equal(InputAction.MoveUp, DefineInputsModel.ActionOf(8)); // player 2's section starts here
        Assert.Null(DefineInputsModel.ActionOf(DefineInputsModel.PauseLine));
    }

    [Fact]
    public void ScrollingDown_WalksThroughPlayerTwo_AndOntoPause()
    {
        var model = new DefineInputsModel();

        for (int i = 0; i < DefineInputsModel.LinesPerPlayer - 1; i++)
        {
            model.MoveDown();
        }

        Assert.Equal(InputAction.ShootRight, model.HighlightedAction);
        Assert.Equal(0, DefineInputsModel.PlayerOf(model.Line));

        model.MoveDown();
        Assert.Equal(InputAction.MoveUp, model.HighlightedAction);
        Assert.Equal(1, DefineInputsModel.PlayerOf(model.Line)); // player 2's section

        for (int i = 0; i < DefineInputsModel.LinesPerPlayer; i++)
        {
            model.MoveDown();
        }

        Assert.True(model.IsPauseLine);

        model.MoveDown();
        Assert.Equal(0, model.Line); // and back round to the top
    }

    [Fact]
    public void ScrollingUp_FromTheFirstLine_LandsOnPause()
    {
        var model = new DefineInputsModel();

        model.MoveUp();

        Assert.True(model.IsPauseLine);
        Assert.Equal(DefineInputsModel.PauseLine, model.Line);
    }

    [Fact]
    public void TheWindowFollowsTheCursor_AndStaysOnScreen()
    {
        var model = new DefineInputsModel();

        for (int line = 1; line < DefineInputsModel.LineCount; line++)
        {
            model.MoveDown();

            Assert.True(model.FirstVisibleLine <= model.Line);
            Assert.True(model.Line < model.FirstVisibleLine + DefineInputsModel.VisibleLines);
            Assert.True(model.FirstVisibleLine >= 0);
            Assert.True(model.FirstVisibleLine + DefineInputsModel.VisibleLines <= DefineInputsModel.LineCount);
        }

        for (int line = DefineInputsModel.LineCount - 2; line >= 0; line--)
        {
            model.MoveUp();

            Assert.True(model.FirstVisibleLine <= model.Line);
            Assert.True(model.Line < model.FirstVisibleLine + DefineInputsModel.VisibleLines);
        }
    }

    [Fact]
    public void ScrollingToPlayerTwo_ScrollsTheWindowDown()
    {
        var model = new DefineInputsModel();

        for (int i = 0; i < DefineInputsModel.LinesPerPlayer; i++)
        {
            model.MoveDown();
        }

        Assert.Equal(DefineInputsModel.LinesPerPlayer, model.Line);
        Assert.True(model.FirstVisibleLine > 0);                                  // the P1 section has moved up
        Assert.True(model.FirstVisibleLine <= DefineInputsModel.LinesPerPlayer);  // and the cursor is in the window
    }

    [Fact]
    public void NothingIsCapturedUntilEnterArmsTheLine()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        Assert.False(model.Assign(settings, InputBinding.Key(Keys.Z)));
        Assert.Equal("W", settings[0][InputAction.MoveUp].Key.DisplayName);
    }

    [Fact]
    public void AssigningArmsThenTakesTheInput_AndMovesOn()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        model.Arm();
        Assert.True(model.Assign(settings, InputBinding.Key(Keys.Z)));

        Assert.Equal("Z", settings[0][InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("P1 LEFT STICK UP", settings[0][InputAction.MoveUp].Pad.DisplayName); // the pad slot is untouched
        Assert.Equal(1, model.Line);  // straight on to MOVE DOWN
        Assert.False(model.IsArmed);
    }

    [Fact]
    public void PlayerTwosLinesWritePlayerTwosControls()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        for (int i = 0; i < DefineInputsModel.LinesPerPlayer; i++)
        {
            model.MoveDown();
        }

        model.Arm();
        model.Assign(settings, InputBinding.Key(Keys.Z));

        Assert.Equal("Z", settings[1][InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("W", settings[0][InputAction.MoveUp].Key.DisplayName); // player 1 is untouched
    }

    [Fact]
    public void ThePauseLine_TakesAGlobalBinding()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        model.MoveUp(); // onto PAUSE
        model.Arm();
        model.Assign(settings, InputBinding.Key(Keys.Escape));

        Assert.Equal("ESCAPE", settings.Pause.DisplayName);
        Assert.Equal(DefineInputsModel.PauseLine, model.Line); // there is nowhere to advance to
    }

    [Fact]
    public void BackWhileArmed_DisarmsWithoutAssigning()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        model.Arm();
        model.CancelArm();

        Assert.False(model.IsArmed);
        Assert.False(model.Assign(settings, InputBinding.Key(Keys.Z)));
    }

    [Fact]
    public void ScrollingIsDeadWhileArmed_SoThatEveryKeyIsCapturable()
    {
        var model = new DefineInputsModel();
        model.Arm();

        model.MoveDown();
        model.MoveUp();

        Assert.Equal(0, model.Line);
    }

    [Fact]
    public void ClearUnbindsBothSlotsOfTheHighlightedLine()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        model.ClearHighlighted(settings);

        Assert.Equal("NONE", settings[0][InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("NONE", settings[0][InputAction.MoveUp].Pad.DisplayName);
        Assert.Equal("D", settings[0][InputAction.MoveRight].Key.DisplayName); // its neighbours are untouched
    }

    [Fact]
    public void ResetPutsEveryLineAndPauseBackToFactory()
    {
        var model = new DefineInputsModel();
        ControlSettings settings = ControlSettings.Defaults();

        model.Arm();
        model.Assign(settings, InputBinding.Key(Keys.Z));
        model.MoveUp();
        model.Arm();
        model.Assign(settings, InputBinding.Key(Keys.Escape));

        model.ResetAll(settings);

        Assert.Equal("W", settings[0][InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("NUMPAD8", settings[1][InputAction.ShootUp].Key.DisplayName);
        Assert.Equal("P", settings.Pause.DisplayName);
    }

    [Fact]
    public void IsCursorOn_TracksTheLineAndSuppressesItselfWhileArmed()
    {
        var model = new DefineInputsModel();

        Assert.True(model.IsCursorOn(0));
        Assert.False(model.IsCursorOn(1));

        model.Arm();
        Assert.False(model.IsCursorOn(0));
    }
}
