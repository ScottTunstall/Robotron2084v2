namespace Robotron2084.Core;

/// <summary>
/// 2D vector of plain integers — the only vector type used in gameplay code
/// (integer-math policy: no float/Vector2 anywhere in gameplay logic).
/// Positions and velocities are per fixed-timestep tick.
/// </summary>
public readonly record struct IntVector2(int X, int Y)
{
    /// <summary>The zero vector (also "no movement" for input direction).</summary>
    public static readonly IntVector2 Zero = new(0, 0);

    public static IntVector2 operator +(IntVector2 a, IntVector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static IntVector2 operator -(IntVector2 a, IntVector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static IntVector2 operator *(IntVector2 a, int scalar) => new(a.X * scalar, a.Y * scalar);

    /// <summary>Squared distance widened to <see cref="long"/> to avoid overflow — never float distance math.</summary>
    public static long DistanceSquared(IntVector2 a, IntVector2 b)
    {
        long dx = a.X - b.X;
        long dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>Hands off to <see cref="Rectangle"/>'s all-int constructor.</summary>
    public Microsoft.Xna.Framework.Point ToPoint() => new(X, Y);
}
