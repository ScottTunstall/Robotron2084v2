using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Palette;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>The coloured wall around the play area. It is drawn as four strips, and it tells what is touching it.</summary>
/// <remarks>
/// Everything on the field stays inside <see cref="PlayfieldBounds"/>, and the wall fills the space from there out to <see cref="GetOuterBounds()"/>.
/// The game passes in the colour the wave's palette gives the wall (<see cref="PlayField.Draw"/>). The colour cycle is used only when there is no palette,
/// as in a test.
/// </remarks>
public sealed class PlayfieldWall
{
    /// <summary>How thick the wall is, in port pixels. It is added to each side of <see cref="PlayfieldBounds"/> to make <see cref="GetOuterBounds()"/>.</summary>
    public static readonly int Thickness = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.WallThicknessArcadePixels);

    private readonly WallColorCycle _colourCycle;
    private readonly Rectangle _playfieldBounds;

    /// <summary>Makes the wall for a play area.</summary>
    /// <param name="playfieldBounds">The play area inside the wall, in port pixels.</param>
    /// <param name="cycle">The colours the wall cycles through when it is not given one.</param>
    public PlayfieldWall(Rectangle playfieldBounds, WallColorCycle colourCycle)
    {
        _playfieldBounds = playfieldBounds;
        _colourCycle = colourCycle;
    }

    /// <summary>The play area and the wall around it, in port pixels.</summary>
    public Rectangle GetOuterBounds() => new(
        _playfieldBounds.X - Thickness,
        _playfieldBounds.Y - Thickness,
        _playfieldBounds.Width + 2 * Thickness,
        _playfieldBounds.Height + 2 * Thickness);

    /// <summary>The play area inside the wall, in port pixels. Everything on the field must stay wholly inside it.</summary>
    public Rectangle PlayfieldBounds => _playfieldBounds;

    /// <summary>Draws the wall as four coloured strips.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="wallPixel">A single white pixel, which is stretched and tinted to make each strip.</param>
    /// <param name="colorOverride">The colour to draw in, or null to use the wall's own colour cycle.</param>
    public void Draw(SpriteBatch spriteBatch, Texture2D wallPixel, Color? colorOverride = null)
    {
        Rectangle outer = GetOuterBounds();
        Color color = colorOverride ?? _colourCycle.CurrentColor;

        spriteBatch.Draw(wallPixel, new Rectangle(outer.X, outer.Y, outer.Width, Thickness), color);
        spriteBatch.Draw(wallPixel, new Rectangle(outer.X, outer.Bottom - Thickness, outer.Width, Thickness), color);
        spriteBatch.Draw(wallPixel, new Rectangle(outer.X, _playfieldBounds.Y, Thickness, _playfieldBounds.Height), color);
        spriteBatch.Draw(wallPixel, new Rectangle(outer.Right - Thickness, _playfieldBounds.Y, Thickness, _playfieldBounds.Height), color);
    }

    /// <summary>Says whether a box overlaps any part of the wall.</summary>
    /// <param name="bounds">The box to test, in port pixels.</param>
    public bool Intersects(Rectangle bounds) =>
        bounds.Intersects(new Rectangle(GetOuterBounds().X, GetOuterBounds().Y, GetOuterBounds().Width, Thickness)) ||
        bounds.Intersects(new Rectangle(GetOuterBounds().X, GetOuterBounds().Bottom - Thickness, GetOuterBounds().Width, Thickness)) ||
        bounds.Intersects(new Rectangle(GetOuterBounds().X, _playfieldBounds.Y, Thickness, _playfieldBounds.Height)) ||
        bounds.Intersects(new Rectangle(GetOuterBounds().Right - Thickness, _playfieldBounds.Y, Thickness, _playfieldBounds.Height));

    /// <summary>Moves the wall's colour cycle on by one tick.</summary>
    /// <param name="gameTime">The time for this tick.</param>
    public void Update(GameTime gameTime) => _colourCycle.Update(gameTime);
}
