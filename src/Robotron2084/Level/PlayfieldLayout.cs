using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Works out where the playfield sits on the screen.</summary>
public static class PlayfieldLayout
{
    // Nothing here may depend on the order static fields are set up in: a static initialiser that reads another
    // member of its own class can read that member's default. Both members below are computed, so there is no order.

    /// <summary>The gap between the edge of the canvas and the play area, in port pixels.</summary>
    private static int GetMargin() => ScreenSize.ToPortPixels(CollisionSizes.PlayfieldMarginArcadePixels);

    /// <summary>Gets the play area inside the wall, which is the screen less the gap on every side.</summary>
    public static Rectangle GetInnerBounds() => new(GetMargin(), GetMargin(), ScreenSize.Width - 2 * GetMargin(), ScreenSize.Height - 2 * GetMargin());
}
