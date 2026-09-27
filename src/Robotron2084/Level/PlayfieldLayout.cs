using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Where the playfield sits on the port's canvas.</summary>
public static class PlayfieldLayout
{
    /// <summary>
    /// The margin between the canvas edge and the inner play area, in canvas pixels. It is a CONST
    /// (not a <c>static readonly</c>) so it is resolved at compile time: a field initialised from
    /// another field depends on declaration order, and reordering the fields has silently zeroed
    /// this margin before.
    /// </summary>
    private const int Margin = CollisionSizes.PlayfieldMarginSpecPixels * ScreenSize.SpecScale;

    /// <summary>The inner play area the wall encloses: the canvas less the margin on every side.</summary>
    public static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);
}
