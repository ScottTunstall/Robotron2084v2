using Microsoft.Xna.Framework.Input;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Input.Models;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
///     The DEFINE INPUTS page's logic (notes §101): one column — player 1's eight stick
///     lines, blank lines, player 2's eight, blank lines, then PAUSE — of which eight are on
///     screen at a time, and the author's two-step flow: Enter arms a line, the next input
///     becomes its binding, and the highlight moves on (stepping over the blanks).
/// </summary>
public sealed class DefineInputsMenuTests
{
    [Fact]
    public void AssigningArmsThenTakesTheInput_AndMovesOn()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsMenu.Arm();
        Assert.True(defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Z)));

        Assert.Equal("Z", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("P1 LEFT STICK UP",
            controlSettings[0][InputAction.MoveUp].PadBinding.GetDisplayName()); // the pad slot is untouched
        Assert.Equal(1, defineInputsMenu.Line); // straight on to MOVE DOWN
        Assert.False(defineInputsMenu.IsArmed);
    }

    [Fact]
    public void BackWhileArmed_DisarmsWithoutAssigning()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsMenu.Arm();
        defineInputsMenu.CancelArm();

        Assert.False(defineInputsMenu.IsArmed);
        Assert.False(defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Z)));
    }

    [Fact]
    public void ClearUnbindsBothSlotsOfTheHighlightedLine()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsMenu.ClearHighlighted(controlSettings);

        Assert.Equal("NONE", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("NONE", controlSettings[0][InputAction.MoveUp].PadBinding.GetDisplayName());
        Assert.Equal("D",
            controlSettings[0][InputAction.MoveRight].KeyBinding.GetDisplayName()); // its neighbours are untouched
    }

    [Fact]
    public void IsCursorOn_TracksTheLineAndSuppressesItselfWhileArmed()
    {
        var defineInputsMenu = new DefineInputsMenu();

        Assert.True(defineInputsMenu.IsCursorOn(0));
        Assert.False(defineInputsMenu.IsCursorOn(1));

        defineInputsMenu.Arm();
        Assert.False(defineInputsMenu.IsCursorOn(0));
    }

    [Fact]
    public void ItStartsOnPlayerOnesFirstLine_WithNothingArmed()
    {
        var defineInputsMenu = new DefineInputsMenu();

        Assert.Equal(0, defineInputsMenu.Line);
        Assert.Equal(0, defineInputsMenu.FirstVisibleLine);
        Assert.False(defineInputsMenu.IsArmed);
        Assert.Equal(InputAction.MoveUp, defineInputsMenu.GetHighlightedAction());
    }

    [Fact]
    public void NothingIsCapturedUntilEnterArmsTheLine()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        Assert.False(defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Z)));
        Assert.Equal("W", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
    }

    [Fact]
    public void PlayerTwosLinesWritePlayerTwosControls()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        for (var i = 0; i < DefineInputsMenu.LinesPerPlayer; i++) defineInputsMenu.MoveDown();

        defineInputsMenu.Arm();
        defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Z));

        Assert.Equal("Z", controlSettings[1][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("W", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName()); // player 1 is untouched
    }

    [Fact]
    public void ResetPutsEveryLineAndPauseBackToFactory()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsMenu.Arm();
        defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Z));
        defineInputsMenu.MoveUp();
        defineInputsMenu.Arm();
        defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Escape));

        defineInputsMenu.ResetAll(controlSettings);

        Assert.Equal("W", controlSettings[0][InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("NUMPAD8", controlSettings[1][InputAction.ShootUp].KeyBinding.GetDisplayName());
        Assert.Equal("P", controlSettings.Pause.GetDisplayName());
    }

    [Fact]
    public void ScrollingDown_WalksThroughPlayerTwo_AndOntoPause()
    {
        var defineInputsMenu = new DefineInputsMenu();

        for (var i = 0; i < DefineInputsMenu.LinesPerPlayer - 1; i++) defineInputsMenu.MoveDown();

        Assert.Equal(InputAction.ShootRight, defineInputsMenu.GetHighlightedAction());
        Assert.Equal(0, DefineInputsMenu.PlayerOf(defineInputsMenu.Line));

        defineInputsMenu.MoveDown();
        Assert.Equal(InputAction.MoveUp, defineInputsMenu.GetHighlightedAction());
        Assert.Equal(1, DefineInputsMenu.PlayerOf(defineInputsMenu.Line)); // player 2's section

        for (var i = 0; i < DefineInputsMenu.LinesPerPlayer; i++) defineInputsMenu.MoveDown();

        Assert.True(defineInputsMenu.IsPauseLine());

        defineInputsMenu.MoveDown();
        Assert.Equal(0, defineInputsMenu.Line); // and back round to the top
    }

    [Fact]
    public void ScrollingIsDeadWhileArmed_SoThatEveryKeyIsCapturable()
    {
        var defineInputsMenu = new DefineInputsMenu();
        defineInputsMenu.Arm();

        defineInputsMenu.MoveDown();
        defineInputsMenu.MoveUp();

        Assert.Equal(0, defineInputsMenu.Line);
    }

    [Fact]
    public void ScrollingToPlayerTwo_ScrollsTheWindowDown()
    {
        var defineInputsMenu = new DefineInputsMenu();

        for (var i = 0; i < DefineInputsMenu.LinesPerPlayer; i++) defineInputsMenu.MoveDown();

        Assert.Equal(DefineInputsMenu.PlayerTwoLine, defineInputsMenu.Line);
        Assert.True(defineInputsMenu.FirstVisibleLine > 0); // the P1 section has moved up
        Assert.True(defineInputsMenu.FirstVisibleLine <= DefineInputsMenu.PlayerTwoLine); // and the cursor is in the window
    }

    [Fact]
    public void ScrollingUp_FromTheFirstLine_LandsOnPause()
    {
        var defineInputsMenu = new DefineInputsMenu();

        defineInputsMenu.MoveUp();

        Assert.True(defineInputsMenu.IsPauseLine());
        Assert.Equal(DefineInputsMenu.PauseLine, defineInputsMenu.Line);
    }

    [Fact]
    public void TheCursorStepsOverTheBlankSpacers_NeverLandingOnOne()
    {
        var defineInputsMenu = new DefineInputsMenu();

        for (var i = 0; i < DefineInputsMenu.LinesPerPlayer; i++) defineInputsMenu.MoveDown();

        // Seven steps through player 1, then straight onto player 2's first line.
        Assert.Equal(DefineInputsMenu.PlayerTwoLine, defineInputsMenu.Line);
        Assert.Equal(InputAction.MoveUp, defineInputsMenu.GetHighlightedAction());

        for (var i = 0; i < DefineInputsMenu.LinesPerPlayer; i++) defineInputsMenu.MoveDown();

        Assert.True(defineInputsMenu.IsPauseLine()); // the gap before PAUSE is stepped over too

        defineInputsMenu.MoveDown();
        Assert.Equal(0, defineInputsMenu.Line); // and the wrap round the page

        defineInputsMenu.MoveUp();
        Assert.True(defineInputsMenu.IsPauseLine()); // back up over the gap
    }

    [Fact]
    public void ThePageIsTwentyOneLines_WithGapsBetweenTheSections()
    {
        Assert.Equal(8, InputActions.All.Length);
        Assert.Equal(8, DefineInputsMenu.LinesPerPlayer);
        Assert.Equal(2, DefineInputsMenu.SpacerLines);
        Assert.Equal(10, DefineInputsMenu.PlayerTwoLine);
        Assert.Equal(20, DefineInputsMenu.PauseLine);
        Assert.Equal(21, DefineInputsMenu.LineCount);

        Assert.Equal(0, DefineInputsMenu.PlayerOf(0));
        Assert.Equal(1, DefineInputsMenu.PlayerOf(15));
        Assert.Equal(InputAction.MoveUp, DefineInputsMenu.GetAction(0));
        Assert.Equal(InputAction.ShootRight, DefineInputsMenu.GetAction(7));
        Assert.True(DefineInputsMenu.IsSpacer(8)); // the blank lines between player 1 and player 2
        Assert.True(DefineInputsMenu.IsSpacer(9));
        Assert.Equal(InputAction.MoveUp, DefineInputsMenu.GetAction(DefineInputsMenu.PlayerTwoLine));
        Assert.Equal(InputAction.ShootRight, DefineInputsMenu.GetAction(17));
        Assert.True(DefineInputsMenu.IsSpacer(18)); // and before PAUSE
        Assert.True(DefineInputsMenu.IsSpacer(19));
        Assert.False(DefineInputsMenu.IsSpacer(DefineInputsMenu.PauseLine));
        Assert.Null(DefineInputsMenu.GetAction(DefineInputsMenu.PauseLine));
    }

    [Fact]
    public void ThePauseLine_TakesAGlobalBinding()
    {
        var defineInputsMenu = new DefineInputsMenu();
        var controlSettings = ControlSettings.CreateDefaults();

        defineInputsMenu.MoveUp(); // onto PAUSE
        defineInputsMenu.Arm();
        defineInputsMenu.Assign(controlSettings, InputBinding.CreateKey(Keys.Escape));

        Assert.Equal("ESCAPE", controlSettings.Pause.GetDisplayName());
        Assert.Equal(DefineInputsMenu.PauseLine, defineInputsMenu.Line); // there is nowhere to advance to
    }

    [Fact]
    public void TheWindowFollowsTheCursor_AndStaysOnScreen()
    {
        var defineInputsMenu = new DefineInputsMenu();

        for (var line = 1; line < DefineInputsMenu.LineCount; line++)
        {
            defineInputsMenu.MoveDown();

            Assert.True(defineInputsMenu.FirstVisibleLine <= defineInputsMenu.Line);
            Assert.True(defineInputsMenu.Line < defineInputsMenu.FirstVisibleLine + DefineInputsMenu.VisibleLines);
            Assert.True(defineInputsMenu.FirstVisibleLine >= 0);
            Assert.True(defineInputsMenu.FirstVisibleLine + DefineInputsMenu.VisibleLines <= DefineInputsMenu.LineCount);
        }

        for (var line = DefineInputsMenu.LineCount - 2; line >= 0; line--)
        {
            defineInputsMenu.MoveUp();

            Assert.True(defineInputsMenu.FirstVisibleLine <= defineInputsMenu.Line);
            Assert.True(defineInputsMenu.Line < defineInputsMenu.FirstVisibleLine + DefineInputsMenu.VisibleLines);
        }
    }
}
