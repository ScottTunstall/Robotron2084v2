using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>One way of deciding whether two things on the field are touching.</summary>
public interface IContactTest
{
    /// <summary>Says whether the two entities are touching.</summary>
    /// <param name="firstEntity">One entity.</param>
    /// <param name="secondEntity">The other entity.</param>
    bool Touches(IEntity firstEntity, IEntity secondEntity);
}
