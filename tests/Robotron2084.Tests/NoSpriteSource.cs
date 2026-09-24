using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Tests;

/// <summary>
/// The artwork a headless test builds its entities with. A test process has no graphics device, so nothing
/// can be loaded: every handle comes back null, but a numbered run still comes back with the RIGHT NUMBER of
/// slots, because the entities index into those runs. Enough to construct and run an entity, never enough to
/// draw one — nothing in the suite draws, and no test asks an entity for its frame.
/// </summary>
internal sealed class NoSpriteSource : ISpriteSource
{
    /// <inheritdoc/>
    public Texture2D Load(string assetName) => null!;

    /// <inheritdoc/>
    public Texture2D[] LoadAll(string[] assetNames) => new Texture2D[assetNames.Length];

    /// <inheritdoc/>
    public Texture2D Create(int width, int height, Color[] pixels) => null!;

    /// <inheritdoc/>
    public Texture2D CreateSolid(int width, int height, Color color) => null!;
}
