using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;
using Robotron2084.Events.Models;
using Robotron2084.Tests.Events;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The field announces that its wave has started.</summary>
public sealed class WaveStartedEventTests
{
    private static readonly GameTime TickGameTime = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

    [Fact]
    public void AFieldAnnouncesNothingBeforeItsFirstTick()
    {
        var recording = new RecordingGameEvents();

        _ = new PlayFieldBuilder().WithGameEvents(recording).Build();

        Assert.Empty(recording.Events);
    }

    [Fact]
    public void AFieldAnnouncesTheWaveStartedOnItsFirstTickAndNeverAgain()
    {
        var recording = new RecordingGameEvents();
        var playField = new PlayFieldBuilder().WithGameEvents(recording).Build();

        playField.Update(TickGameTime);
        playField.Update(TickGameTime);
        playField.Update(TickGameTime);

        Assert.Equal([new GameEvent(GameEventKind.WaveStarted, IntVector2.Zero)], recording.Events);
    }
}
