using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.States;

/// <summary>Holds the current state and forwards update/draw; transitions replace <see cref="Current"/>.</summary>
public sealed class GameStateManager
{
    public GameStateManager(IGameState firstState)
    {
        Current = firstState;
    }

    public IGameState Current { get; private set; }

    public void TransitionTo(IGameState next) => Current = next;

    public void Update(GameTime gameTime) => Current.Update(gameTime, this);

    public void Draw(SpriteBatch spriteBatch, SpriteFont font) => Current.Draw(spriteBatch, font);
}
