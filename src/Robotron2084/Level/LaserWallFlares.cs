using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// The laser-vs-wall flares (RRG23 <c>LASDIE</c> → <c>LASDIH</c>/<c>LASDIV</c>, notes §63): where a laser runs off
/// the playfield the ROM paints the end pixels in the wave's LASCOL slot for two ROM frames, then repaints them in
/// WALCOL. The LEFT/RIGHT walls use <c>LASDIH</c> (a SOLID fill); the TOP/BOTTOM walls use <c>LASDIV</c>, which ANDs
/// WALCOL's high nibble onto LASCOL's low nibble — a dither, so the wall shows through alternate rows.
/// </summary>
internal sealed class LaserWallFlares
{
    /// <summary>The height of the LASCOL bands a dithered flare draws.</summary>
    private static readonly int DitherBandHeight = ScreenSize.ToPortPixels(2);

    /// <summary>Two bytes of video memory: 4 rows of arcade pixels along the wall.</summary>
    private static readonly int FlareLength = ScreenSize.ToPortPixelsFromArcade(4);

    /// <summary>Two bytes of video memory: 2 columns of arcade pixels across the wall.</summary>
    private static readonly int FlareThickness = ScreenSize.ToPortPixelsFromColumns(2);

    private readonly List<LaserWallFlare> _flares = [];

    /// <summary>The live flares.</summary>
    public IReadOnlyList<LaserWallFlare> Flares => _flares;

    /// <summary>Paints the flares OVER the wall, in the wave's LASCOL slot, as the ROM writes those pixels.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set that draws the solid rectangles.</param>
    /// <param name="levelNumber">The wave, which picks the LASCOL slot.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites, int levelNumber)
    {
        if (_flares.Count == 0)
        {
            return;
        }

        Color flareColor = sprites.Blitter.GetSlotColour(WavePaletteTables.GetLaserWallSlot(levelNumber));
        foreach (LaserWallFlare flare in _flares)
        {
            if (!flare.Dithered)
            {
                sprites.Blitter.DrawSolidRectangle(spriteBatch, flare.Bounds, flareColor);
                continue;
            }

            // LASDIV: the ROM's mixed nibble — one band in LASCOL, the next left as WALCOL. Draw only the LASCOL bands.
            for (int y = flare.Bounds.Y; y < flare.Bounds.Bottom; y += DitherBandHeight * 2)
            {
                sprites.Blitter.DrawSolidRectangle(
                    spriteBatch,
                    new Rectangle(flare.Bounds.X, y, flare.Bounds.Width, DitherBandHeight),
                    flareColor);
            }
        }
    }

    /// <summary>Starts a flare where a laser ran off the playfield.</summary>
    /// <param name="laserBounds">The laser's box when it hit the wall.</param>
    /// <param name="direction">The laser's direction, which picks the wall when the box alone does not.</param>
    /// <param name="wall">The playfield's wall.</param>
    public void Spawn(Rectangle laserBounds, Direction8 direction, PlayfieldWall wall)
    {
        Rectangle inner = wall.PlayfieldBounds;
        Rectangle outer = wall.OuterBounds;

        // Which wall did it cross? A laser whose bounds are outside the TOP/BOTTOM edges died against a horizontal
        // wall (the ROM's LASDIV, dithered); one outside the LEFT/RIGHT edges died against a vertical wall (LASDIH).
        bool horizontalWall = laserBounds.Top < inner.Top || laserBounds.Bottom > inner.Bottom;
        bool verticalWall = laserBounds.Left < inner.Left || laserBounds.Right > inner.Right;
        if (!horizontalWall && !verticalWall)
        {
            // Only called when the wall really was hit. Fall back to the travel axis so a caller mistake cannot
            // paint a flare in mid-air.
            horizontalWall = direction is Direction8.Up or Direction8.Down;
        }

        Rectangle bounds;
        if (horizontalWall)
        {
            int y = laserBounds.Top < inner.Top ? outer.Y : outer.Bottom - FlareThickness;
            int x = Math.Clamp(laserBounds.Center.X - FlareThickness / 2, inner.Left, inner.Right - FlareThickness);
            bounds = new Rectangle(x, y, FlareThickness, FlareLength);
        }
        else
        {
            int x = laserBounds.Left < inner.Left ? outer.X : outer.Right - FlareThickness;
            int y = Math.Clamp(laserBounds.Center.Y - FlareThickness / 2, inner.Top, inner.Bottom - FlareLength);
            bounds = new Rectangle(x, y, FlareThickness, FlareLength);
        }

        _flares.Add(new LaserWallFlare(bounds, Dithered: horizontalWall));
    }

    /// <summary>
    /// Runs every flare's clock down and drops the finished ones. The field calls this at the top of its tick, so a
    /// flare spawned by a laser later in the same tick still gets its full two frames.
    /// </summary>
    public void Update()
    {
        for (int i = _flares.Count - 1; i >= 0; i--)
        {
            LaserWallFlare flare = _flares[i] with
            {
                RemainingClockUnits = _flares[i].RemainingClockUnits - ArcadeClock.UnitsPerPortTick,
            };
            if (flare.RemainingClockUnits <= 0)
            {
                _flares.RemoveAt(i);
            }
            else
            {
                _flares[i] = flare;
            }
        }
    }
}
