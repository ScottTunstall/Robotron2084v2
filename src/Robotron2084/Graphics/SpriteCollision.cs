using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities;

namespace Robotron2084.Graphics;

/// <summary>
/// Pixel-perfect contact over the loaded sprite set (notes §118): each sprite's opaque-pixel mask is
/// derived from its texture the first time it is asked for and kept, because a sprite never changes
/// shape — only which sprite an entity is showing does.
/// </summary>
public sealed class SpriteCollision : IPixelCollision
{
    private readonly Dictionary<Texture2D, SpriteMask> _masks = [];

    /// <inheritdoc/>
    public bool Overlaps(SpriteShape a, SpriteShape b) => SpriteMask.Overlap(a.Mask, a.DrawnBounds, b.Mask, b.DrawnBounds);

    /// <inheritdoc/>
    public SpriteShape? GetShape(IEntity entity)
    {
        if (entity is not IAnimationFrameSource frameSource)
        {
            return null;
        }

        Texture2D animationFrame = frameSource.GetCurrentAnimationFrame();
        return new SpriteShape(GetMask(animationFrame), BlitterDraw.DrawnRect(entity.Bounds, animationFrame));
    }

    private SpriteMask GetMask(Texture2D animationFrame)
    {
        if (!_masks.TryGetValue(animationFrame, out SpriteMask? mask))
        {
            mask = SpriteMask.CreateFromTexture(animationFrame);
            _masks[animationFrame] = mask;
        }

        return mask;
    }
}
