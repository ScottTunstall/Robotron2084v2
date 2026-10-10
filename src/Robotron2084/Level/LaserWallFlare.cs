using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Level;

/// <summary>A short flash of colour where a laser has run into the wall.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>LASDIE</c>, which draws it in
/// <c>LASCOL</c> and waits with <c>NAP 2</c></item>
/// <item>Disassembly: not separately labelled</item>
/// </list> A flash on a side wall is solid. A flash on
/// the top or bottom wall is dithered, so the wall shows through every other row.</item>
/// </list>
/// </remarks>
/// <param name="Bounds">Where the flash is drawn.</param>
/// <param name="Dithered">True for a flash on the top or bottom wall, which lets the wall show through every other row.</param>
/// <param name="RemainingClockUnits">How long the flash has left, in clock units (notes §52).</param>
internal sealed record LaserWallFlare(
    Rectangle Bounds,
    bool Dithered,
    int RemainingClockUnits = LaserWallFlare.LifeClockUnits)
{
    /// <summary>How long a new flash lasts, in clock units: two ROM frames. It is the starting value of <see cref="RemainingClockUnits"/>, which then counts down to nothing.</summary>
    public const int LifeClockUnits = 2 * ArcadeClock.UnitsPerRomFrame;
}
