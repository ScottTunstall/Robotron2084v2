using Microsoft.Xna.Framework;

namespace Robotron2084.Graphics;

/// <summary>An entity's collision picture: the mask of the picture it is drawn with and the rectangle that picture is drawn in.</summary>
/// <param name="Mask">The picture's opaque pixels.</param>
/// <param name="DrawnBounds">Where the picture is drawn, in screen pixels.</param>
public readonly record struct PictureShape(SpriteMask Mask, Rectangle DrawnBounds);
