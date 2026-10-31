using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Input;

/// <summary>
///     The DEFINITIONS page's capture step (notes §101): what a press means while a line
///     is armed. The page is two steps on purpose — Enter arms, then the input is taken —
///     so that the cursor keys, which navigate the page, can themselves be bound.
/// </summary>
public sealed class ControlCaptureTests
{
    private static InputSnapshot Bare(KeyboardState? keyboardState = null, GamePadState? gamePadState = null)
    {
        return new InputSnapshot(keyboardState ?? new KeyboardState(), gamePadState ?? new GamePadState(), new GamePadState());
    }

    [Fact]
    public void AKeyGoingDown_IsCaptured()
    {
        var captured = ControlCapture.GetNewlyPressed(
            Bare(new KeyboardState(Keys.W)),
            Bare(new KeyboardState(Keys.W, Keys.Up)));

        Assert.Equal(InputBinding.CreateKey(Keys.Up), captured);
    }

    [Fact]
    public void AHeldKey_IsNotCapturedAgain()
    {
        var captured = ControlCapture.GetNewlyPressed(
            Bare(new KeyboardState(Keys.W)),
            Bare(new KeyboardState(Keys.W)));

        Assert.Equal(InputBindingKind.None, captured.Kind);
    }

    [Fact]
    public void AGamePadButton_IsCapturedWithItsPadAndButton()
    {
        var gamePadState = TestPads.WithButton(Buttons.Y);

        var captured = ControlCapture.GetNewlyPressed(Bare(), Bare(gamePadState: gamePadState));

        Assert.Equal(InputBinding.CreateButton(0, Buttons.Y), captured);
    }

    [Fact]
    public void AStickPush_IsCapturedAsADirection_NotAsAButton()
    {
        var gamePadState = TestPads.Pad(new Vector2(0f, 1f));

        var captured = ControlCapture.GetNewlyPressed(Bare(), Bare(gamePadState: gamePadState));

        // Up on the left stick, in screen space (XNA's Y is up-positive).
        Assert.Equal(InputBinding.CreateStick(0, false, 0, -1), captured);
    }

    [Fact]
    public void TheSecondPadsInputSaysSo()
    {
        var gamePadState = TestPads.WithButton(Buttons.A);
        InputSnapshot previous = new(new KeyboardState(), new GamePadState(), new GamePadState());
        InputSnapshot current = new(new KeyboardState(), new GamePadState(), gamePadState);

        var captured = ControlCapture.GetNewlyPressed(previous, current);

        Assert.Equal(InputBinding.CreateButton(1, Buttons.A), captured);
        Assert.Equal("P2 A", captured.GetDisplayName());
    }

    [Fact]
    public void AKeyBeatsAPadPressOnTheSameTick()
    {
        // A keyboard-defining pass is the common case, so it wins the tie.
        var gamePadState = TestPads.WithButton(Buttons.A);

        var captured = ControlCapture.GetNewlyPressed(Bare(), Bare(new KeyboardState(Keys.R), gamePadState));

        Assert.Equal(InputBinding.CreateKey(Keys.R), captured);
    }

    [Theory]
    [InlineData(Buttons.DPadUp)]
    [InlineData(Buttons.DPadDown)]
    [InlineData(Buttons.DPadLeft)]
    [InlineData(Buttons.DPadRight)]
    [InlineData(Buttons.Start)]
    [InlineData(Buttons.BigButton)]
    public void EveryCaptureableButton_IsCapturable(Buttons button)
    {
        var gamePadState = TestPads.WithButton(button);

        var captured = ControlCapture.GetNewlyPressed(Bare(), Bare(gamePadState: gamePadState));

        Assert.Equal(InputBinding.CreateButton(0, button), captured);
    }
}
