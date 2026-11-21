using Robotron2084.Core.Models;
using Robotron2084.Events;
using Robotron2084.Events.Models;
using Xunit;

namespace Robotron2084.Tests.Events;

/// <summary>The hub that passes each event on to everyone who subscribed.</summary>
public sealed class GameEventHubTests
{
    private static readonly GameEvent AnEvent = new(GameEventKind.FamilyMemberRescued, new IntVector2(3, 4));

    [Fact]
    public void ANullListenerIsRefused()
    {
        var hub = new GameEventHub();

        _ = Assert.Throws<ArgumentNullException>(() => hub.Subscribe(null!));
    }

    [Fact]
    public void ARaisedEventWithNobodySubscribedIsLostQuietly()
    {
        var hub = new GameEventHub();

        hub.Raise(AnEvent);

        Assert.Equal(0, hub.ListenerCount);
    }

    [Fact]
    public void EventsAreHandedOnInTheOrderTheyWereRaised()
    {
        var hub = new GameEventHub();
        var listener = new RecordingGameEvents();
        hub.Subscribe(listener);
        var other = new GameEvent(GameEventKind.PlayerKilled, IntVector2.Zero);

        hub.Raise(AnEvent);
        hub.Raise(other);

        Assert.Equal([AnEvent, other], listener.Events);
    }

    [Fact]
    public void EverySubscriberIsToldOfAnEvent()
    {
        var hub = new GameEventHub();
        var first = new RecordingGameEvents();
        var second = new RecordingGameEvents();
        hub.Subscribe(first);
        hub.Subscribe(second);

        hub.Raise(AnEvent);

        Assert.Equal([AnEvent], first.Events);
        Assert.Equal([AnEvent], second.Events);
    }

    [Fact]
    public void TheNullGameEventsTellsNobody()
    {
        NullGameEvents.Instance.Raise(AnEvent);
    }
}
