using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>Something on the playfield. It is updated on each tick, it can be drawn, and it can be taken off the field.</summary>
/// <seealso cref="PlayField"/>
/// <remarks>
/// The arcade has nothing like this. It keeps its objects in lists, and 50 times a second it runs each
/// object's own routine. <see cref="Update"/> stands for that call. In the disassembly, the list that the
/// spheroid, enforcer, quark, spark and shell share is <c>object_metadata_list_2_pointer</c> (<c>$9813</c>).
///
/// A tick is one go round this game's loop, which happens 60 times a second. A beat is an entity's own turn
/// to think and move, which comes every few ticks. <see cref="ArcadeClock"/> explains how the two are timed.
///
/// The names of source files and labels come from the 1982 Williams listing in <c>ref/original-source/</c>.
/// "notes §NN" points at a section of <c>docs/arcade-fidelity-notes.md</c>.
/// </remarks>
public interface IEntity
{
    /// <summary>The box used to tell what the entity touches. For most entities it is also the box the entity is drawn in.</summary>
    Rectangle GetBounds();

    /// <summary>Where the entity is in its life (see <see cref="EntityLifeState"/>).</summary>
    EntityLifeState LifeState { get; }

    /// <summary>Where the entity's top-left corner is on the screen.</summary>
    IntVector2 Position { get; }

    /// <summary>Draws the entity where it is now.</summary>
    /// <param name="spriteBatch">What the entity is drawn with.</param>
    void Draw(SpriteBatch spriteBatch);

    /// <summary>Moves the entity on by one tick. The playfield calls this on every tick, except during the short freeze just after the player is killed, and while a robot is still appearing at the start of a wave. The entity decides for itself whether it moves or has a beat on that tick.</summary>
    /// <param name="gameTime">The game's own clock for this tick.</param>
    /// <param name="field">The playfield the entity is on.</param>
    void Update(GameTime gameTime, PlayField field);
}
