using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// The colour-cycling 4px (spec) border around the play area, rendered as a
/// ring of strips built from a 1x1 white pixel tinted per draw call.
/// Entities live inside <see cref="PlayfieldBounds"/>; the ring itself spans
/// from there out to <see cref="OuterBounds"/>.
/// </summary>
public sealed class PlayfieldWall
{
    /// <summary>Wall strip thickness: spec's 4px widened by SpecScale (8 internal px at 2x).</summary>
    public static readonly int Thickness = ScreenSize.Scaled(GameplayConstants.WallThicknessSpecPixels);

    private readonly Rectangle _playfieldBounds;
    private readonly WallColorCycle _cycle;

    public PlayfieldWall(Rectangle playfieldBounds, WallColorCycle cycle)
    {
        _playfieldBounds = playfieldBounds;
        _cycle = cycle;
    }

    /// <summary>The inner play area (entities must stay fully inside this).</summary>
    public Rectangle PlayfieldBounds => _playfieldBounds;

    /// <summary>The playfield expanded by the wall on all sides.</summary>
    public Rectangle OuterBounds => new(
        _playfieldBounds.X - Thickness,
        _playfieldBounds.Y - Thickness,
        _playfieldBounds.Width + 2 * Thickness,
        _playfieldBounds.Height + 2 * Thickness);

    public void Update(GameTime gameTime) => _cycle.Update(gameTime);

    public void Draw(SpriteBatch spriteBatch, Texture2D wallPixel, Color? colorOverride = null)
    {
        Rectangle outer = OuterBounds;
        Color color = colorOverride ?? _cycle.CurrentColor;

        spriteBatch.Draw(wallPixel, new Rectangle(outer.X, outer.Y, outer.Width, Thickness), color);
        spriteBatch.Draw(wallPixel, new Rectangle(outer.X, outer.Bottom - Thickness, outer.Width, Thickness), color);
        spriteBatch.Draw(wallPixel, new Rectangle(outer.X, _playfieldBounds.Y, Thickness, _playfieldBounds.Height), color);
        spriteBatch.Draw(wallPixel, new Rectangle(outer.Right - Thickness, _playfieldBounds.Y, Thickness, _playfieldBounds.Height), color);
    }

    /// <summary>True if <paramref name="bounds"/> overlaps any of the 4 border strips.</summary>
    public bool Intersects(Rectangle bounds) =>
        bounds.Overlaps(new Rectangle(OuterBounds.X, OuterBounds.Y, OuterBounds.Width, Thickness)) ||
        bounds.Overlaps(new Rectangle(OuterBounds.X, OuterBounds.Bottom - Thickness, OuterBounds.Width, Thickness)) ||
        bounds.Overlaps(new Rectangle(OuterBounds.X, _playfieldBounds.Y, Thickness, _playfieldBounds.Height)) ||
        bounds.Overlaps(new Rectangle(OuterBounds.Right - Thickness, _playfieldBounds.Y, Thickness, _playfieldBounds.Height));
}
