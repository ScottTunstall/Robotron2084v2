using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>
/// The port's own control actions (notes §101). Robotron's cabinet has two fixed
/// eight-way sticks — the left moves, the right fires — so these eight ARE the
/// arcade's controls; which physical key or stick they land on is the port's choice,
/// and that is what the DEFINE INPUTS page edits.
/// </summary>
public enum InputAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    ShootUp,
    ShootDown,
    ShootLeft,
    ShootRight,
}

/// <summary>Names and ordering for <see cref="InputAction"/> (the page draws these).</summary>
public static class InputActions
{
    /// <summary>The eight actions in the page's order: the move stick, then the shoot stick.</summary>
    public static readonly InputAction[] All =
    [
        InputAction.MoveUp,
        InputAction.MoveDown,
        InputAction.MoveLeft,
        InputAction.MoveRight,
        InputAction.ShootUp,
        InputAction.ShootDown,
        InputAction.ShootLeft,
        InputAction.ShootRight,
    ];

    /// <summary>True for the four move actions (the left stick).</summary>
    public static bool IsMove(this InputAction action) => action
        is InputAction.MoveUp or InputAction.MoveDown or InputAction.MoveLeft or InputAction.MoveRight;

    /// <summary>The page's label, e.g. "MOVE UP" / "SHOOT LEFT".</summary>
    public static string Label(this InputAction action) => action switch
    {
        InputAction.MoveUp => "MOVE UP",
        InputAction.MoveDown => "MOVE DOWN",
        InputAction.MoveLeft => "MOVE LEFT",
        InputAction.MoveRight => "MOVE RIGHT",
        InputAction.ShootUp => "SHOOT UP",
        InputAction.ShootDown => "SHOOT DOWN",
        InputAction.ShootLeft => "SHOOT LEFT",
        _ => "SHOOT RIGHT",
    };
}

/// <summary>
/// One line of the definitions page: the action's KEYBOARD binding and its GAMEPAD
/// binding. Two slots, because the port has always accepted either device at the same
/// time (the old <c>CompositePlayerInputSource</c>): arming a line and pressing a key
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

    /// <summary>The page's value column: "W  P1-LS-UP", or one device only, or "-".</summary>
    public string DisplayName
    {
        get
        {
            if (Key.Kind == InputBindingKind.None)
            {
                return Pad.Kind == InputBindingKind.None ? "-" : Pad.DisplayName;
            }

            return Pad.Kind == InputBindingKind.None ? Key.DisplayName : $"{Key.DisplayName}  {Pad.DisplayName}";
        }
    }
}
