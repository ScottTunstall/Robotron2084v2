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
