using Robotron2084.Hud;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// The port's PAUSE key (notes §101). It is an edge detector on purpose: the player
/// holds the key while looking away from the screen, and a held key must not flap the
/// game in and out of pause.
/// </summary>
public sealed class PauseToggleTests
{
    [Fact]
    public void ANewGameIsNotPaused() => Assert.False(new PauseToggle().IsPaused);

    [Fact]
    public void ItTogglesOnThePress_NotWhileTheKeyIsHeld()
    {
        var pause = new PauseToggle();

        pause.Tick(held: true);
        Assert.True(pause.IsPaused);

        for (int tick = 0; tick < 100; tick++)
        {
            pause.Tick(held: true);
        }

        Assert.True(pause.IsPaused); // still one pause, however long the key is down
    }

    [Fact]
    public void PressingAgainUnpauses()
    {
        var pause = new PauseToggle();

        pause.Tick(held: true);
        pause.Tick(held: false);
        pause.Tick(held: true);

        Assert.False(pause.IsPaused);
    }

    [Fact]
    public void Reset_LeavesItRunning_AndSwallowsTheKeyThatWasHeld()
    {
        var pause = new PauseToggle();
        pause.Tick(held: true);

        pause.Reset();

        Assert.False(pause.IsPaused);
        pause.Tick(held: true); // the key that was down when the game ended must not re-pause it
        Assert.False(pause.IsPaused);

        pause.Tick(held: false);
        pause.Tick(held: true);
        Assert.True(pause.IsPaused);
    }
}
