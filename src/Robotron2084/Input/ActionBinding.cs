using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>
///     One line of the definitions page: the action's KEYBOARD binding and its GAMEPAD
///     binding. Two slots, because the port accepts either device at the same
///     time: arming a line and pressing a key
///     replaces the keyboard slot and leaves the pad slot alone, and vice versa.
/// </summary>
public readonly record struct ActionBinding(InputBinding KeyBinding, InputBinding PadBinding)
{
    /// <summary>An unbound line.</summary>
    public static readonly ActionBinding None = new(InputBinding.None, InputBinding.None);

    /// <summary>
    ///     The page's value column: "W OR P1 LEFT STICK UP" when both devices are bound, the one
    ///     device when only it is, or "NONE". The word OR is spelled out because a bare gap reads as
    ///     one long sentence, and a hyphen cannot substitute for it — the arcade's small font has no '-'.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var hasKey = KeyBinding.Kind != InputBindingKind.None;
            var hasPad = PadBinding.Kind != InputBindingKind.None;
            if (!hasKey && !hasPad) return "NONE";

            if (!hasKey) return PadBinding.GetDisplayName();

            if (!hasPad) return KeyBinding.GetDisplayName();

            return $"{KeyBinding.GetDisplayName()} OR {PadBinding.GetDisplayName()}";
        }
    }

    /// <summary>
    ///     Routes <paramref name="binding" /> to the slot its DEVICE belongs to — a keyboard
    ///     key to <see cref="KeyBinding" />, anything from a gamepad to <see cref="PadBinding" /> — and
    ///     clears both when given <see cref="InputBinding.None" /> (the page's Del).
    /// </summary>
    public ActionBinding With(InputBinding binding)
    {
        return binding.Kind switch
        {
            InputBindingKind.None => None,
            InputBindingKind.Key => this with { KeyBinding = binding },
            _ => this with { PadBinding = binding }
        };
    }

    /// <summary>True while either slot is held.</summary>
    public bool IsHeld(KeyboardState keyboardState, GamePadState padOne, GamePadState padTwo)
    {
        return KeyBinding.IsHeld(keyboardState, padOne, padTwo) || PadBinding.IsHeld(keyboardState, padOne, padTwo);
    }
}
