using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>
/// Contract for every playfield entity (spec: "One c# class AT LEAST per
/// entity"). <see cref="PlayField"/> (the central hub) is passed into
/// <see cref="Update"/> so entities can query the player, walls, input, and
/// spawn new entities without any global singleton.
/// </summary>
public interface IEntity
{
    /// <summary>Top-left corner of the entity (top-left convention is used by every entity).</summary>
    IntVector2 Position { get; }

    /// <summary>Collision/draw rectangle.</summary>
    Rectangle Bounds { get; }

    EntityLifeState LifeState { get; }

    void Update(GameTime gameTime, PlayField field);

    /// <summary>
    /// Draws the entity. Takes the shared <see cref="SpriteSet"/> so entities
    /// can stay free of statics and unit-testable (Texture2D requires a
    /// GraphicsDevice, so textures can't be baked into entity constructors in
    /// tests — plan 9.4 threads the SpriteSet into PlayField.Draw, which
    /// forwards it here).
    /// </summary>
    void Draw(SpriteBatch spriteBatch, SpriteSet sprites);
}
