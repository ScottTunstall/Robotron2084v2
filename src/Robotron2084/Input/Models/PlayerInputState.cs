using Robotron2084.Core;
using Robotron2084.Core.Models;

namespace Robotron2084.Input.Models;

/// <summary>
///     One poll of the player's input. <see cref="MoveDirection" /> components are
///     each exactly -1, 0, or 1 (8-way digital direction, per the integer-math
///     policy); <see cref="IntVector2.Zero" /> means no movement.
///     Two-stick layout: <see cref="MoveDirection" /> is the move stick and
///     <see cref="ShootDirection" /> is the aim stick. When <see cref="ShootDirection" />
///     is zero the player fires in their current facing direction.
///     <see cref="SkipWaveHeld" /> is a port-only playtest input: it clears the
///     current wave immediately, so waves can be jumped between. No arcade
///     counterpart.
///     <see cref="StartOnePlayerHeld" /> / <see cref="StartTwoPlayersHeld" />
///     are the arcade's coin-door START 1 / START 2 buttons (ROM PIA2 B4/B5 → RRG23
///     START1/START2), read by the title screen to pick the number of players.
/// </summary>
/// <param name="MoveDirection">The way the move stick is pushed, or <see cref="IntVector2.Zero" /> when it is not.</param>
/// <param name="ShootDirection">The way the aim stick is pushed, or <see cref="IntVector2.Zero" /> when it is not.</param>
/// <param name="FireHeld">Whether the fire control is held.</param>
/// <param name="SkipWaveHeld">Whether the port's skip-wave key is held.</param>
/// <param name="StartOnePlayerHeld">Whether START 1 is held.</param>
/// <param name="StartTwoPlayersHeld">Whether START 2 is held.</param>
public readonly record struct PlayerInputState(
    IntVector2 MoveDirection,
    IntVector2 ShootDirection,
    bool FireHeld,
    bool SkipWaveHeld = false,
    bool StartOnePlayerHeld = false,
    bool StartTwoPlayersHeld = false)
{
    /// <summary>No-aim constructor: the player fires along their facing direction.</summary>
    /// <param name="moveDirection">The way the move stick is pushed.</param>
    /// <param name="fireHeld">Whether the fire control is held.</param>
    public PlayerInputState(IntVector2 moveDirection, bool fireHeld)
        : this(moveDirection, IntVector2.Zero, fireHeld)
    {
    }
}
