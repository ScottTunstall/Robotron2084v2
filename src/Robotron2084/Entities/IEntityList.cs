using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>
///     The jobs every list of entities can do, whatever kind of entity it holds: update them, draw them, count them
///     and clear out the dead ones.
/// </summary>
/// <remarks>See <see cref="EntityList{T}" />. See <see cref="PlayField" /> for the order these jobs are done in.</remarks>
public interface IEntityList
{
    /// <summary>Every entity in the list, whatever kind it is.</summary>
    IEnumerable<IEntity> Entities { get; }

    /// <summary>Draws the entities, in the order they are in the list.</summary>
    /// <param name="spriteBatch">What the entities are drawn with.</param>
    /// <param name="field">The playfield, which does the drawing of each entity.</param>
    void DrawAll(SpriteBatch spriteBatch, PlayField field);

    /// <summary>Counts the entities that are not dead yet.</summary>
    int GetLiveCount();

    /// <summary>Takes the dead entities out of the list.</summary>
    void PruneDead();

    /// <summary>Moves every entity in the list on by one tick. The playfield does the update of each entity.</summary>
    /// <param name="gameTime">The game's own clock for this tick.</param>
    /// <param name="field">The playfield the entities are on.</param>
    void UpdateAll(GameTime gameTime, PlayField field);
}
