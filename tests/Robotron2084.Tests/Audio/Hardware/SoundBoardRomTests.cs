using Robotron2084.Audio;
using Robotron2084.Audio.Hardware;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Audio.Hardware;

/// <summary>
/// The emulated board running the real sound ROM. The ROM is not in git,
/// so these skip when it is missing. The expectations were measured against MAME 0.288 driving the same
/// sound tables through the arcade's own <c>SNDSEQ</c> (notes §126).
/// </summary>
public class SoundBoardRomTests
{
    private const int SampleRate = 44_100;

    /// <summary>The port's fixed update rate, which the wave-clear screen is timed in.</summary>
    private const int PortTicksPerSecond = 60;

    /// <summary>Loudness below which a stretch of sound counts as silence.</summary>
    private const float Silence = 0.01f;

    [Fact]
    public void TheBoardIsSilent_UntilItIsSentASoundNumber()
    {
        (_, SoundBoardRenderer renderer) = StartBoard();

        Assert.True(Loudness(renderer, seconds: 0.5) < Silence);
    }

    [Fact]
    public void TheLaser_IsHeard_ThenDiesAwayWithinThreeSeconds()
    {
        (SoundBoard board, SoundBoardRenderer renderer) = StartBoard();

        board.SendSoundNumber(0x25);

        Assert.True(Loudness(renderer, seconds: 0.25) > 0.3f);
        Loudness(renderer, seconds: 2.75);
        Assert.True(Loudness(renderer, seconds: 0.5) < Silence);
    }

    [Fact]
    public void TheShellFireSound_KeepsGoing_UntilSomethingReplacesIt()
    {
        (SoundBoard board, SoundBoardRenderer renderer) = StartBoard();

        board.SendSoundNumber(0x04);
        Loudness(renderer, seconds: 5);

        Assert.True(Loudness(renderer, seconds: 0.5) > 0.3f);
    }

    [Fact]
    public void SoundNumber13_StopsWhateverIsPlaying()
    {
        // The shell bounce table ends with it for one vblank (R5 $4B16: (1,4,$14) (1,1,$13)).
        (SoundBoard board, SoundBoardRenderer renderer) = StartBoard();
        board.SendSoundNumber(0x04);
        Loudness(renderer, seconds: 0.5);

        board.SendSoundNumber(0x13);
        Loudness(renderer, seconds: 0.25);

        Assert.True(Loudness(renderer, seconds: 0.5) < Silence);
    }

    /// <summary>
    /// The wave-end music is a sound the BOARD loops: it keeps playing after the ROM's table has asked
    /// for it for the last time, so the next level's own sounds would cut it short wherever the loop had
    /// got to — the author's report. Measure the phrase here and assert the voice is held long enough
    /// for the music to run out, so the two cannot drift apart again (notes §128).
    /// </summary>
    [Fact]
    public void TheHeldVoiceCoversTheWholeWaveEndMusic()
    {
        var board = new SoundBoard(RomFiles.ReadOrSkip(RomFiles.SoundRom));
        var renderer = new SoundBoardRenderer(board, SampleRate);
        board.SendSoundNumber(0x0E);

        const int measuredTicks = 1800;
        var level = new float[measuredTicks + 1];
        for (int tick = 1; tick <= measuredTicks; tick++)
        {
            level[tick] = Loudness(renderer, 1 / (double)PortTicksPerSecond);
        }

        int period = 0;
        double best = double.MaxValue;
        for (int candidate = 60; candidate <= 400; candidate++)
        {
            double difference = 0;
            for (int tick = 1; tick + candidate <= measuredTicks; tick++)
            {
                difference += Math.Abs(level[tick] - level[tick + candidate]);
            }

            difference /= measuredTicks - candidate;
            if (difference < best)
            {
                best = difference;
                period = candidate;
            }
        }

        Assert.InRange(period, 175, 191); // 183 ticks = 3.05 s

        // The ROM's table asks for the sound 29 times, 4 ticks apart, the first at tick 1, and the
        // phrase that follows the last ask plays out to here.
        SoundEntry line = SoundTables.WaveEnd.Entries[0];
        int musicEndsAt = 1 + ((line.Repetitions - 1) * line.LengthVblanks) + period;

        Assert.True(
            SoundTuning.WaveEndMusicTicks >= musicEndsAt,
            $"the voice is held {SoundTuning.WaveEndMusicTicks} ticks, which must cover the level music ({musicEndsAt} ticks)");
    }

    private static (SoundBoard Board, SoundBoardRenderer Renderer) StartBoard()
    {
        var board = new SoundBoard(RomFiles.ReadOrSkip(RomFiles.SoundRom));
        return (board, new SoundBoardRenderer(board, SampleRate));
    }

    private static float Loudness(SoundBoardRenderer renderer, double seconds)
    {
        var samples = new float[(int)(SampleRate * seconds)];
        renderer.Render(samples);
        double sumOfSquares = samples.Sum(sample => (double)sample * sample);
        return (float)Math.Sqrt(sumOfSquares / samples.Length);
    }
}
