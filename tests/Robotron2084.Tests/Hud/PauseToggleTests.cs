using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
///     The port's PAUSE key (notes §101). It is an edge detector on purpose: the player
///     holds the key while looking away from the screen, and a held key must not flap the
///     game in and out of pause.
/// </summary>
public sealed class PauseToggleTests
{
    [Fact]
    public void ANewGameIsNotPaused()
    {
        Assert.False(new PauseToggle().IsPaused);
    }

    [Fact]
    public void ItTogglesOnThePress_NotWhileTheKeyIsHeld()
    {
        var pauseToggle = new PauseToggle();

        pauseToggle.Tick(true);
        Assert.True(pauseToggle.IsPaused);

        for (var tick = 0; tick < 100; tick++) pauseToggle.Tick(true);

        Assert.True(pauseToggle.IsPaused); // still one pause, however long the key is down
    }

    [Fact]
    public void PressingAgainUnpauses()
    {
        var pauseToggle = new PauseToggle();

        pauseToggle.Tick(true);
        pauseToggle.Tick(false);
        pauseToggle.Tick(true);

        Assert.False(pauseToggle.IsPaused);
    }

    [Fact]
    public void Reset_LeavesItRunning_AndSwallowsTheKeyThatWasHeld()
    {
        var pauseToggle = new PauseToggle();
        pauseToggle.Tick(true);

        pauseToggle.Reset();

        Assert.False(pauseToggle.IsPaused);
        pauseToggle.Tick(true); // the key that was down when the game ended must not re-pause it
        Assert.False(pauseToggle.IsPaused);

        pauseToggle.Tick(false);
        pauseToggle.Tick(true);
        Assert.True(pauseToggle.IsPaused);
    }
}
