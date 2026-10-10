using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
///     Turns "what went down since the last tick" into an <see cref="InputBinding" />
///     (notes §101) — the DEFINE INPUTS page's capture step, kept MonoGame-free apart from
///     the two state structs so it can be unit-tested with snapshots built by hand.
/// </summary>
public static class ControlCapture
{
    /// <summary>
    ///     The buttons the page will accept. The sticking-point bits are deliberately NOT
    ///     here: a pushed stick is captured as a stick DIRECTION (which carries the
    ///     direction the player pushed), while these are the real buttons plus the two
    ///     thumb CLICKS, which are separate bits.
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
        Buttons.DPadRight
    ];

    /// <summary>
    ///     The first input that went DOWN between the two snapshots, or
    ///     <see cref="InputBinding.None" />. Keys are checked before pads, and the two pads
    ///     in order, so a key press captured on the same tick as a stick push wins — which
    ///     is the common case on a keyboard-defining pass.
    /// </summary>
    public static InputBinding GetNewlyPressed(InputSnapshot previous, InputSnapshot current)
    {
        foreach (var key in GetNewlyPressedKeys(previous.Keys, current.Keys)) return InputBinding.CreateKey(key);

        for (var pad = 0; pad < 2; pad++)
        {
            var stickBinding = GetNewlyPressedStick(pad, previous, current);
            if (stickBinding.Kind != InputBindingKind.None) return stickBinding;
        }

        for (var pad = 0; pad < 2; pad++)
        {
            var button = GetNewlyPressedButton(pad, previous, current);
            if (button.Kind != InputBindingKind.None) return button;
        }

        return InputBinding.None;
    }

    private static InputBinding GetNewlyPressedButton(int padIndex, InputSnapshot previous, InputSnapshot current)
    {
        var previousPad = padIndex == 1 ? previous.PadTwo : previous.PadOne;
        var currentPad = padIndex == 1 ? current.PadTwo : current.PadOne;

        foreach (var button in CapturableButtons)
            if (currentPad.IsButtonDown(button) && !previousPad.IsButtonDown(button))
                return InputBinding.CreateButton(padIndex, button);

        return InputBinding.None;
    }

    /// <summary>The keys that went down, in <see cref="Keys" /> order so the result is deterministic.</summary>
    private static IEnumerable<Keys> GetNewlyPressedKeys(KeyboardState previous, KeyboardState current)
    {
        return current.GetPressedKeys()
            .Where(key => !previous.IsKeyDown(key))
            .OrderBy(key => (int)key);
    }

    private static InputBinding GetNewlyPressedStick(int padIndex, InputSnapshot previous, InputSnapshot current)
    {
        var previousPad = padIndex == 1 ? previous.PadTwo : previous.PadOne;
        var currentPad = padIndex == 1 ? current.PadTwo : current.PadOne;

        foreach (var isRightStick in (bool[])[false, true])
        {
            var direction = GamePadSticks.Read(currentPad, isRightStick);
            if (direction != IntVector2.Zero && direction != GamePadSticks.Read(previousPad, isRightStick))
                return InputBinding.CreateStick(padIndex, isRightStick, direction.X, direction.Y);
        }

        return InputBinding.None;
    }
}
