using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Input;

/// <summary>
/// The two factory control schemes (notes §101) and the rules that turn them into a
/// <see cref="PlayerInputState"/>: the stick vectors, and firing = "any shoot action is
/// held" (the port's rule since round 7, which the DEFINITIONS page has to preserve).
/// </summary>
public sealed class PlayerControlsTests
{
    private static readonly GamePadState NoPad = new();

    [Fact]
    public void PlayerOneDefaults_AreThePortsKeyboardScheme()
    {
        PlayerControls controls = PlayerControls.Defaults(0);

        Assert.Equal("W", controls[InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("S", controls[InputAction.MoveDown].Key.DisplayName);
        Assert.Equal("A", controls[InputAction.MoveLeft].Key.DisplayName);
        Assert.Equal("D", controls[InputAction.MoveRight].Key.DisplayName);
        Assert.Equal("I", controls[InputAction.ShootUp].Key.DisplayName);
        Assert.Equal("K", controls[InputAction.ShootDown].Key.DisplayName);
        Assert.Equal("J", controls[InputAction.ShootLeft].Key.DisplayName);
        Assert.Equal("L", controls[InputAction.ShootRight].Key.DisplayName);
    }

    [Fact]
    public void PlayerTwoDefaults_UseTheCursorKeysAndTheKeypad()
    {
        PlayerControls controls = PlayerControls.Defaults(1);

        Assert.Equal("UP", controls[InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("NUMPAD8", controls[InputAction.ShootUp].Key.DisplayName);
        Assert.Equal("NUMPAD5", controls[InputAction.ShootDown].Key.DisplayName);
    }

    [Fact]
    public void TheDefaultsBindTheArcadesTwoSticks()
    {
        PlayerControls playerOne = PlayerControls.Defaults(0);
        PlayerControls playerTwo = PlayerControls.Defaults(1);

        Assert.Equal("P1 LEFT STICK UP", playerOne[InputAction.MoveUp].Pad.DisplayName);
        Assert.Equal("P1 RIGHT STICK UP", playerOne[InputAction.ShootUp].Pad.DisplayName);
        Assert.Equal("P2 LEFT STICK UP", playerTwo[InputAction.MoveUp].Pad.DisplayName);
        Assert.Equal("P2 RIGHT STICK RT", playerTwo[InputAction.ShootRight].Pad.DisplayName);
    }

    [Fact]
    public void WASD_ProducesTheMoveVector()
    {
        PlayerControls controls = PlayerControls.Defaults(0);

        IntVector2 diagonal = controls.MoveDirection(new KeyboardState(Keys.W, Keys.D), NoPad, NoPad);

        Assert.Equal(new IntVector2(1, -1), diagonal);
    }

    [Fact]
    public void IJKL_ProducesTheShootVector_AndFires()
    {
        PlayerControls controls = PlayerControls.Defaults(0);
        var keys = new KeyboardState(Keys.J); // shoot left

        Assert.Equal(new IntVector2(-1, 0), controls.ShootDirection(keys, NoPad, NoPad));
        Assert.True(controls.Firing(keys, NoPad, NoPad));
    }

    [Fact]
    public void OppositeDirectionsCancel_AndNothingHeldMeansNoFire()
    {
        PlayerControls controls = PlayerControls.Defaults(0);
        var both = new KeyboardState(Keys.A, Keys.D);

        Assert.Equal(IntVector2.Zero, controls.MoveDirection(both, NoPad, NoPad));
            Assert.False(controls.Firing(new KeyboardState(), NoPad, NoPad));
    }

    [Fact]
    public void TheLeftStickMoves_AndTheRightStickShoots()
    {
        PlayerControls controls = PlayerControls.Defaults(0);
        GamePadState pad = TestPads.Pad(leftStick: new Vector2(0f, 1f), rightStick: new Vector2(-1f, 0f));

        Assert.Equal(new IntVector2(0, -1), controls.MoveDirection(new KeyboardState(), pad, NoPad));
        Assert.Equal(new IntVector2(-1, 0), controls.ShootDirection(new KeyboardState(), pad, NoPad));
        Assert.True(controls.Firing(new KeyboardState(), pad, NoPad));
    }

    [Fact]
    public void AReboundAction_ReplacesOnlyItsOwnDeviceSlot()
    {
        PlayerControls controls = PlayerControls.Defaults(0);
        ActionBinding line = controls[InputAction.MoveUp];

        // A keyboard key replaces the keyboard slot and leaves the pad binding alone.
        ActionBinding rebound = line.With(InputBinding.Key(Keys.Up));
        Assert.Equal("UP", rebound.Key.DisplayName);
        Assert.Equal("P1 LEFT STICK UP", rebound.Pad.DisplayName);

        // …and a pad input leaves the keyboard slot alone.
        ActionBinding onPad = line.With(InputBinding.Button(0, Buttons.Y));
        Assert.Equal("W", onPad.Key.DisplayName);
        Assert.Equal("P1 Y", onPad.Pad.DisplayName);

        // The page's Del clears both.
        Assert.Equal(InputBindingKind.None, line.With(InputBinding.None).Key.Kind);
        Assert.Equal(InputBindingKind.None, line.With(InputBinding.None).Pad.Kind);
    }
}
