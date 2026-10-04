using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Keeps the flashes of colour where lasers have run into the wall, draws them, and takes them away when they are done.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>LASDIE</c>, which calls <c>LASDIH</c> for the side walls and <c>LASDIV</c> for the top and bottom</item>
/// <item>Disassembly: not separately labelled</item>
/// </list>
/// Where a laser runs off the field, the arcade paints the last pixels in the wave's laser wall colour (<c>LASCOL</c>) for two ROM frames, then paints
/// them back in the wall's colour (<c>WALCOL</c>). A side wall is painted solid. On the top and bottom walls the colours are mixed, so the wall shows through every
/// other row (notes §63).
/// </remarks>
internal sealed class LaserWallFlares
{
    /// <summary>The height of each coloured band in a flash on the top or bottom wall, in port pixels.</summary>
    private static readonly int DitherBandHeight = ScreenSize.ToPortPixels(2);

    /// <summary>How long a flash is, along the wall, in port pixels.</summary>
    /// <remarks>It is two bytes of the arcade's video memory.</remarks>
    private static readonly int FlareLength = ScreenSize.ToPortPixels(4);

    /// <summary>How thick a flash is, across the wall, in port pixels.</summary>
    /// <remarks>It is two bytes of the arcade's video memory.</remarks>
    private static readonly int FlareThickness = ScreenSize.ToPortPixelsFromColumns(2);

    /// <summary>The flashes that are showing.</summary>
    private readonly List<LaserWallFlare> _flares = [];

    /// <summary>The flashes that are showing.</summary>
    public IReadOnlyList<LaserWallFlare> Flares => _flares;

    /// <summary>Draws the flashes over the wall, in the colour the wave gives them.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set, which draws the solid rectangles.</param>
    /// <param name="levelNumber">The wave, which picks the colour.</param>
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

    /// <summary>Starts a flash where a laser ran into the wall.</summary>
    /// <param name="laserBounds">The laser's box when it hit the wall.</param>
    /// <param name="direction">The way the laser was going, which picks the wall when the box alone does not.</param>
    /// <param name="wall">The wall round the playfield.</param>
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

    /// <summary>Counts every flash's time down by one tick and takes away the ones that are done. The field does this at the start of its tick, so a flash made later in the same tick still lasts its full time.</summary>
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
