using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Entities;

/// <summary>An entity that can be blown apart: it gives the box and the sprite to cut into strips.</summary>
public interface IExplodable : IEntity, IAnimationFrameSource
{
    /// <summary>The sprite that is cut into strips when the entity is blown apart.</summary>
    Texture2D GetExplosionAnimationFrame();

    /// <summary>The box the explosion starts in.</summary>
    Rectangle GetExplosionBounds();
}
