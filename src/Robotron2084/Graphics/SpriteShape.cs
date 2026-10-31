using Microsoft.Xna.Framework;

namespace Robotron2084.Graphics;

/// <summary>
///     An entity's collision sprite: the mask of the sprite it is drawn with and the rectangle that sprite is drawn
///     in.
/// </summary>
/// <param name="SpriteMask">The sprite's opaque pixels.</param>
/// <param name="DrawnBounds">Where the sprite is drawn, in screen pixels.</param>
public readonly record struct SpriteShape(SpriteMask SpriteMask, Rectangle DrawnBounds);
