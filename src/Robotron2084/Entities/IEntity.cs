using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>Something on the playfield that has a place and a size, can be drawn, and can be moved on in time.</summary>
/// <remarks>
///     The names of source files and labels come from the 1982 Williams listing in <c>ref/original-source/</c>.
///     "notes §NN" points at a section of <c>docs/arcade-fidelity-notes.md</c>.
/// </remarks>
public interface IEntity
{
    /// <summary>Where the entity is in its life (see <see cref="EntityLifeState" />).</summary>
    EntityLifeState LifeState { get; }

    /// <summary>Where the entity's top-left corner is on the screen.</summary>
    IntVector2 Position { get; }

    /// <summary>The box used to tell what the entity touches.</summary>
    Rectangle GetBounds();

    /// <summary>Draws the entity where it is now.</summary>
    /// <param name="spriteBatch">What the entity is drawn with.</param>
    void Draw(SpriteBatch spriteBatch);

    /// <summary>Moves the entity on by one <see cref="ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    /// <param name="gameTime">The game's own clock for this <see cref="ArcadeClock.PortTicksPerSecond">tick</see>.</param>
    /// <param name="field">The playfield the entity is on.</param>
    void Update(GameTime gameTime, PlayField field);
}
