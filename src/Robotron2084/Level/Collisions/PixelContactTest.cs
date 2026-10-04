using Robotron2084.Entities;
using Robotron2084.Graphics;

namespace Robotron2084.Level.Collisions;

/// <summary>Two things are touching when their sprites share a lit pixel, as in the arcade. Something with no sprite of its own is tested by its box.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRS22.ASM</c> <c>COL0V</c> (reached through the <c>COL0</c> vector), which walks the two sprites' bytes and skips a zero byte as see-through (notes §118)</item>
/// <item>Disassembly: the fixed block at <c>$D85C</c></item>
/// </list>
/// The cruise missile is the one thing with no sprite of its own: it is drawn as solid pixelCollision, so its box is used.
/// </remarks>
public sealed class PixelContactTest : IContactTest
{
    private readonly BoxContactTest _boxContactTest = new();
    private readonly IPixelCollision _pixelCollision;

    /// <summary>Makes the test from the sprite shapes it will compare.</summary>
    /// <param name="pixelCollision">Supplies each entity's sprite shape and compares two of them.</param>
    public PixelContactTest(IPixelCollision pixelCollision) => _pixelCollision = pixelCollision;

    /// <inheritdoc/>
    public bool Touches(IEntity a, IEntity b)
    {
        if (_pixelCollision.GetShape(a) is { } shapeA && _pixelCollision.GetShape(b) is { } shapeB)
        {
            return _pixelCollision.Overlaps(shapeA, shapeB);
        }

        return _boxContactTest.Touches(a, b);
    }
}
