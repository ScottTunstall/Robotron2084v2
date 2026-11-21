using Robotron2084.Core.Models;
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
        var skipWaveInputSource = new FakeInputSource(new PlayerInputState(IntVector2.Zero, IntVector2.Zero, false, true));
        var gameSession = GameSession.CreateNewGame(skipWaveInputSource, 1);
        var playingState = new PlayingState(TestTextures.SharedTextureSet, null!, gameSession, new Random(1));
        var gameStateManager = new GameStateManager(playingState);
        Assert.False(gameSession.GetCurrent().IsChicken);

        playingState.Update(new Microsoft.Xna.Framework.GameTime(), gameStateManager);

        Assert.Equal(2, gameSession.GetCurrent().Wave);
        Assert.True(gameSession.GetCurrent().IsChicken);
    }
}
