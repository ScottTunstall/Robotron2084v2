using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.States;

/// <summary>A screen of the game (title / playing / wave-clear / game-over / high-score entry).</summary>
public interface IGameState
{
    void Update(GameTime gameTime, GameStateManager manager);

    /// <summary>Draws the state's content onto whatever the caller has already cleared (spec: states clear to black themselves via the caller).</summary>
    void Draw(SpriteBatch spriteBatch, SpriteFont font);
}
