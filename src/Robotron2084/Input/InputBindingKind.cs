using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Input;

/// <summary>What a binding is attached to.</summary>
public enum InputBindingKind
{
    /// <summary>Nothing — the action is unbound.</summary>
    None,

    /// <summary>A keyboard key (<see cref="InputBinding.Code"/> is a <see cref="Keys"/>).</summary>
    Key,

    /// <summary>A gamepad button (<see cref="InputBinding.Code"/> is a <see cref="Buttons"/>).</summary>
    GamePadButton,

    /// <summary>A left-stick direction (<see cref="InputBinding.Code"/> is a direction code).</summary>
    GamePadLeftStick,

    /// <summary>A right-stick direction.</summary>
    GamePadRightStick,
}
