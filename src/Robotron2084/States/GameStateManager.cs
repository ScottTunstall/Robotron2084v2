using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.States;

/// <summary>Holds the current state and forwards update/draw; transitions replace <see cref="CurrentGameState" />.</summary>
public sealed class GameStateManager
{
    public GameStateManager(IGameState firstGameState)
    {
        CurrentGameState = firstGameState;
    }

    public IGameState CurrentGameState { get; private set; }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        CurrentGameState.Draw(spriteBatch, font);
    }

    public void TransitionTo(IGameState nextGameState)
    {
        CurrentGameState = nextGameState;
    }

    public void Update(GameTime gameTime)
    {
        CurrentGameState.Update(gameTime, this);
    }
}
