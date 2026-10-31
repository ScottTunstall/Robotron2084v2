using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Graphics;

/// <summary>
///     The game's sprites: the ROM-extracted sprites from the content pipeline, and the handful the port draws
///     itself, which <see cref="TextureFactory" /> builds against the graphics device.
/// </summary>
public sealed class ContentTextureSource : ITextureSource
{
    private readonly ContentManager _contentManager;
    private readonly TextureFactory _textureFactory;

    /// <summary>Wires the source to the game's device and content.</summary>
    /// <param name="graphicsDevice">The graphics device the self-drawn sprites are created on.</param>
    /// <param name="contentManager">The content pipeline the ROM-extracted sprites are loaded from.</param>
    public ContentTextureSource(GraphicsDevice graphicsDevice, ContentManager contentManager)
    {
        _contentManager = contentManager;
        _textureFactory = new TextureFactory(graphicsDevice);
    }

    /// <inheritdoc />
    public Texture2D Create(int width, int height, Color[] pixels)
    {
        return _textureFactory.Create(width, height, pixels);
    }

    /// <inheritdoc />
    public Texture2D CreateSolid(int width, int height, Color colour)
    {
        return _textureFactory.CreateSolid(width, height, colour);
    }

    /// <inheritdoc />
    public Texture2D Load(string assetName)
    {
        return _contentManager.Load<Texture2D>(assetName);
    }

    /// <inheritdoc />
    public Texture2D[] LoadAll(string[] assetNames)
    {
        var sprites = new Texture2D[assetNames.Length];
        for (var i = 0; i < assetNames.Length; i++) sprites[i] = Load(assetNames[i]);

        return sprites;
    }
}
