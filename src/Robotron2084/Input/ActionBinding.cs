using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>
/// One line of the definitions page: the action's KEYBOARD binding and its GAMEPAD
/// binding. Two slots, because the port accepts either device at the same
/// time: arming a line and pressing a key
/// replaces the keyboard slot and leaves the pad slot alone, and vice versa.
/// </summary>
public readonly record struct ActionBinding(InputBinding Key, InputBinding Pad)
{
    /// <summary>An unbound line.</summary>
    public static readonly ActionBinding None = new(InputBinding.None, InputBinding.None);

    /// <summary>
    /// Routes <paramref name="binding"/> to the slot its DEVICE belongs to — a keyboard
    /// key to <see cref="Key"/>, anything from a gamepad to <see cref="Pad"/> — and
    /// clears both when given <see cref="InputBinding.None"/> (the page's Del).
    /// </summary>
    public ActionBinding With(InputBinding binding) => binding.Kind switch
    {
        InputBindingKind.None => None,
        InputBindingKind.Key => this with { Key = binding },
        _ => this with { Pad = binding },
    };

    /// <summary>True while either slot is held.</summary>
    public bool IsHeld(KeyboardState keys, GamePadState padOne, GamePadState padTwo) =>
        Key.IsHeld(keys, padOne, padTwo) || Pad.IsHeld(keys, padOne, padTwo);

    /// <summary>
    /// The page's value column: "W OR P1 LEFT STICK UP" when both devices are bound, the one
    /// device when only it is, or "NONE". The word OR is spelled out because a bare gap reads as
    /// one long sentence, and a hyphen cannot substitute for it — the arcade's small font has no '-'.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (Key.Kind == InputBindingKind.None)
            {
                return Pad.Kind == InputBindingKind.None ? "NONE" : Pad.DisplayName;
            }

            return Pad.Kind == InputBindingKind.None ? Key.DisplayName : $"{Key.DisplayName} OR {Pad.DisplayName}";
        }
    }
}
