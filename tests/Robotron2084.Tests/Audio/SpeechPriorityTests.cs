using Robotron2084.Audio;
using Robotron2084.Audio.Models;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>An important line stops what is being said and is heard at once; anything else waits its turn.</summary>
public sealed class SpeechPriorityTests
{
    private readonly FakeSpeechVoice _voice = new() { TicksPerLine = 10 };
    private readonly SpeechChannel _speechChannel;

    public SpeechPriorityTests()
    {
        _speechChannel = new SpeechChannel(_voice);
    }

    [Fact]
    public void AnImportantLine_StopsANormalLine_DropsWhatIsWaiting_AndIsSpokenNext()
    {
        _speechChannel.Say("KILL");
        _speechChannel.Tick();
        _speechChannel.Say("IT");

        _speechChannel.Say("GOT", speechPriority: SpeechPriority.Important);
        _speechChannel.Tick();

        Assert.Equal(1, _voice.StopCount);
        Assert.Equal(["KILL", "GOT"], _voice.SpokenLineNames);
    }

    [Fact]
    public void AnImportantLine_DoesNotStopAnotherImportantLine()
    {
        _speechChannel.Say("GOT", speechPriority: SpeechPriority.Important);
        _speechChannel.Tick();

        _speechChannel.Say("THE", speechPriority: SpeechPriority.Important);
        _speechChannel.Tick();

        Assert.Equal(0, _voice.StopCount);
        Assert.Equal(["GOT"], _voice.SpokenLineNames);
    }

    [Fact]
    public void ANormalLine_WaitsBehindAnImportantOne()
    {
        _speechChannel.Say("GOT", speechPriority: SpeechPriority.Important);
        _speechChannel.Tick();

        _speechChannel.Say("KILL");
        _speechChannel.Tick();

        Assert.Equal(0, _voice.StopCount);
        Assert.Equal(["GOT"], _voice.SpokenLineNames);
    }

    [Fact]
    public void AnImportantLine_EndsASilenceThatIsBeingWaitedOut()
    {
        _speechChannel.Wait(30);
        _speechChannel.Tick();

        _speechChannel.Say("GOT", speechPriority: SpeechPriority.Important);
        _speechChannel.Tick();

        Assert.Equal(["GOT"], _voice.SpokenLineNames);
    }

    [Fact]
    public void WhenNothingIsBeingSaid_AnImportantLineStopsNothing()
    {
        _speechChannel.Say("GOT", speechPriority: SpeechPriority.Important);
        _speechChannel.Tick();

        Assert.Equal(0, _voice.StopCount);
        Assert.Equal(["GOT"], _voice.SpokenLineNames);
    }
}
