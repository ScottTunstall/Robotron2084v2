using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>The speech channel (port only): a line is never cut off, and lines are spoken in the order asked for.</summary>
public sealed class SpeechChannelTests
{
    [Fact]
    public void ALineAskedFor_IsSpokenOnTheNextTick()
    {
        var voice = new FakeSpeechVoice();
        var channel = new SpeechChannel(voice);

        channel.Say("GRUNT");
        Assert.Empty(voice.SpokenLineNames);
        channel.Tick();

        Assert.Equal(["GRUNT"], voice.SpokenLineNames);
    }

    [Fact]
    public void ALineAskedForWhileAnotherIsSpoken_WaitsForItToFinish_AndNeverCutsItOff()
    {
        var voice = new FakeSpeechVoice { TicksPerLine = 5 };
        var channel = new SpeechChannel(voice);
        channel.Say("FIRST");
        channel.Tick();

        channel.Say("SECOND");
        for (var tick = 0; tick < 4; tick++)
        {
            voice.Tick();
            channel.Tick();
            Assert.Equal(["FIRST"], voice.SpokenLineNames); // still on the first line
        }

        voice.Tick();
        channel.Tick();
        Assert.Equal(["FIRST", "SECOND"], voice.SpokenLineNames);
    }

    [Fact]
    public void LinesAreSpokenInTheOrderTheyWereAskedFor()
    {
        var voice = new FakeSpeechVoice();
        var channel = new SpeechChannel(voice);
        channel.Say("ONE");
        channel.Say("TWO");
        channel.Say("THREE");

        for (var tick = 0; tick < 30; tick++)
        {
            channel.Tick();
            voice.Tick();
        }

        Assert.Equal(["ONE", "TWO", "THREE"], voice.SpokenLineNames);
    }

    [Fact]
    public void TheChannelIsBusyUntilEveryLineIsSpoken()
    {
        var voice = new FakeSpeechVoice();
        var channel = new SpeechChannel(voice);
        Assert.False(channel.IsBusy());

        channel.Say("ONE");
        Assert.True(channel.IsBusy()); // waiting
        channel.Tick();
        Assert.True(channel.IsBusy()); // being spoken

        for (var tick = 0; tick < 3; tick++) voice.Tick();
        Assert.False(channel.IsBusy());
    }

    [Fact]
    public void TheSpeechService_DoesNothingUntilItIsSetUp_ThenQueuesLikeTheChannel()
    {
        var voice = new FakeSpeechVoice();

        Speech.Say("IGNORED");
        Speech.Tick();
        Assert.False(Speech.IsBusy());

        Speech.Initialise(voice);
        Speech.Say("HELLO");
        Assert.True(Speech.IsBusy());
        Speech.Tick();
        Assert.Equal(["HELLO"], voice.SpokenLineNames);
    }
}
