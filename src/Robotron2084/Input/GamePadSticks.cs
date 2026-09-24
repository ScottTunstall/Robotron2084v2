using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// Reads a gamepad stick as one of the eight directions (or zero), dead-zoned and
/// quantized. The single place floating point touches port-only input code:
/// <see cref="GamePadState.ThumbSticks"/> is <see cref="Vector2"/> at the
/// hardware boundary and is quantized before it leaves.
/// </summary>
public static class GamePadSticks
{
    private const float DeadZoneLengthSquared = 0.0625f; // 0.25^2 — still-centered

    /// <summary>The stick's direction in SCREEN space (Y down-positive), or zero when centred.</summary>
    public static IntVector2 Read(GamePadState pad, bool rightStick)
    {
        Vector2 stick = rightStick ? pad.ThumbSticks.Right : pad.ThumbSticks.Left;
        if (stick.LengthSquared() < DeadZoneLengthSquared)
        {
            return IntVector2.Zero;
        }

        return new IntVector2(Math.Sign(stick.X), Math.Sign(-stick.Y));
    }
}
