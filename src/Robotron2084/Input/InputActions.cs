using Robotron2084.Input.Models;

namespace Robotron2084.Input;

/// <summary>The ordering of <see cref="InputAction" /> (the page draws them in this order).</summary>
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
        InputAction.ShootRight
    ];
}
