using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Entities;

/// <summary>An entity that can be blown apart: it gives the bounds and the animation frame to cut into strips.</summary>
public interface IExplodable : IEntity, IAnimationFrameSource
{
    /// <summary>The animation frame that is cut into strips when the entity is blown apart.</summary>
    Texture2D GetExplosionAnimationFrameTexture();

    /// <summary>The bounds the explosion starts in.</summary>
    Rectangle GetExplosionBounds();
}
