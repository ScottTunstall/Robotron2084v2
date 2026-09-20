using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>One tick's raw hardware, as the definitions page and the game read it.</summary>
public readonly record struct InputSnapshot(KeyboardState Keys, GamePadState PadOne, GamePadState PadTwo)
{
    /// <summary>Reads the live keyboard and both gamepads.</summary>
    public static InputSnapshot Read() => new(
        Keyboard.GetState(),
        GamePad.GetState(PlayerIndex.One),
        GamePad.GetState(PlayerIndex.Two));
}

/// <summary>
/// Turns "what went down since the last tick" into an <see cref="InputBinding"/>
/// (notes §101) — the DEFINE INPUTS page's capture step, kept MonoGame-free apart from
/// the two state structs so it can be unit-tested with snapshots built by hand.
/// </summary>
public static class ControlCapture
{
    /// <summary>
    /// The buttons the page will accept. The sticking-point bits are deliberately NOT
    /// here: a pushed stick is captured as a stick DIRECTION (which carries the
    /// direction the player pushed), while these are the real buttons plus the two
    /// thumb CLICKS, which are separate bits.
    /// </summary>
    private static readonly Buttons[] CapturableButtons =
    [
        Buttons.A,
        Buttons.B,
        Buttons.X,
        Buttons.Y,
        Buttons.LeftShoulder,
        Buttons.RightShoulder,
        Buttons.LeftStick,
        Buttons.RightStick,
        Buttons.Back,
        Buttons.Start,
        Buttons.BigButton,
        Buttons.DPadUp,
        Buttons.DPadDown,
        Buttons.DPadLeft,
        Buttons.DPadRight,
    ];

    /// <summary>
    /// The first input that went DOWN between the two snapshots, or
    /// <see cref="InputBinding.None"/>. Keys are checked before pads, and the two pads
    /// in order, so a key press captured on the same tick as a stick push wins — which
    /// is the common case on a keyboard-defining pass.
    /// </summary>
    public static InputBinding NewlyPressed(InputSnapshot previous, InputSnapshot current)
    {
        foreach (Keys key in NewlyPressedKeys(previous.Keys, current.Keys))
        {
            return InputBinding.Key(key);
        }

        for (int pad = 0; pad < 2; pad++)
        {
            InputBinding stick = NewlyPressedStick(pad, previous, current);
            if (stick.Kind != InputBindingKind.None)
            {
                return stick;
            }
        }

        for (int pad = 0; pad < 2; pad++)
        {
            InputBinding button = NewlyPressedButton(pad, previous, current);
            if (button.Kind != InputBindingKind.None)
            {
                return button;
            }
        }

        return InputBinding.None;
    }

    /// <summary>The keys that went down, in <see cref="Keys"/> order so the result is deterministic.</summary>
    private static IEnumerable<Keys> NewlyPressedKeys(KeyboardState previous, KeyboardState current) =>
        current.GetPressedKeys()
            .Where(key => !previous.IsKeyDown(key))
            .OrderBy(key => (int)key);

    private static InputBinding NewlyPressedStick(int padIndex, InputSnapshot previous, InputSnapshot current)
    {
        GamePadState previousPad = padIndex == 1 ? previous.PadTwo : previous.PadOne;
        GamePadState currentPad = padIndex == 1 ? current.PadTwo : current.PadOne;

        foreach (bool rightStick in (bool[])[false, true])
        {
            IntVector2 direction = GamePadSticks.Read(currentPad, rightStick);
            if (direction != IntVector2.Zero && direction != GamePadSticks.Read(previousPad, rightStick))
            {
                return InputBinding.Stick(padIndex, rightStick, direction.X, direction.Y);
            }
        }

        return InputBinding.None;
    }

    private static InputBinding NewlyPressedButton(int padIndex, InputSnapshot previous, InputSnapshot current)
    {
        GamePadState previousPad = padIndex == 1 ? previous.PadTwo : previous.PadOne;
        GamePadState currentPad = padIndex == 1 ? current.PadTwo : current.PadOne;

        foreach (Buttons button in CapturableButtons)
        {
            if (currentPad.IsButtonDown(button) && !previousPad.IsButtonDown(button))
            {
                return InputBinding.Button(padIndex, button);
            }
        }

        return InputBinding.None;
    }
}
