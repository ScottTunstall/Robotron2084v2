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

    /// <summary>Works out how far apart two points are when you may only move sideways and up and down: the sideways gap plus the up-and-down gap.</summary>
    /// <param name="other">The other point.</param>
    public int GetManhattanDistance(IntVector2 other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Squared-distance check strictly beyond <paramref name="distance"/>.</summary>
    public bool IsFartherThan(IntVector2 b, int distance) => DistanceSquared(this, b) > (long)distance * distance;
}
