namespace Robotron2084.Input.Models;

/// <summary>
///     The port's own control actions (notes §101). Robotron's cabinet has two fixed
///     eight-way sticks — the left moves, the right fires — so these eight ARE the
///     arcade's controls; which physical key or stick they land on is the port's choice,
///     and that is what the DEFINE INPUTS page edits.
/// </summary>
public enum InputAction
{
    /// <summary>Move up.</summary>
    MoveUp,

    /// <summary>Move down.</summary>
    MoveDown,

    /// <summary>Move left.</summary>
    MoveLeft,

    /// <summary>Move right.</summary>
    MoveRight,

    /// <summary>Shoot up.</summary>
    ShootUp,

    /// <summary>Shoot down.</summary>
    ShootDown,

    /// <summary>Shoot left.</summary>
    ShootLeft,

    /// <summary>Shoot right.</summary>
    ShootRight
}
