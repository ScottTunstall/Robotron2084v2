using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.States;

namespace Robotron2084.Tests.States;

/// <summary>A screen that counts how many times it was closed.</summary>
internal sealed class FakeClosableGameState : IClosableGameState
{
    /// <summary>How many times <see cref="Close" /> was called.</summary>
    public int CloseCount { get; private set; }

    /// <inheritdoc />
    public void Close()
    {
        CloseCount++;
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
    }
}
