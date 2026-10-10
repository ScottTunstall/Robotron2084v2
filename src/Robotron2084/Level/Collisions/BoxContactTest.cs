using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>Two things are touching when the boxes around them overlap. Used when there are no sprites to compare.</summary>
public sealed class BoxContactTest : IContactTest
{
    /// <inheritdoc />
    public bool Touches(IEntity a, IEntity b)
    {
        return a.GetBounds().Intersects(b.GetBounds());
    }
}
