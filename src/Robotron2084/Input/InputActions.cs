namespace Robotron2084.Input;

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
