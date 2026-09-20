using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The spheroid's and quark's shared exit: step to the nearest wall edge and vanish there.</summary>
/// <seealso cref="Spheroid"/>
/// <seealso cref="Quark"/>
/// <remarks>ROM: the spheroid's version is RRC11.ASM's <c>CIRCLE</c>/<c>CIRC2L</c>/<c>CIRC3L</c>/
/// <c>CIRC4</c>; the quark's is RRTK4.ASM's <c>SQUARE</c>/<c>SQVEL</c>.</remarks>
internal static class WallFleeHelper
{
    /// <summary>Steps toward the nearest wall edge, and reports whether the entity reached it.</summary>
    /// <param name="position">The entity's current top-left corner.</param>
    /// <param name="width">The entity's box width, in port pixels.</param>
    /// <param name="height">The entity's box height, in port pixels.</param>
    /// <param name="speed">How many port pixels to move this tick.</param>
    /// <param name="wall">The wall to make for.</param>
    /// <param name="newPosition">The position after the step.</param>
    /// <returns>True once the entity's box has reached the wall and should be removed.</returns>
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
