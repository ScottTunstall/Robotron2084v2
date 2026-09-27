namespace Robotron2084.Core;

/// <summary>Integer-math distance helpers (squared integer distances, never <c>Vector2.DistanceSquared</c>).</summary>
public static class IntVector2Extensions
{
    /// <summary>Squared-distance check strictly beyond <paramref name="distance"/>.</summary>
    public static bool IsFartherThan(this IntVector2 a, IntVector2 b, int distance) =>
        IntVector2.DistanceSquared(a, b) > (long)distance * distance;
}
