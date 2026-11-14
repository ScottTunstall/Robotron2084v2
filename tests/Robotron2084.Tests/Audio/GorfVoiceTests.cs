using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>Gorf's lines and the speech service saying them.</summary>
[Collection("SoundFacade")]
public sealed class GorfVoiceTests : IDisposable
{
    /// <summary>Leaves the shared speech service as it was before each test.</summary>
    public void Dispose()
    {
        Speech.ResetForTesting();
    }

    [Fact]
    public void EveryLineHasItsSampleInTheGorfFolder_AndEverySampleHasALine()
    {
        var lineNames = SpeechFolders.GetLineNames(SpeechFolders.Gorf);
        var gorfLines = Enum.GetValues<GorfLine>();

        Assert.All(gorfLines, gorfLine => Assert.Contains(GorfVoice.GetLineName(gorfLine), lineNames));
        Assert.Equal(lineNames.Count, gorfLines.Length);
    }

    [Fact]
    public void ALineIsSaidAtThePitchAskedFor_OrAsRecordedIfNoneIs()
    {
        var voice = new FakeSpeechVoice { TicksPerLine = 1 };
        Speech.Initialise(voice);

        Speech.SayGorf(GorfLine.GotYou);
        Speech.SayGorf(GorfLine.HaHaHaHa, -0.5f);
        for (var tick = 0; tick < 10; tick++)
        {
            Speech.Tick();
            voice.Tick();
        }

        Assert.Equal(["GOTYOU", "HAHAHAHA"], voice.SpokenLineNames);
        Assert.Equal([0f, -0.5f], voice.SpokenPitchOctaves);
    }

    [Theory]
    [InlineData(-1.5f)]
    [InlineData(1.5f)]
    public void APitchOfMoreThanAnOctaveEitherWayIsRefused(float pitchOctaves)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Speech.SayGorf(GorfLine.GotYou, pitchOctaves));
    }
}
