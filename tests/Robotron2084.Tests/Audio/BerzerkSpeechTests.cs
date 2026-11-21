using Robotron2084.Audio;
using Robotron2084.Audio.Models;
using Xunit;
using static Robotron2084.Audio.Models.BerzerkWord;

namespace Robotron2084.Tests.Audio;

/// <summary>The speech service saying the Berzerk robot's words: in order, at their pitch, with pauses between.</summary>
[Collection("SoundFacade")]
public sealed class BerzerkSpeechTests : IDisposable
{
    [Fact]
    public void APauseIsASilence_NotALine()
    {
        var voice = SayAndListen(() => Speech.SayBerzerk(Intruder, Alert, Pause, Intruder, Alert));

        Assert.Equal(["INTRUDER", "ALERT", "INTRUDER", "ALERT"], voice.SpokenLineNames);
    }

    /// <summary>Leaves the shared speech service as it was before each test.</summary>
    public void Dispose()
    {
        Speech.ResetForTesting();
    }

    [Fact]
    public void EveryWordIsSaidAtThePitchAndVolumeAskedFor()
    {
        var voice = SayAndListen(() => Speech.SayBerzerk(new BerzerkVoiceSetting(7, 4), Intruder, Alert));

        Assert.Equal(2, voice.SpokenLineNames.Count);
        Assert.All(voice.SpokenPitchOctaves, octaves => Assert.Equal(BerzerkVoice.GetPitchOctaves(7), octaves));
        Assert.All(voice.SpokenVolumeShares, volumeShare => Assert.Equal(BerzerkVoice.GetVolumeShare(4), volumeShare));
    }

    [Fact]
    public void NothingIsSaidOnTheAttractPagesWhenTheirSoundIsOff()
    {
        var voice = new FakeSpeechVoice();
        Speech.Initialise(voice);
        Speech.AttractMuted = true;
        try
        {
            Speech.SayBerzerk(Chicken, Fight, Like, A, Robot);
            Speech.Tick();
        }
        finally
        {
            Speech.AttractMuted = false;
        }

        Assert.Empty(voice.SpokenLineNames);
        Assert.False(Speech.IsBusy());
    }

    [Fact]
    public void TheWordsAreSaidInOrder_AtTheSamplePitchAndVolume()
    {
        var voice = SayAndListen(() => Speech.SayBerzerk(The, Humanoid, Must, Not, Escape));

        Assert.Equal(["THE", "HUMANOID", "MUST", "NOT", "ESCAPE"], voice.SpokenLineNames);
        Assert.All(voice.SpokenPitchOctaves, octaves => Assert.Equal(0f, octaves));
        Assert.All(voice.SpokenVolumeShares, volumeShare => Assert.Equal(1f, volumeShare));
    }

    private static FakeSpeechVoice SayAndListen(Action say)
    {
        var voice = new FakeSpeechVoice { TicksPerLine = 1 };
        Speech.Initialise(voice);
        Speech.AttractMuted = false;
        say();
        for (var tick = 0; tick < 200 && Speech.IsBusy(); tick++)
        {
            Speech.Tick();
            voice.Tick();
        }

        return voice;
    }
}
