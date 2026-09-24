using Microsoft.Xna.Framework;

namespace Robotron2084.Core;

/// <summary>
/// All-int distance/overlap helpers (integer-math policy: squared integer
/// distances, never <c>Vector2.DistanceSquared</c>).
/// </summary>
public static class RectangleExtensions
{
    /// <summary>Thin named wrapper over <see cref="Rectangle.Intersects(Rectangle)"/> used everywhere for readability.</summary>
    public static bool Overlaps(this Rectangle a, Rectangle b) => a.Intersects(b);

    /// <summary>Squared-distance check within <paramref name="distance"/> (inclusive).</summary>
    public static bool IsWithinDistance(this IntVector2 a, IntVector2 b, int distance) =>
        IntVector2.DistanceSquared(a, b) <= (long)distance * distance;

    /// <summary>Squared-distance check strictly beyond <paramref name="distance"/>.</summary>
    public static bool IsFartherThan(this IntVector2 a, IntVector2 b, int distance) =>
        IntVector2.DistanceSquared(a, b) > (long)distance * distance;
}
