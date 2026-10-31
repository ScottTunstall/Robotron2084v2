using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities;

namespace Robotron2084.Graphics;

/// <summary>
///     Pixel-perfect contact over the loaded texture set (notes §118): each sprite's opaque-pixel mask is
///     derived from its texture the first time it is asked for and kept, because a sprite never changes
///     shape — only which sprite an entity is showing does.
/// </summary>
public sealed class SpriteCollision : IPixelCollision
{
    private readonly Dictionary<Texture2D, SpriteMask> _masks = [];

    /// <inheritdoc />
    public SpriteShape? GetShape(IEntity entity)
    {
        if (entity is not IAnimationFrameSource animationFrameSource) return null;

        var animationFrameTexture = animationFrameSource.GetCurrentAnimationFrameTexture();
        return new SpriteShape(GetMask(animationFrameTexture), BlitterDraw.GetDrawnBounds(entity.GetBounds(), animationFrameTexture));
    }

    /// <inheritdoc />
    public bool Overlaps(SpriteShape a, SpriteShape b)
    {
        return SpriteMask.Overlaps(a.SpriteMask, a.DrawnBounds, b.SpriteMask, b.DrawnBounds);
    }

    private SpriteMask GetMask(Texture2D animationFrameTexture)
    {
        if (!_masks.TryGetValue(animationFrameTexture, out var mask))
        {
            mask = SpriteMask.CreateFromTexture(animationFrameTexture);
            _masks[animationFrameTexture] = mask;
        }

        return mask;
    }
}
