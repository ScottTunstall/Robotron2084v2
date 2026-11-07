using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Palette;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>The coloured wall around the play area. It is drawn as four strips, and it tells what is touching it.</summary>
/// <remarks>
///     Everything on the field stays inside <see cref="PlayfieldBounds" />, and the wall fills the space from there out to
///     <see cref="GetOuterBounds()" />.
///     The game passes in the colour the wave's palette gives the wall (<see cref="PlayField.Draw" />). The colour cycle
///     is used only when there is no palette,
///     as in a test.
/// </remarks>
public sealed class PlayfieldWall
{
    /// <summary>
    ///     How thick the wall is, in port pixels. It is added to each side of <see cref="PlayfieldBounds" /> to make
    ///     <see cref="GetOuterBounds()" />.
    /// </summary>
    public static readonly int Thickness =
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.WallThicknessArcadePixels);

    /// <summary>How much lighter than the wall a door's panels are, from 0 (the wall's own colour) to 1 (white).</summary>
    private const float PanelBrightening = 0.35f;

    private readonly WallColourCycle _wallColourCycle;
    private readonly Rectangle _playfieldBounds;

    /// <summary>Makes the wall for a play area.</summary>
    /// <param name="playfieldBounds">The play area inside the wall, in port pixels.</param>
    /// <param name="cycle">The colours the wall cycles through when it is not given one.</param>
    public PlayfieldWall(Rectangle playfieldBounds, WallColourCycle wallColourCycle)
    {
        _playfieldBounds = playfieldBounds;
        _wallColourCycle = wallColourCycle;
    }

    /// <summary>The play area inside the wall, in port pixels. Everything on the field must stay wholly inside it.</summary>
    public Rectangle PlayfieldBounds => _playfieldBounds;

    /// <summary>Draws the wall as four coloured strips, with a gap in a strip where one of its doors is open.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="wallPixelTexture">A single white pixel, which is stretched and tinted to make each strip.</param>
    /// <param name="colourOverride">The colour to draw in, or null to use the wall's own colour cycle.</param>
    /// <param name="doors">
    ///     The doors in the wall (port only, notes §151), or null for none. The two panels of a door are drawn a little lighter
    ///     than the wall, so they read as a door.
    /// </param>
    public void Draw(SpriteBatch spriteBatch, Texture2D wallPixelTexture, Color? colourOverride = null,
        IReadOnlyList<WallDoor>? doors = null)
    {
        var colour = colourOverride ?? _wallColourCycle.CurrentColour;
        var doorList = doors ?? [];

        foreach (var piece in GetWallPieces(doorList)) spriteBatch.Draw(wallPixelTexture, piece, colour);

        var panelColour = Color.Lerp(colour, Color.White, PanelBrightening);
        foreach (var panel in GetDoorPanels(doorList)) spriteBatch.Draw(wallPixelTexture, panel, panelColour);
    }

    /// <summary>Gets the rectangles of the wall itself: each of the four strips, cut where a door is.</summary>
    /// <param name="doors">The doors in the wall.</param>
    /// <returns>The rectangles, in port pixels.</returns>
    /// <remarks>
    ///     A door cuts out the whole length of its opening, whether it is shut or open. The door's own panels
    ///     (<see cref="GetDoorPanels" />) fill the part that is not yet open.
    /// </remarks>
    public IReadOnlyList<Rectangle> GetWallPieces(IReadOnlyList<WallDoor> doors)
    {
        var pieces = new List<Rectangle>();
        foreach (var side in Enum.GetValues<WallDoorSide>())
        {
            var strip = GetStrip(side);
            var isAlongX = IsAlongX(side);
            var cursor = GetStart(strip, isAlongX);
            var stripEnd = GetEnd(strip, isAlongX);
            foreach (var door in doors.Where(door => door.Side == side).OrderBy(door => door.GetStart()))
            {
                AddPiece(pieces, strip, isAlongX, cursor, door.GetStart());
                cursor = door.GetEnd();
            }

            AddPiece(pieces, strip, isAlongX, cursor, stripEnd);
        }

        return pieces;
    }

    /// <summary>Gets the rectangles of the two panels of each door: what is left of the shut door after it has slid open.</summary>
    /// <param name="doors">The doors in the wall.</param>
    /// <returns>The rectangles, in port pixels. A door that is wide open has none.</returns>
    /// <remarks>
    ///     The panels slide apart from the middle of the opening, each into the wall at its own end of the opening, so
    ///     they shorten as the door opens and grow as it shuts.
    /// </remarks>
    public IReadOnlyList<Rectangle> GetDoorPanels(IReadOnlyList<WallDoor> doors)
    {
        var panels = new List<Rectangle>();
        foreach (var door in doors)
        {
            var strip = GetStrip(door.Side);
            var isAlongX = IsAlongX(door.Side);
            var panelLength = (door.GapLength - door.GetOpenLength()) / 2;
            AddPiece(panels, strip, isAlongX, door.GetStart(), door.GetStart() + panelLength);
            AddPiece(panels, strip, isAlongX, door.GetEnd() - panelLength, door.GetEnd());
        }

        return panels;
    }

    /// <summary>Gets the strip of wall on one side, in port pixels.</summary>
    /// <param name="side">The side.</param>
    private Rectangle GetStrip(WallDoorSide side)
    {
        var outer = GetOuterBounds();
        return side switch
        {
            WallDoorSide.North => new Rectangle(outer.X, outer.Y, outer.Width, Thickness),
            WallDoorSide.South => new Rectangle(outer.X, outer.Bottom - Thickness, outer.Width, Thickness),
            WallDoorSide.West => new Rectangle(outer.X, _playfieldBounds.Y, Thickness, _playfieldBounds.Height),
            _ => new Rectangle(outer.Right - Thickness, _playfieldBounds.Y, Thickness, _playfieldBounds.Height)
        };
    }

    /// <summary>Gets whether a side's strip runs along the X axis (the top and bottom walls) rather than the Y axis.</summary>
    private static bool IsAlongX(WallDoorSide side)
    {
        return side is WallDoorSide.North or WallDoorSide.South;
    }

    private static int GetStart(Rectangle strip, bool isAlongX)
    {
        if (isAlongX) return strip.X;

        return strip.Y;
    }

    private static int GetEnd(Rectangle strip, bool isAlongX)
    {
        if (isAlongX) return strip.Right;

        return strip.Bottom;
    }

    /// <summary>Adds the part of a strip between two places along it, if it is not empty.</summary>
    private static void AddPiece(List<Rectangle> pieces, Rectangle strip, bool isAlongX, int from, int to)
    {
        if (to <= from) return;

        if (isAlongX) pieces.Add(new Rectangle(from, strip.Y, to - from, strip.Height));
        else pieces.Add(new Rectangle(strip.X, from, strip.Width, to - from));
    }

    /// <summary>The play area and the wall around it, in port pixels.</summary>
    public Rectangle GetOuterBounds()
    {
        return new Rectangle(
            _playfieldBounds.X - Thickness,
            _playfieldBounds.Y - Thickness,
            _playfieldBounds.Width + 2 * Thickness,
            _playfieldBounds.Height + 2 * Thickness);
    }

    /// <summary>Says whether a box overlaps any part of the wall.</summary>
    /// <param name="bounds">The box to test, in port pixels.</param>
    public bool Intersects(Rectangle bounds)
    {
        return bounds.Intersects(new Rectangle(GetOuterBounds().X, GetOuterBounds().Y, GetOuterBounds().Width,
                   Thickness)) ||
               bounds.Intersects(new Rectangle(GetOuterBounds().X, GetOuterBounds().Bottom - Thickness,
                   GetOuterBounds().Width, Thickness)) ||
               bounds.Intersects(new Rectangle(GetOuterBounds().X, _playfieldBounds.Y, Thickness,
                   _playfieldBounds.Height)) ||
               bounds.Intersects(new Rectangle(GetOuterBounds().Right - Thickness, _playfieldBounds.Y, Thickness,
                   _playfieldBounds.Height));
    }

    /// <summary>Moves the wall's colour cycle on by one tick.</summary>
    /// <param name="gameTime">The time for this tick.</param>
    public void Update(GameTime gameTime)
    {
        _wallColourCycle.Update(gameTime);
    }
}
