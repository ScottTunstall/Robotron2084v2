using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Entities;

/// <summary>Something that can hand over the animation frame it is showing.</summary>
public interface IAnimationFrameSource
{
    /// <summary>The animation frame this entity is showing right now.</summary>
    Texture2D GetCurrentAnimationFrameTexture();
}
