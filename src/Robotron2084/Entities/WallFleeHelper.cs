using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Shared wall logic for the two entities that glide around the field and then make for a
/// wall edge and vanish — <see cref="Spheroid"/> and <see cref="Quark"/> (plan 8.6). A small
/// internal helper rather than a base class, so each entity keeps its own life cycle.
///
/// Note for callers: neither entity is stopped by an ELECTRODE. Only the playfield wall
/// reflects them.
/// </summary>
/// <remarks>
/// The spheroid's version is RRC11.ASM's `CIRCLE`, `CIRC2L`, `CIRC3L` and `CIRC4`; the
/// quark's is RRTK4.ASM's `SQUARE` and `SQVEL`.
/// </remarks>
internal static class WallFleeHelper
{
    /// <summary>
    /// Moves the entity by its velocity, bouncing back the X or the Y component when that axis
    /// alone would cross the wall. A diagonal that still fits is left alone.
    /// </summary>
    /// <param name="position">The entity's current top-left corner.</param>
    /// <param name="velocity">This tick's step on each axis.</param>
    /// <param name="width">The entity's box width, in port pixels.</param>
    /// <param name="height">The entity's box height, in port pixels.</param>
    /// <param name="wall">The playfield wall to test against.</param>
    /// <param name="newVelocity">
    /// The velocity after any reflection, so the caller can keep its movement direction — and
    /// so its animation — in step with where it is actually going.
    /// </param>
    /// <returns>The entity's new top-left corner.</returns>
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
    /// Steps toward the nearest wall edge — whichever of the four is closest — and reports
    /// whether the entity has arrived, which is when it disappears.
    /// </summary>
    /// <param name="position">The entity's current top-left corner.</param>
    /// <param name="width">The entity's box width, in port pixels.</param>
    /// <param name="height">The entity's box height, in port pixels.</param>
    /// <param name="speed">How many port pixels to move this tick.</param>
    /// <param name="wall">The wall whose edges the entity is making for.</param>
    /// <param name="newPosition">The position after the step.</param>
    /// <returns>True once the entity's box has reached the wall and it should be removed.</returns>
    /// <remarks>Spec: "moves to the closest WALL EDGE and DISAPPEARS".</remarks>
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
