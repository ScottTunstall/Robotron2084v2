using Robotron2084.Audio;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>The sound board turned down while the speech talks, sliding there and back a step at a time.</summary>
public sealed class BoardVolumeDuringSpeechTests
{
    /// <summary>More ticks than the slide from one end to the other takes.</summary>
    private const int EnoughTicks = 20;

    /// <summary>How close two volumes must be to count as the same.</summary>
    private const float Tolerance = 0.0001f;

    private static BoardVolumeDuringSpeech AdvanceFor(int ticks, bool isSpeechTalking, BoardVolumeDuringSpeech? volume = null)
    {
        volume ??= new BoardVolumeDuringSpeech();
        for (var tick = 0; tick < ticks; tick++) volume.Advance(isSpeechTalking);

        return volume;
    }

    [Fact]
    public void TheBoardStartsAtFullVolume()
    {
        Assert.Equal(1f, new BoardVolumeDuringSpeech().Volume);
    }

    [Fact]
    public void TheBoardStaysAtFullVolumeWhileTheSpeechIsQuiet()
    {
        Assert.Equal(1f, AdvanceFor(EnoughTicks, false).Volume);
    }

    [Fact]
    public void TheBoardIsTurnedDownWhileTheSpeechTalks()
    {
        Assert.Equal(SoundTuning.BoardVolumeWhileSpeaking, AdvanceFor(EnoughTicks, true).Volume, Tolerance);
    }

    [Fact]
    public void TheBoardSlidesDownOneStepATick()
    {
        var volume = AdvanceFor(1, true);

        Assert.Equal(1f - SoundTuning.BoardVolumeStepPerTick, volume.Volume, Tolerance);
    }

    [Fact]
    public void TheBoardNeverGoesBelowItsVolumeWhileSpeaking()
    {
        BoardVolumeDuringSpeech volume = new();
        for (var tick = 0; tick < EnoughTicks; tick++)
        {
            volume.Advance(true);
            Assert.True(volume.Volume >= SoundTuning.BoardVolumeWhileSpeaking - Tolerance);
        }
    }

    [Fact]
    public void TheBoardSlidesBackToFullVolumeWhenTheSpeechStops()
    {
        var volume = AdvanceFor(EnoughTicks, true);

        volume.Advance(false);
        Assert.Equal(SoundTuning.BoardVolumeWhileSpeaking + SoundTuning.BoardVolumeStepPerTick, volume.Volume, Tolerance);

        Assert.Equal(1f, AdvanceFor(EnoughTicks, false, volume).Volume, Tolerance);
    }
}
