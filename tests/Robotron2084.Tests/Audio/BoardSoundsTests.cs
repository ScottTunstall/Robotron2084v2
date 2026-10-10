using Robotron2084.Audio;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class BoardSoundsTests
{
    [Fact]
    public void TheListHasEveryNumberFrom1To63_InOrder_EachWithAName()
    {
        Assert.Equal(Enumerable.Range(BoardSounds.First, BoardSounds.Last), BoardSounds.All.Select(sound => sound.Number));
        Assert.All(BoardSounds.All, sound => Assert.NotEmpty(sound.Name));
    }

    [Fact]
    public void EverySoundTheGameSends_IsInTheList()
    {
        int[] sent = [.. SoundTables.All.SelectMany(table => table.Entries).Select(entry => (int)entry.SoundNumber).Distinct()];

        Assert.All(sent, number => Assert.Contains(BoardSounds.All, sound => sound.Number == number));
    }

    [Theory]
    [InlineData("PERK$$", "PERKSS")]
    [InlineData("ED'S SOUND 10", "EDS SOUND 10")]
    [InlineData("DEFENDER SND #$17", "DEFENDER SND S17")]
    public void TheDisplayKeepsOnlyWhatTheFontsCanPrint(string source, string shown) =>
        Assert.Equal(shown, SoundTestState.ToDisplay(source));
}
