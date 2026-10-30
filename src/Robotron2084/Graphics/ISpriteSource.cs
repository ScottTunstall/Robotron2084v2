using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Graphics;

/// <summary>Makes sprites from pixels, and loads sprites by name.</summary>
public interface ISpriteSource
{
    /// <summary>Makes a sprite from the given pixels.</summary>
    /// <param name="width">Sprite width in pixels.</param>
    /// <param name="height">Sprite height in pixels.</param>
    /// <param name="pixels">The pixels, row by row from the top.</param>
    Texture2D Create(int width, int height, Color[] pixels);

    /// <summary>Makes a sprite of one flat colour.</summary>
    /// <param name="width">Sprite width in pixels.</param>
    /// <param name="height">Sprite height in pixels.</param>
    /// <param name="color">The colour to fill it with.</param>
    Texture2D CreateSolid(int width, int height, Color color);

    /// <summary>Loads one sprite by its content asset name (e.g. <c>Sprites/Skull</c>).</summary>
    /// <param name="assetName">The asset's name in the content pipeline.</param>
    Texture2D Load(string assetName);

    /// <summary>Loads several sprites by asset name.</summary>
    /// <param name="assetNames">The assets' names.</param>
    /// <returns>The sprites, in the same order as <paramref name="assetNames" />.</returns>
    Texture2D[] LoadAll(string[] assetNames);
}
