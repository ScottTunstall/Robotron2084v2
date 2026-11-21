using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.States;

/// <summary>Holds the current state and forwards update/draw; transitions replace <see cref="CurrentGameState" />.</summary>
/// <remarks>
///     A screen that is left is closed first when it is an <see cref="IClosableGameState" />, so whatever it changed that
///     other screens share (such as the palette) is put back however it is left. There is always a current state: the
///     manager refuses to be made, or to move on, without one.
/// </remarks>
public sealed class GameStateManager
{
    /// <summary>Makes a manager that starts on a state.</summary>
    /// <param name="firstGameState">The state to start on.</param>
    /// <exception cref="ArgumentNullException">The state is null.</exception>
    public GameStateManager(IGameState firstGameState)
    {
        ArgumentNullException.ThrowIfNull(firstGameState);

        CurrentGameState = firstGameState;
    }

    /// <summary>The state that is being updated and drawn.</summary>
    public IGameState CurrentGameState { get; private set; }

    /// <summary>Draws the current state.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="font">The font for the text the state draws.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        CurrentGameState.Draw(spriteBatch, font);
    }

    /// <summary>Closes the current state, if it needs closing, and makes another the current one.</summary>
    /// <param name="nextGameState">The state to move to.</param>
    /// <exception cref="ArgumentNullException">The state is null. The current state is left as it was.</exception>
    public void TransitionTo(IGameState nextGameState)
    {
        ArgumentNullException.ThrowIfNull(nextGameState);

        if (CurrentGameState is IClosableGameState closableGameState) closableGameState.Close();

        CurrentGameState = nextGameState;
    }

    /// <summary>Updates the current state.</summary>
    /// <param name="gameTime">How much time has passed since the last update.</param>
    public void Update(GameTime gameTime)
    {
        CurrentGameState.Update(gameTime, this);
    }
}
