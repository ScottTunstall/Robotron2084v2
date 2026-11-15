namespace Robotron2084.Core.Models;

/// <summary>
///     2D vector of plain integers — the only vector type used in gameplay code
///     (integer-math policy: no float/Vector2 anywhere in gameplay logic).
///     Positions and velocities are per fixed-timestep <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.
/// </summary>
/// <param name="X">The part across the screen. Right is positive.</param>
/// <param name="Y">The part up and down the screen. Down is positive.</param>
public readonly record struct IntVector2(int X, int Y)
{
    /// <summary>The zero vector (also "no movement" for input direction).</summary>
    public static readonly IntVector2 Zero = new(0, 0);

    /// <summary>Adds two vectors, one part at a time.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The sum.</returns>
    public static IntVector2 operator +(IntVector2 a, IntVector2 b)
    {
        return new IntVector2(a.X + b.X, a.Y + b.Y);
    }

    /// <summary>Takes the second vector from the first, one part at a time.</summary>
    /// <param name="a">The vector to take from.</param>
    /// <param name="b">The vector to take away.</param>
    /// <returns>The difference.</returns>
    public static IntVector2 operator -(IntVector2 a, IntVector2 b)
    {
        return new IntVector2(a.X - b.X, a.Y - b.Y);
    }

    /// <summary>Makes a vector longer or shorter by a whole number.</summary>
    /// <param name="a">The vector.</param>
    /// <param name="scalar">What each part is multiplied by.</param>
    /// <returns>The scaled vector.</returns>
    public static IntVector2 operator *(IntVector2 a, int scalar)
    {
        return new IntVector2(a.X * scalar, a.Y * scalar);
    }

    /// <summary>Squared distance widened to <see cref="long" /> to avoid overflow — never float distance math.</summary>
    public static long ComputeDistanceSquared(IntVector2 a, IntVector2 b)
    {
        long dx = a.X - b.X;
        long dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>
    ///     Works out how far apart two points are when you may only move sideways and up and down: the sideways gap plus
    ///     the up-and-down gap.
    /// </summary>
    /// <param name="other">The other point.</param>
    public int GetManhattanDistance(IntVector2 other)
    {
        return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    }

    /// <summary>Squared-distance check strictly beyond <paramref name="distance" />.</summary>
    public bool IsFartherThan(IntVector2 b, int distance)
    {
        return ComputeDistanceSquared(this, b) > (long)distance * distance;
    }
}
