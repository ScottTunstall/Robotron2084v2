using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Shared movement logic for the two enemies that drift around the arena bouncing off its
/// outer wall, and — once they decide to leave — head straight for whichever wall edge is
/// nearest and disappear when they reach it: <see cref="Spheroid"/> and <see cref="Quark"/>
/// (plan 8.6). A static helper rather than a shared base class, so each entity keeps its own
/// independent life cycle and state. Neither entity is stopped by an ELECTRODE — only the
/// outer playfield wall affects them.
/// </summary>
/// <remarks>
/// The spheroid's version is RRC11.ASM's `CIRCLE`, `CIRC2L`, `CIRC3L` and `CIRC4`; the
/// quark's is RRTK4.ASM's `SQUARE` and `SQVEL`.
/// </remarks>
internal static class WallFleeHelper
{
    /// <summary>
    /// Steps toward the nearest wall edge — whichever of the four (left, right, top, bottom)
    /// the entity currently has the shortest distance to — and reports whether the entity has
    /// arrived, which is when it disappears. This is how a spheroid or quark leaves the field
    /// once it decides to flee: rather than picking a single fixed direction, it always heads
    /// for whichever edge is currently closest.
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
