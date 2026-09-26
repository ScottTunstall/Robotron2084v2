using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Level;

/// <summary>
/// One laser-vs-wall flare. The ROM's flare lives 2 frames in LASCOL (`NAP 2,LDH1`), so 2 port ticks at the
/// 6/5 tick ratio.
/// </summary>
/// <param name="Bounds">Where the flare is drawn.</param>
/// <param name="Dithered">True for `LASDIV` (a top/bottom wall): LASCOL dithered with WALCOL.</param>
/// <param name="RemainingClockUnits">How long the flare has left: <c>NAP 2</c>, two ROM frames, counted in clock units (notes §52).</param>
internal sealed record LaserWallFlare(
    Rectangle Bounds,
    bool Dithered,
    int RemainingClockUnits = LaserWallFlare.LifeClockUnits)
{
    /// <summary>How long a new flare lasts: two ROM frames.</summary>
    public const int LifeClockUnits = 2 * ArcadeClock.UnitsPerRomFrame;
}
