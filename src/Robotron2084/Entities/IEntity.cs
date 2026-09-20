using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>Something on the playfield: updates once a tick, can be drawn, and can be taken off.</summary>
/// <seealso cref="PlayField"/>
/// <remarks>
/// The arcade has no such interface: it calls each object's own 6809 routine once a frame, from
/// linked-list metadata records. The list the spheroid, enforcer, quark, spark and shell share is
/// the one behind <c>object_metadata_list_2_pointer</c> ($9813); <see cref="Update"/> is that call.
///
/// Timers count 5 per tick and 6 per arcade frame, so an interval of N frames is due at 6 x N. A
/// "beat" is one pass of an entity's own update routine.
///
/// Source file and label names come from the 1982 Williams listing in <c>ref/original-source/</c>;
/// "notes §NN" points at a section of <c>docs/arcade-fidelity-notes.md</c>.
/// </remarks>
public interface IEntity
{
    /// <summary>Top-left corner of the entity on screen.</summary>
    IntVector2 Position { get; }

    /// <summary>Collision and draw rectangle; the player's hit box is the exception.</summary>
    Rectangle Bounds { get; }

    /// <summary>Where the entity is in its life cycle — see <see cref="EntityLifeState"/>.</summary>
    EntityLifeState LifeState { get; }

    /// <summary>Advances the entity by one game tick.</summary>
    /// <param name="gameTime">Elapsed time for this tick.</param>
    /// <param name="field">The playfield the entity is on.</param>
    void Update(GameTime gameTime, PlayField field);

    /// <summary>Draws the entity at its current position.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    void Draw(SpriteBatch spriteBatch, SpriteSet sprites);
}
