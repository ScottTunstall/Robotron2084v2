using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// Left stick (8-way, dead-zoned and quantized) + A button / right trigger
/// (fire). The one permitted place floating point touches gameplay-adjacent
/// code: <see cref="GamePadState.ThumbSticks"/> is inherently <see cref="Vector2"/>
/// at the hardware/API boundary, and is fully quantized to an
/// <see cref="IntVector2"/> before it leaves <see cref="Poll"/> — no float
/// crosses into <see cref="PlayerInputState"/> or beyond.
/// </summary>
public sealed class GamePadPlayerInputSource : IPlayerInputSource
{
    private const float DeadZoneLengthSquared = 0.0625f; // 0.25^2 — still-centered stick

    private readonly PlayerIndex _playerIndex;

    public GamePadPlayerInputSource(PlayerIndex playerIndex = PlayerIndex.One)
    {
        _playerIndex = playerIndex;
    }

    public PlayerInputState Poll()
    {
        GamePadState state = GamePad.GetState(_playerIndex);
        Vector2 stick = state.ThumbSticks.Left; // local to this method only

        // XNA left-stick Y is up-positive; screen Y is down-positive, so negate before signing.
        IntVector2 moveDirection = stick.LengthSquared() < DeadZoneLengthSquared
            ? IntVector2.Zero
            : new(Math.Sign(stick.X), Math.Sign(-stick.Y));

        // The trigger threshold float is likewise local/immediate, not stored.
        bool fire = state.IsButtonDown(Buttons.A) || state.Triggers.Right > 0.5f;

        // Coin-door buttons: START = one player, BACK = two (the arcade's
        // START 1 / START 2).
        bool startOne = state.IsButtonDown(Buttons.Start);
        bool startTwo = state.IsButtonDown(Buttons.Back);

        return new PlayerInputState(moveDirection, fire) with
        {
            StartOnePlayerPressed = startOne,
            StartTwoPlayersPressed = startTwo,
        };
    }
}
