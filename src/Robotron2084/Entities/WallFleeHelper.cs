using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Shared wall logic for Spheroid and Quark — the two genuinely near-identical
/// "glide, drop, flee and vanish" entities (plan 8.6). Kept as a small
/// internal helper rather than a base class so each entity keeps its own
/// independent lifecycle.
/// </summary>
internal static class WallFleeHelper
{
    /// <summary>
    /// Advances <paramref name="position"/> by <paramref name="velocity"/>,
    /// reflecting the X or Y component when that axis alone would cross the
    /// wall (spheroids/quarks "glide" over electrodes — only the wall stops them).
    /// </summary>
    public static IntVector2 MoveWithReflection(IntVector2 position, IntVector2 velocity, int width, int height, PlayfieldWall wall, out IntVector2 newVelocity)
    {
        IntVector2 v = velocity;
        if (wall.Intersects(new Rectangle(position.X + v.X, position.Y, width, height)))
        {
            v = new IntVector2(-v.X, v.Y);
        }

        if (wall.Intersects(new Rectangle(position.X, position.Y + v.Y, width, height)))
        {
            v = new IntVector2(v.X, -v.Y);
        }

        newVelocity = v;
        return position + v;
    }

    /// <summary>
    /// Moves one axis at a time toward the nearest wall edge; returns true
    /// once the entity has reached the wall (ready to disappear, spec:
    /// "moves to the closest WALL EDGE and DISAPPEARS").
    /// </summary>
    public static bool FleeTowardNearestEdge(IntVector2 position, int width, int height, int speed, PlayfieldWall wall, out IntVector2 newPosition)
    {
        Rectangle outer = wall.OuterBounds;
        int distLeft = position.X - outer.X;
        int distRight = outer.Right - (position.X + width);
        int distTop = position.Y - outer.Y;
        int distBottom = outer.Bottom - (position.Y + height);
        int min = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        IntVector2 candidate = position;
        if (min == distLeft)
        {
            candidate -= new IntVector2(speed, 0);
        }
        else if (min == distRight)
        {
            candidate += new IntVector2(speed, 0);
        }
        else if (min == distTop)
        {
            candidate -= new IntVector2(0, speed);
        }
        else
        {
            candidate += new IntVector2(0, speed);
        }

        newPosition = candidate;
        return wall.Intersects(new Rectangle(candidate.X, candidate.Y, width, height));
    }
}
