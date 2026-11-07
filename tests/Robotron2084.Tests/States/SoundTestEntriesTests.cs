using Robotron2084.Audio;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The sound test page's list: the board's sounds, the transporter, then the speech lines (port only).</summary>
public sealed class SoundTestEntriesTests
{
    private const int BoardSoundCount = 63;

    [Fact]
    public void TheList_HoldsTheBoardsSounds_ThenTheTransporter_ThenEverySpeechLine()
    {
        var entries = SoundTestEntries.All;

        Assert.Equal(BoardSoundCount + 1 + SpeechLinePaths.All.Length, entries.Count);
        Assert.All(entries.Take(BoardSoundCount), entry => Assert.NotNull(entry.SoundNumber));
        Assert.Null(entries[BoardSoundCount].SoundNumber);
        Assert.Null(entries[BoardSoundCount].SpeechLineName);
        Assert.Equal("TRANSPORTER", entries[BoardSoundCount].Heading);
    }

    [Fact]
    public void EachSpeechEntry_NamesItsLine_AndHasNoSoundNumber()
    {
        var speechEntries = SoundTestEntries.All.Skip(BoardSoundCount + 1).ToArray();

        Assert.NotEmpty(speechEntries);
        Assert.All(speechEntries, entry =>
        {
            Assert.Null(entry.SoundNumber);
            Assert.NotNull(entry.SpeechLineName);
            Assert.Equal(entry.SpeechLineName, entry.Name);
        });
        Assert.Equal("SPEECH 1", speechEntries[0].Heading);
        Assert.Contains(speechEntries, entry => entry.SpeechLineName == "GOTYOU");
    }

    [Fact]
    public void EverySpeechLineName_IsInTheFontsCharacters_SoThePageCanPrintIt()
    {
        var speechEntries = SoundTestEntries.All.Skip(BoardSoundCount + 1);

        Assert.All(speechEntries, entry => Assert.Equal(entry.Name, SoundTestState.ToDisplay(entry.Name)));
    }
}
