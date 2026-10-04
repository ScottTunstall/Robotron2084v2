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
        PlayerControls controls = PlayerControls.CreateDefaults(0);

        Assert.Equal("W", controls[InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("S", controls[InputAction.MoveDown].KeyBinding.GetDisplayName());
        Assert.Equal("A", controls[InputAction.MoveLeft].KeyBinding.GetDisplayName());
        Assert.Equal("D", controls[InputAction.MoveRight].KeyBinding.GetDisplayName());
        Assert.Equal("I", controls[InputAction.ShootUp].KeyBinding.GetDisplayName());
        Assert.Equal("K", controls[InputAction.ShootDown].KeyBinding.GetDisplayName());
        Assert.Equal("J", controls[InputAction.ShootLeft].KeyBinding.GetDisplayName());
        Assert.Equal("L", controls[InputAction.ShootRight].KeyBinding.GetDisplayName());
    }

    [Fact]
    public void PlayerTwoDefaults_UseTheCursorKeysAndTheKeypad()
    {
        PlayerControls controls = PlayerControls.CreateDefaults(1);

        Assert.Equal("UP", controls[InputAction.MoveUp].KeyBinding.GetDisplayName());
        Assert.Equal("NUMPAD8", controls[InputAction.ShootUp].KeyBinding.GetDisplayName());
        Assert.Equal("NUMPAD5", controls[InputAction.ShootDown].KeyBinding.GetDisplayName());
    }

    [Fact]
    public void TheDefaultsBindTheArcadesTwoSticks()
    {
        PlayerControls playerOne = PlayerControls.CreateDefaults(0);
        PlayerControls playerTwo = PlayerControls.CreateDefaults(1);

        Assert.Equal("P1 LEFT STICK UP", playerOne[InputAction.MoveUp].PadBinding.GetDisplayName());
        Assert.Equal("P1 RIGHT STICK UP", playerOne[InputAction.ShootUp].PadBinding.GetDisplayName());
        Assert.Equal("P2 LEFT STICK UP", playerTwo[InputAction.MoveUp].PadBinding.GetDisplayName());
        Assert.Equal("P2 RIGHT STICK RIGHT", playerTwo[InputAction.ShootRight].PadBinding.GetDisplayName());
    }

    [Fact]
    public void WASD_ProducesTheMoveVector()
    {
        PlayerControls controls = PlayerControls.CreateDefaults(0);

        IntVector2 diagonal = controls.GetMoveDirection(new KeyboardState(Keys.W, Keys.D), NoPad, NoPad);

        Assert.Equal(new IntVector2(1, -1), diagonal);
    }

    [Fact]
    public void IJKL_ProducesTheShootVector_AndFires()
    {
        PlayerControls controls = PlayerControls.CreateDefaults(0);
        var keys = new KeyboardState(Keys.J); // shoot left

        Assert.Equal(new IntVector2(-1, 0), controls.GetShootDirection(keys, NoPad, NoPad));
        Assert.True(controls.IsFiring(keys, NoPad, NoPad));
    }

    [Fact]
    public void OppositeDirectionsCancel_AndNothingHeldMeansNoFire()
    {
        PlayerControls controls = PlayerControls.CreateDefaults(0);
        var both = new KeyboardState(Keys.A, Keys.D);

        Assert.Equal(IntVector2.Zero, controls.GetMoveDirection(both, NoPad, NoPad));
            Assert.False(controls.IsFiring(new KeyboardState(), NoPad, NoPad));
    }

    [Fact]
    public void TheLeftStickMoves_AndTheRightStickShoots()
    {
        PlayerControls controls = PlayerControls.CreateDefaults(0);
        GamePadState pad = TestPads.Pad(leftStick: new Vector2(0f, 1f), isRightStick: new Vector2(-1f, 0f));

        Assert.Equal(new IntVector2(0, -1), controls.GetMoveDirection(new KeyboardState(), pad, NoPad));
        Assert.Equal(new IntVector2(-1, 0), controls.GetShootDirection(new KeyboardState(), pad, NoPad));
        Assert.True(controls.IsFiring(new KeyboardState(), pad, NoPad));
    }

    [Fact]
    public void AReboundAction_ReplacesOnlyItsOwnDeviceSlot()
    {
        PlayerControls controls = PlayerControls.CreateDefaults(0);
        ActionBinding line = controls[InputAction.MoveUp];

        // A keyboard key replaces the keyboard slot and leaves the pad binding alone.
        ActionBinding rebound = line.With(InputBinding.CreateKey(Keys.Up));
        Assert.Equal("UP", rebound.KeyBinding.GetDisplayName());
        Assert.Equal("P1 LEFT STICK UP", rebound.PadBinding.GetDisplayName());

        // …and a pad input leaves the keyboard slot alone.
        ActionBinding onPad = line.With(InputBinding.CreateButton(0, Buttons.Y));
        Assert.Equal("W", onPad.KeyBinding.GetDisplayName());
        Assert.Equal("P1 Y", onPad.PadBinding.GetDisplayName());

        // The page's Del clears both.
        Assert.Equal(InputBindingKind.None, line.With(InputBinding.None).KeyBinding.Kind);
        Assert.Equal(InputBindingKind.None, line.With(InputBinding.None).PadBinding.Kind);
    }
}
