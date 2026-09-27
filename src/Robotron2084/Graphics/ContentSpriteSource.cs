using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Graphics;

/// <summary>
/// The game's pictures: the ROM-extracted pictures from the content pipeline, and the handful the port draws
/// itself, which <see cref="PictureFactory"/> builds against the graphics device.
/// </summary>
public sealed class ContentSpriteSource : ISpriteSource
{
    private readonly ContentManager _content;
    private readonly PictureFactory _factory;

    /// <summary>Wires the source to the game's device and content.</summary>
    /// <param name="device">The graphics device the self-drawn pictures are created on.</param>
    /// <param name="content">The content pipeline the ROM-extracted pictures are loaded from.</param>
    public ContentSpriteSource(GraphicsDevice device, ContentManager content)
    {
        _content = content;
        _factory = new PictureFactory(device);
    }

    /// <inheritdoc/>
    public Texture2D Create(int width, int height, Color[] pixels) => _factory.Create(width, height, pixels);

    /// <inheritdoc/>
    public Texture2D CreateSolid(int width, int height, Color color) => _factory.CreateSolid(width, height, color);

    /// <inheritdoc/>
    public Texture2D Load(string assetName) => _content.Load<Texture2D>(assetName);

    /// <inheritdoc/>
    public Texture2D[] LoadAll(string[] assetNames)
    {
        var pictures = new Texture2D[assetNames.Length];
        for (int i = 0; i < assetNames.Length; i++)
        {
            pictures[i] = Load(assetNames[i]);
        }

        return pictures;
    }
}
