using Robotron2084.Input.Models;

namespace Robotron2084.Input.Extensions;

/// <summary>Names and questions for an <see cref="InputAction" />.</summary>
public static class InputActionExtensions
{
    /// <summary>The page's label, e.g. "MOVE UP" / "SHOOT LEFT".</summary>
    public static string GetLabel(this InputAction action)
    {
        return action switch
        {
            InputAction.MoveUp => "MOVE UP",
            InputAction.MoveDown => "MOVE DOWN",
            InputAction.MoveLeft => "MOVE LEFT",
            InputAction.MoveRight => "MOVE RIGHT",
            InputAction.ShootUp => "SHOOT UP",
            InputAction.ShootDown => "SHOOT DOWN",
            InputAction.ShootLeft => "SHOOT LEFT",
            _ => "SHOOT RIGHT"
        };
    }

    /// <summary>True for the four move actions (the left stick).</summary>
    public static bool IsMove(this InputAction action)
    {
        return action
            is InputAction.MoveUp or InputAction.MoveDown or InputAction.MoveLeft or InputAction.MoveRight;
    }
}
