using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Where the playfield sits on the port's canvas.</summary>
public static class PlayfieldLayout
{
    // NOTE: static FIELD initialisers run in DECLARATION order, so no initialiser here may read
    // another field of this class — a field read before its own initialiser has run is still its
    // default. Margin is a const so this pair is order-free; keep it that way, and do not move these
    // members about into a read-before-initialisation. Doing that once hid the wall, the score and
    // the spare men at once (nothing failed: 562 tests, both builds and every gate were green).
    // SMELL (unfixed, ledger D-031): this geometry is a static class of derived constants, and every
    // state that needs it caches its own copy of InnerBounds (PlayingState, AttractState,
    // StorylineState). One owned playfield rectangle, passed where it is needed, would remove the
    // duplication and the ordering question together.

    /// <summary>
    /// The margin between the canvas edge and the inner play area, in canvas pixels. It is a CONST
    /// (not a <c>static readonly</c>) so it is resolved at compile time, which is what makes the pair
    /// below immune to declaration order.
    /// </summary>
    private const int Margin = CollisionSizes.PlayfieldMarginSpecPixels * ScreenSize.SpecScale;

    /// <summary>The inner play area the wall encloses: the canvas less the margin on every side.</summary>
    public static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);
}
