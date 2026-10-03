using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Input;

/// <summary>
/// The DEFINITIONS page's capture step (notes §101): what a press means while a line
/// is armed. The page is two steps on purpose — Enter arms, then the input is taken —
/// so that the cursor keys, which navigate the page, can themselves be bound.
/// </summary>
public sealed class ControlCaptureTests
{
    private static InputSnapshot Bare(KeyboardState? keys = null, GamePadState? pad = null) =>
        new(keys ?? new KeyboardState(), pad ?? new GamePadState(), new GamePadState());

    [Fact]
    public void AKeyGoingDown_IsCaptured()
    {
        InputBinding captured = ControlCapture.GetNewlyPressed(
            Bare(new KeyboardState(Keys.W)),
            Bare(new KeyboardState(Keys.W, Keys.Up)));

        Assert.Equal(InputBinding.CreateKey(Keys.Up), captured);
    }

    [Fact]
    public void AHeldKey_IsNotCapturedAgain()
    {
        InputBinding captured = ControlCapture.GetNewlyPressed(
            Bare(new KeyboardState(Keys.W)),
            Bare(new KeyboardState(Keys.W)));

        Assert.Equal(InputBindingKind.None, captured.Kind);
    }

    [Fact]
    public void AGamePadButton_IsCapturedWithItsPadAndButton()
    {
        GamePadState pad = TestPads.WithButton(Buttons.Y);

        InputBinding captured = ControlCapture.GetNewlyPressed(Bare(), Bare(pad: pad));

        Assert.Equal(InputBinding.CreateButton(0, Buttons.Y), captured);
    }

    [Fact]
    public void AStickPush_IsCapturedAsADirection_NotAsAButton()
    {
        GamePadState pad = TestPads.Pad(leftStick: new Vector2(0f, 1f));

        InputBinding captured = ControlCapture.GetNewlyPressed(Bare(), Bare(pad: pad));

        // Up on the left stick, in screen space (XNA's Y is up-positive).
        Assert.Equal(InputBinding.CreateStick(0, rightStick: false, 0, -1), captured);
    }

    [Fact]
    public void TheSecondPadsInputSaysSo()
    {
        GamePadState pad = TestPads.WithButton(Buttons.A);
        InputSnapshot previous = new(new KeyboardState(), new GamePadState(), new GamePadState());
        InputSnapshot current = new(new KeyboardState(), new GamePadState(), pad);

        InputBinding captured = ControlCapture.GetNewlyPressed(previous, current);

        Assert.Equal(InputBinding.CreateButton(1, Buttons.A), captured);
        Assert.Equal("P2 A", captured.GetDisplayName());
    }

    [Fact]
    public void AKeyBeatsAPadPressOnTheSameTick()
    {
        // A keyboard-defining pass is the common case, so it wins the tie.
        GamePadState pad = TestPads.WithButton(Buttons.A);

        InputBinding captured = ControlCapture.GetNewlyPressed(Bare(), Bare(new KeyboardState(Keys.R), pad));

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
        GamePadState pad = TestPads.WithButton(button);

        InputBinding captured = ControlCapture.GetNewlyPressed(Bare(), Bare(pad: pad));

        Assert.Equal(InputBinding.CreateButton(0, button), captured);
    }
}
