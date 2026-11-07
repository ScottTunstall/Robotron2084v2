using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>Leaving a screen closes it, so whatever it changed that other screens share is put back.</summary>
public sealed class GameStateManagerTests
{
    [Fact]
    public void LeavingAClosableScreen_ClosesItOnce()
    {
        var leftScreen = new FakeClosableGameState();
        var nextScreen = new FakeClosableGameState();
        var gameStateManager = new GameStateManager(leftScreen);

        gameStateManager.TransitionTo(nextScreen);

        Assert.Equal(1, leftScreen.CloseCount);
        Assert.Equal(0, nextScreen.CloseCount);
        Assert.Same(nextScreen, gameStateManager.CurrentGameState);
    }
}
