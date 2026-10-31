using Robotron2084.Entities;

namespace Robotron2084.Graphics;

/// <summary>Tells whether two sprites cover a common screen pixel, and which sprite an entity is drawn with.</summary>
public interface IPixelCollision
{
    /// <summary>The sprite an entity is drawn with, or null when it shows no sprite of its own.</summary>
    /// <param name="entity">The entity to describe.</param>
    SpriteShape? GetShape(IEntity entity);

    /// <summary>True when the two sprites cover a common screen pixel.</summary>
    /// <param name="a">One entity's sprite.</param>
    /// <param name="b">The other entity's sprite.</param>
    bool Overlaps(SpriteShape a, SpriteShape b);
}
