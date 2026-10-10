using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Graphics;

/// <summary>
///     Where a <see cref="SpriteSet" />'s textures come from: the content pipeline for the sprites extracted from the
///     ROM, and code for the few sprites the port draws itself. Splitting this out is what lets a
///     <see cref="SpriteSet" /> be built without a graphics device — a test needs entities, not sprites.
/// </summary>
public interface ISpriteSource
{
    /// <summary>Creates a sprite from pixels the port draws itself (the four player laser shapes).</summary>
    /// <param name="width">Sprite width in pixels.</param>
    /// <param name="height">Sprite height in pixels.</param>
    /// <param name="pixels">The pixels, row-major.</param>
    Texture2D Create(int width, int height, Color[] pixels);

    /// <summary>Creates a sprite of one flat colour (the wall pixel).</summary>
    /// <param name="width">Sprite width in pixels.</param>
    /// <param name="height">Sprite height in pixels.</param>
    /// <param name="color">The colour to fill it with.</param>
    Texture2D CreateSolid(int width, int height, Color color);

    /// <summary>Loads one sprite by its content asset name (e.g. <c>Sprites/Skull</c>).</summary>
    /// <param name="assetName">The asset's name in the content pipeline.</param>
    Texture2D Load(string assetName);

    /// <summary>Loads several sprites by asset name, in order — a numbered run, or a font's glyphs.</summary>
    /// <param name="assetNames">The assets' names, in the order the caller wants them.</param>
    Texture2D[] LoadAll(string[] assetNames);
}
