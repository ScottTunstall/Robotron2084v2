using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Where the playfield sits on the port's canvas.</summary>
public static class PlayfieldLayout
{
    private static readonly int Margin = ScreenSize.Scaled(CollisionSizes.PlayfieldMarginSpecPixels);

    /// <summary>The inner play area the wall encloses: the canvas less the margin on every side.</summary>
    public static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);
}
