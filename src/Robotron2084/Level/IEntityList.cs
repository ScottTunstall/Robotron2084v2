using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>The passes every list of entities needs, without their element type — what the field walks.</summary>
/// <remarks>See <see cref="EntityList{T}"/>, and <see cref="PlayField"/> for the orders these run in.</remarks>
public interface IEntityList
{
    /// <summary>The entities in the list, as the common type the registry-driven phases walk.</summary>
    IEnumerable<IEntity> Entities { get; }

    /// <summary>Advances every entity in the list one tick, through the field's own per-entity step.</summary>
    /// <param name="gameTime">Elapsed time for this tick.</param>
    /// <param name="field">The field the entities live on and that advances them.</param>
    void UpdateAll(GameTime gameTime, PlayField field);

    /// <summary>Removes the entities that have died (the ROM's list counts decrement, plan 9.1).</summary>
    void PruneDead();

    /// <summary>Draws the list, in the order the entities sit in it.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="field">The field, whose materialisation guards decide what may be drawn.</param>
    void DrawAll(SpriteBatch spriteBatch, PlayField field);
}
