using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>One way of deciding whether two things on the field are touching.</summary>
/// <remarks>The arcade compares the lit pixels of the two sprites (<see cref="PixelContactTest"/>). A test with no
/// sprites to look at compares their boxes instead (<see cref="BoxContactTest"/>).</remarks>
public interface IContactTest
{
    /// <summary>Says whether the two entities are touching.</summary>
    /// <param name="a">One entity.</param>
    /// <param name="b">The other entity.</param>
    bool Touches(IEntity a, IEntity b);
}
