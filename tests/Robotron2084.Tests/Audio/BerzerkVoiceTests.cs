using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>The Berzerk robot's words and pitch.</summary>
public sealed class BerzerkVoiceTests
{
    [Fact]
    public void EveryWordHasItsSampleInTheBerzerkFolder_AndThePauseHasNone()
    {
        var lineNames = SpeechFolders.GetLineNames(SpeechFolders.Berzerk);
        var words = Enum.GetValues<BerzerkWord>().Where(word => word != BerzerkWord.Pause).ToList();

        Assert.All(words, word => Assert.Contains(BerzerkVoice.GetLineName(word), lineNames));
        Assert.Equal(lineNames.Count, words.Count); // and every sample has a word
        Assert.Throws<ArgumentOutOfRangeException>(() => BerzerkVoice.GetLineName(BerzerkWord.Pause));
    }

    [Fact]
    public void TheSamplePitchPlaysTheSamplesAsRecorded()
    {
        Assert.Equal(0f, BerzerkVoice.GetPitchOctaves(BerzerkVoice.SamplePitch));
    }

    [Theory]
    [InlineData(0, 13.0 / 16.0)]
    [InlineData(7, 13.0 / 9.0)]
    [InlineData(5, 13.0 / 11.0)]
    public void APitchPlaysTheSamplesFasterOrSlowerByTheRatioOfTheChipsClocks(int pitch, double speed)
    {
        Assert.Equal(Math.Log2(speed), BerzerkVoice.GetPitchOctaves(pitch), 5);
    }

    [Fact]
    public void EveryPitchIsWithinTheOctaveEitherWayThatTheVoiceCanPlay()
    {
        for (var pitch = BerzerkVoice.MinimumPitch; pitch <= BerzerkVoice.MaximumPitch; pitch++)
            Assert.InRange(BerzerkVoice.GetPitchOctaves(pitch), -1f, 1f);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void APitchOutsideZeroToSevenIsRefused(int pitch)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BerzerkVoice.GetPitchOctaves(pitch));
    }
}
