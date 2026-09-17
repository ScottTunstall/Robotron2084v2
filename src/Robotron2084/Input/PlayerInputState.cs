using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// One poll of the player's input. <see cref="MoveDirection"/> components are
/// each exactly -1, 0, or 1 (8-way digital direction, per the integer-math
/// policy); <see cref="IntVector2.Zero"/> means no movement.
///
/// Two-stick layout (user requirement, 2026-09-12): <see cref="MoveDirection"/>
/// is the move stick (WASD / left stick) and <see cref="AimDirection"/> is the
/// aim stick (IJKL; gamepad right stick deferred). When <see cref="AimDirection"/>
/// is zero the player fires in their current facing direction.
///
/// <see cref="SkipLevelPressed"/> is the port's test key (P, 2026-09-13
/// round 6): clears the current level immediately so the author can jump
/// between waves while playtesting. No arcade counterpart.
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
    /// <summary>No-aim constructor (gamepad for now; aim stick is deferred).</summary>
    public PlayerInputState(IntVector2 moveDirection, bool firePressed)
        : this(moveDirection, IntVector2.Zero, firePressed)
    {
    }
}
