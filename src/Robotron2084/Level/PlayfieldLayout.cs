using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Where the playfield sits on the port's canvas.</summary>
public static class PlayfieldLayout
{
    // NOTE: nothing here may depend on static initialisation ORDER. A static field initialiser that
    // reads another member of its own class can read that member's DEFAULT (initialisers run in
    // declaration order), which is what once hid the wall, the score and the spare men — with 562
    // tests, both builds and every gate green. Both members below are computed PROPERTIES, so there is
    // no order to get wrong; if such a dependency is ever introduced, refactor it OUT rather than
    // documenting which line must come first. This code is read by humans, not only by the compiler.

    /// <summary>The inner play area the wall encloses: the canvas less the margin on every side.</summary>
    public static Rectangle InnerBounds => new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);

    /// <summary>The margin between the canvas edge and the inner play area, in canvas pixels.</summary>
    private static int Margin => ScreenSize.Scaled(CollisionSizes.PlayfieldMarginSpecPixels);
}
