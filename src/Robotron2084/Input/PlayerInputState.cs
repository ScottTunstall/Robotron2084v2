using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// One poll of the player's input. <see cref="MoveDirection"/> components are
/// each exactly -1, 0, or 1 (8-way digital direction, per the integer-math
/// policy); <see cref="IntVector2.Zero"/> means no movement.
///
/// Two-stick layout: <see cref="MoveDirection"/> is the move stick and
/// <see cref="AimDirection"/> is the aim stick. When <see cref="AimDirection"/>
/// is zero the player fires in their current facing direction.
///
/// <see cref="SkipLevelPressed"/> is a port-only playtest input: it clears the
/// current level immediately, so waves can be jumped between. No arcade
/// counterpart.
///
/// <see cref="StartOnePlayerPressed"/> / <see cref="StartTwoPlayersPressed"/>
/// are the arcade's coin-door START 1 / START 2 buttons (ROM PIA2 B4/B5 → RRG23
/// START1/START2), read by the title screen to pick the number of players.
/// </summary>
public readonly record struct PlayerInputState(
    IntVector2 MoveDirection,
    IntVector2 AimDirection,
    bool FirePressed,
    bool SkipLevelPressed = false,
    bool StartOnePlayerPressed = false,
    bool StartTwoPlayersPressed = false)
{
    /// <summary>No-aim constructor: the player fires along their facing direction.</summary>
    public PlayerInputState(IntVector2 moveDirection, bool firePressed)
        : this(moveDirection, IntVector2.Zero, firePressed)
    {
    }
}
