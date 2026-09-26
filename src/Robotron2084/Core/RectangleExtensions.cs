using Microsoft.Xna.Framework;

namespace Robotron2084.Core;

/// <summary>Rectangle overlap helper.</summary>
public static class RectangleExtensions
{
    /// <summary>Thin named wrapper over <see cref="Rectangle.Intersects(Rectangle)"/> used everywhere for readability.</summary>
    public static bool Overlaps(this Rectangle a, Rectangle b) => a.Intersects(b);
}
