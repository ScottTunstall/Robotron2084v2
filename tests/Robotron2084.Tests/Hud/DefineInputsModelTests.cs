using Microsoft.Xna.Framework.Input;
using Robotron2084.Hud;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
///     The DEFINE INPUTS page's logic (notes §101): one column — player 1's eight stick
///     lines, blank lines, player 2's eight, blank lines, then PAUSE — of which eight are on
///     screen at a time, and the author's two-step flow: Enter arms a line, the next input
///     becomes its binding, and the highlight moves on (stepping over the blanks).
/// </summary>
public sealed class DefineInputsModelTests
{
    [Fact]
    public void ItStartsOnPlayerOnesFirstLine_WithNothingArmed()
    {
        var defineInputsModel = new DefineInputsModel();

        Assert.Equal(0, defineInputsModel.Line);
        Assert.Equal(0, defineInputsModel.FirstVisibleLine);
        Assert.False(defineInputsModel.IsArmed);
        Assert.Equal(InputAction.MoveUp, defineInputsModel.GetHighlightedAction());
    }

    [Fact]
    public void ThePageIsTwentyOneLines_WithGapsBetweenTheSections()
    {
        Assert.Equal(8, InputActions.All.Length);
        Assert.Equal(8, DefineInputsModel.LinesPerPlayer);
        Assert.Equal(2, DefineInputsModel.SpacerLines);
        Assert.Equal(10, DefineInputsModel.PlayerTwoLine);
        Assert.Equal(20, DefineInputsModel.PauseLine);
        Assert.Equal(21, DefineInputsModel.LineCount);

        Assert.Equal(0, DefineInputsModel.PlayerOf(0));
        Assert.Equal(1, DefineInputsModel.PlayerOf(15));
        Assert.Equal(InputAction.MoveUp, DefineInputsModel.GetAction(0));
        Assert.Equal(InputAction.ShootRight, DefineInputsModel.GetAction(7));
        Assert.True(DefineInputsModel.IsSpacer(8)); // the blank lines between player 1 and player 2
        Assert.True(DefineInputsModel.IsSpacer(9));
        Assert.Equal(InputAction.MoveUp, DefineInputsModel.GetAction(DefineInputsModel.PlayerTwoLine));
        Assert.Equal(InputAction.ShootRight, DefineInputsModel.GetAction(17));
        Assert.True(DefineInputsModel.IsSpacer(18)); // and before PAUSE
        Assert.True(DefineInputsModel.IsSpacer(19));
        Assert.False(DefineInputsModel.IsSpacer(DefineInputsModel.PauseLine));
        Assert.Null(DefineInputsModel.GetAction(DefineInputsModel.PauseLine));
    }

    [Fact]
    public void TheCursorStepsOverTheBlankSpacers_NeverLandingOnOne()
    {
        var defineInputsModel = new DefineInputsModel();

        for (var i = 0; i < DefineInputsModel.LinesPerPlayer; i++) defineInputsModel.MoveDown();

        // Seven steps through player 1, then straight onto player 2's first line.
        Assert.Equal(DefineInputsModel.PlayerTwoLine, defineInputsModel.Line);
        Assert.Equal(InputAction.MoveUp, defineInputsModel.GetHighlightedAction());

        for (var i = 0; i < DefineInputsModel.LinesPerPlayer; i++) defineInputsModel.MoveDown();

        Assert.True(defineInputsModel.IsPauseLine()); // the gap before PAUSE is stepped over too

        defineInputsModel.MoveDown();
        Assert.Equal(0, defineInputsModel.Line); // and the wrap round the page

        defineInputsModel.MoveUp();
        Assert.True(defineInputsModel.IsPauseLine()); // back up over the gap
    }

    [Fact]
    public void ScrollingDown_WalksThroughPlayerTwo_AndOntoPause()
    {
        var defineInputsModel = new DefineInputsModel();

        for (var i = 0; i < DefineInputsModel.LinesPerPlayer - 1; i++) defineInputsModel.MoveDown();

        Assert.Equal(InputAction.ShootRight, defineInputsModel.GetHighlightedAction());
        Assert.Equal(0, DefineInputsModel.PlayerOf(defineInputsModel.Line));

        defineInputsModel.MoveDown();
        Assert.Equal(InputAction.MoveUp, defineInputsModel.GetHighlightedAction());
        Assert.Equal(1, DefineInputsModel.PlayerOf(defineInputsModel.Line)); // player 2's section

        for (var i = 0; i < DefineInputsModel.LinesPerPlayer; i++) defineInputsModel.MoveDown();

        Assert.True(defineInputsModel.IsPauseLine());

        defineInputsModel.MoveDown();
        Assert.Equal(0, defineInputsModel.Line); // and back round to the top
    }

    [Fact]
    public void ScrollingUp_FromTheFirstLine_LandsOnPause()
    {
        var defineInputsModel = new DefineInputsModel();

        defineInputsModel.MoveUp();

        Assert.True(defineInputsModel.IsPauseLine());
        Assert.Equal(DefineInputsModel.PauseLine, defineInputsModel.Line);
    }

    [Fact]
    public void TheWindowFollowsTheCursor_AndStaysOnScreen()
    {
        var defineInputsModel = new DefineInputsModel();

        for (var line = 1; line < DefineInputsModel.LineCount; line++)
        {
            defineInputsModel.MoveDown();

            Assert.True(defineInputsModel.FirstVisibleLine <= defineInputsModel.Line);
            Assert.True(defineInputsModel.Line < defineInputsModel.FirstVisibleLine + DefineInputsModel.VisibleLines);
            Assert.True(defineInputsModel.FirstVisibleLine >= 0);
            Assert.True(defineInputsModel.FirstVisibleLine + DefineInputsModel.VisibleLines <= DefineInputsModel.LineCount);
        }

        for (var line = DefineInputsModel.LineCount - 2; line >= 0; line--)
        {
            defineInputsModel.MoveUp();

            Assert.True(defineInputsModel.FirstVisibleLine <= defineInputsModel.Line);
            Assert.True(defineInputsModel.Line < defineInputsModel.FirstVisibleLine + DefineInputsModel.VisibleLines);
        }
    }

    [Fact]
    public void ScrollingToPlayerTwo_ScrollsTheWindowDown()
    {
        var defineInputsModel = new DefineInputsModel();

        for (var i = 0; i < DefineInputsModel.LinesPerPlayer; i++) defineInputsModel.MoveDown();

        Assert.Equal(DefineInputsModel.PlayerTwoLine, defineInputsModel.Line);
        Assert.True(defineInputsModel.FirstVisibleLine > 0); // the P1 section has moved up
        Assert.True(defineInputsModel.FirstVisibleLine <= DefineInputsModel.PlayerTwoLine); // and the cursor is in the window
    }

    [Fact]
    public void NothingIsCapturedUntilEnterArmsTheLine()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        Assert.False(defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Z)));
        Assert.Equal("W", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
    }

    [Fact]
    public void AssigningArmsThenTakesTheInput_AndMovesOn()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsModel.Arm();
        Assert.True(defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Z)));

        Assert.Equal("Z", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("P1 LEFT STICK UP",
            controlSettings[0][InputAction.MoveUp].PadBinding.GetDisplayName()); // the pad slot is untouched
        Assert.Equal(1, defineInputsModel.Line); // straight on to MOVE DOWN
        Assert.False(defineInputsModel.IsArmed);
    }

    [Fact]
    public void PlayerTwosLinesWritePlayerTwosControls()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        for (var i = 0; i < DefineInputsModel.LinesPerPlayer; i++) defineInputsModel.MoveDown();

        defineInputsModel.Arm();
        defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Z));

        Assert.Equal("Z", controlSettings[1][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("W", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName()); // player 1 is untouched
    }

    [Fact]
    public void ThePauseLine_TakesAGlobalBinding()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsModel.MoveUp(); // onto PAUSE
        defineInputsModel.Arm();
        defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Escape));

        Assert.Equal("ESCAPE", controlSettings.Pause.GetDisplayName());
        Assert.Equal(DefineInputsModel.PauseLine, defineInputsModel.Line); // there is nowhere to advance to
    }

    [Fact]
    public void BackWhileArmed_DisarmsWithoutAssigning()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsModel.Arm();
        defineInputsModel.CancelArm();

        Assert.False(defineInputsModel.IsArmed);
        Assert.False(defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Z)));
    }

    [Fact]
    public void ScrollingIsDeadWhileArmed_SoThatEveryKeyIsCapturable()
    {
        var defineInputsModel = new DefineInputsModel();
        defineInputsModel.Arm();

        defineInputsModel.MoveDown();
        defineInputsModel.MoveUp();

        Assert.Equal(0, defineInputsModel.Line);
    }

    [Fact]
    public void ClearUnbindsBothSlotsOfTheHighlightedLine()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsModel.ClearHighlighted(controlSettings);

        Assert.Equal("NONE", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("NONE", controlSettings[0][InputAction.MoveUp].PadBinding.GetDisplayName());
        Assert.Equal("D",
            controlSettings[0][InputAction.MoveRight].KeyBinding.GetDisplayName()); // its neighbours are untouched
    }

    [Fact]
    public void ResetPutsEveryLineAndPauseBackToFactory()
    {
        var defineInputsModel = new DefineInputsModel();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsModel.Arm();
        defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Z));
        defineInputsModel.MoveUp();
        defineInputsModel.Arm();
        defineInputsModel.Assign(controlSettings, InputBinding.CreateKey(Keys.Escape));

        defineInputsModel.ResetAll(controlSettings);

        Assert.Equal("W", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("NUMPAD8", controlSettings[1][InputAction.ShootUp].KeyBinding.GetDisplayName());
        Assert.Equal("P", controlSettings.Pause.GetDisplayName());
    }

    [Fact]
    public void IsCursorOn_TracksTheLineAndSuppressesItselfWhileArmed()
    {
        var defineInputsModel = new DefineInputsModel();

        Assert.True(defineInputsModel.IsCursorOn(0));
        Assert.False(defineInputsModel.IsCursorOn(1));

        defineInputsModel.Arm();
        Assert.False(defineInputsModel.IsCursorOn(0));
    }
}
