using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Rendering;

namespace Robotron2084.Level;

/// <summary>
/// Pixel-perfect contact over the loaded sprite set (notes §118): each picture's opaque-pixel mask is
/// derived from its texture the first time it is asked for and kept, because a picture never changes
/// shape — only which picture an entity is showing does.
/// </summary>
public sealed class SpriteCollision(SpriteSet sprites) : IPixelCollision
{
    private readonly Dictionary<Texture2D, SpriteMask> _masks = [];

    /// <inheritdoc/>
    public PictureShape? ShapeOf(IEntity entity)
    {
        if (entity is not IArtSource art)
        {
            return null;
        }

        Texture2D picture = art.CurrentFrameArt(sprites);
        return new PictureShape(MaskOf(picture), SpriteSet.ArtRect(entity.Bounds, picture));
    }

    /// <inheritdoc/>
    public bool Overlaps(PictureShape a, PictureShape b) => SpriteMask.Overlap(a.Mask, a.DrawnBounds, b.Mask, b.DrawnBounds);

    private SpriteMask MaskOf(Texture2D picture)
    {
        if (!_masks.TryGetValue(picture, out SpriteMask? mask))
        {
            mask = SpriteMask.FromTexture(picture);
            _masks[picture] = mask;
        }

        return mask;
    }
}
