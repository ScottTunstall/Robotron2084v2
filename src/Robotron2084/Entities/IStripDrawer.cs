using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Entities;

/// <summary>Something that draws a strip of one of its own animation frames the way it draws itself.</summary>
public interface IStripDrawer
{
    /// <summary>Draws one strip of one of its animation frames, in the colours it draws itself in.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="animationFrameTexture">The animation frame the strip is cut from.</param>
    /// <param name="source">The strip, in the animation frame's own pixels.</param>
    /// <param name="destination">Where the strip goes, in port pixels.</param>
    void DrawStrip(SpriteBatch spriteBatch, Texture2D animationFrameTexture, Rectangle source, Rectangle destination);
}
