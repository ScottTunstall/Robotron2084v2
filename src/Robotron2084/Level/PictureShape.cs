using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Level;

/// <summary>An entity's collision picture: the mask of the picture it is drawn with and the rectangle that picture is drawn in.</summary>
/// <param name="Mask">The picture's opaque pixels.</param>
/// <param name="DrawnBounds">Where the picture is drawn, in screen pixels.</param>
public readonly record struct PictureShape(SpriteMask Mask, Rectangle DrawnBounds);
