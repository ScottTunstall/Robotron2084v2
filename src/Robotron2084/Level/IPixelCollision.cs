using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>
/// The contact test the playfield uses between two entities (notes §118): the opaque pixels of the art
/// they are drawn with, rather than their collision boxes.
/// </summary>
/// <remarks>
/// ROM: <c>COL0</c>, called against a process list — <c>LDU OPICT,X / LDX #HPTR / JSR COL0</c> is the
/// hulk's test against the humans (RRH11) and every robot's own entry uses it; its body lives in the R5's
/// fixed block at $D85C. It walks the two objects' picture bytes and skips a zero byte as transparent.
/// The field falls back to the boxes when it has no test at all (a headless test) or when an entity shows
/// no picture of its own (the cruise missile is drawn as solid pixels — its ROM picture exists only to
/// size the box).
/// </remarks>
public interface IPixelCollision
{
    /// <summary>The picture an entity is drawn with, or null when it shows no picture of its own.</summary>
    /// <param name="entity">The entity to describe.</param>
    PictureShape? ShapeOf(IEntity entity);

    /// <summary>True when the two pictures cover a common screen pixel.</summary>
    /// <param name="a">One entity's picture.</param>
    /// <param name="b">The other entity's picture.</param>
    bool Overlaps(PictureShape a, PictureShape b);
}
