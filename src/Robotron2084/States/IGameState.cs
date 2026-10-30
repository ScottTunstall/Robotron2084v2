using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.States;

/// <summary>A screen of the game.</summary>
public interface IGameState
{
    /// <summary>Draws the screen over whatever is already there.</summary>
    /// <param name="spriteBatch">What the screen is drawn with.</param>
    /// <param name="font">The font for any text on the screen.</param>
    void Draw(SpriteBatch spriteBatch, SpriteFont font);

    /// <summary>Moves the screen on by one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    /// <param name="gameTime">The game's own clock for this <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</param>
    /// <param name="manager">The manager to ask when the screen wants to change to another screen.</param>
    void Update(GameTime gameTime, GameStateManager manager);
}
