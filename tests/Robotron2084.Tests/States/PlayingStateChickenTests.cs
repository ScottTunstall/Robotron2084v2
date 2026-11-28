using Robotron2084.Core.Models;
using Robotron2084.Input;
using Robotron2084.Input.Models;
using Robotron2084.Level;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>A player who ends a wave with a family member left on the field is a chicken in the next wave.</summary>
public sealed class PlayingStateChickenTests
{
    [Fact]
    public void ClearingAWave_WithTheFamilyStillOnTheField_MakesThePlayerAChicken()
    {
        var skipWaveInputSource = new SkipWaveInputSource();
        var gameSession = GameSession.CreateNewGame(skipWaveInputSource, 1);
        var playingState = new PlayingState(TestTextures.SharedTextureSet, null!, gameSession, new Random(1));
        var gameStateManager = new GameStateManager(playingState);
        Assert.False(gameSession.GetCurrent().IsChicken);

        // The skip-wave key must be seen up before it goes down.
        playingState.Update(new Microsoft.Xna.Framework.GameTime(), gameStateManager);
        Assert.Equal(1, gameSession.GetCurrent().Wave);

        skipWaveInputSource.IsSkipWaveHeld = true;
        playingState.Update(new Microsoft.Xna.Framework.GameTime(), gameStateManager);

        Assert.Equal(2, gameSession.GetCurrent().Wave);
        Assert.True(gameSession.GetCurrent().IsChicken);
    }

    [Fact]
    public void ASkipWaveKeyHeldFromTheMenu_DoesNotSkipTheFirstWave()
    {
        var skipWaveInputSource = new SkipWaveInputSource { IsSkipWaveHeld = true };
        var gameSession = GameSession.CreateNewGame(skipWaveInputSource, 1);
        var playingState = new PlayingState(TestTextures.SharedTextureSet, null!, gameSession, new Random(1));

        playingState.Update(new Microsoft.Xna.Framework.GameTime(), new GameStateManager(playingState));

        Assert.Equal(1, gameSession.GetCurrent().Wave);
    }

    /// <summary>An input source whose skip-wave key the test presses and lets go.</summary>
    private sealed class SkipWaveInputSource : IPlayerInputSource
    {
        public bool IsSkipWaveHeld { get; set; }

        public PlayerInputState Poll()
        {
            return new PlayerInputState(IntVector2.Zero, IntVector2.Zero, false, IsSkipWaveHeld);
        }
    }
}
