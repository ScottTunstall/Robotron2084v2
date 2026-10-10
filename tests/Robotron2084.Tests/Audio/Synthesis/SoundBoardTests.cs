using Robotron2084.Audio;
using Robotron2084.Audio.Synthesis;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Audio.Synthesis;

/// <summary>
/// The game's sound board, rebuilt from the sound ROM's source. The expectations were measured against
/// MAME 0.288 driving the same sound tables through the arcade's own <c>SNDSEQ</c> (notes §126); the board
/// itself is checked change for change against the real ROM by the sound player's tests (notes §130).
/// </summary>
public class SoundBoardTests
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
    /// The wave-end music is a sound the BOARD loops: it keeps playing after the ROM's table has asked for
    /// it for the last time, so the next level's own sounds used to cut it short wherever the loop had got
    /// to (the author's report). Measure the phrase here, then pin the held voice between the table's last
    /// ask and the music's own end — the exact value inside that window is the author's ear (notes §128).
    /// </summary>
    [Fact]
    public void TheHeldVoiceStaysWithinTheWaveEndMusic()
    {
        var board = new SoundBoard();
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

        // The ROM's table asks for the sound 29 times, 4 ticks apart, the first at tick 1, so the last
        // ask lands at tableEndsAt and the phrase that follows it plays out to musicEndsAt. The hold sits
        // between them: long enough that every ask is heard, short enough never to outlast the music.
        SoundEntry line = SoundTables.WaveEnd.Entries[0];
        int tableEndsAt = 1 + ((line.Repetitions - 1) * line.LengthVblanks);
        int musicEndsAt = tableEndsAt + period;

        Assert.InRange(SoundTuning.WaveEndMusicTicks, tableEndsAt, musicEndsAt);
    }

    [Fact]
    public void EverySoundNumberTheGameSends_HasARoutine()
    {
        var board = new SoundBoard();
        IEnumerable<int> sent = SoundTables.All.SelectMany(table => table.Entries).Select(entry => (int)entry.SoundNumber);

        Assert.All(sent, soundNumber => Assert.True(board.CanPlay(soundNumber), $"sound ${soundNumber:X2}"));
    }

    [Fact]
    public void SoundNumber0_IsNotASound_AndIsIgnored()
    {
        (SoundBoard board, SoundBoardRenderer renderer) = StartBoard();

        board.SendSoundNumber(0);

        Assert.False(board.CanPlay(0));
        Assert.True(Loudness(renderer, seconds: 0.5) < Silence);
    }

    /// <summary>
    /// The eight coin sounds (<c>CNSND</c>, notes §130): each is built, is heard, and ends by itself within a few seconds
    /// (the board has no loop in any of them, so a run that never ends would be a mistake in the port).
    /// </summary>
    [Theory]
    [InlineData(0x0C)]
    [InlineData(0x20)]
    [InlineData(0x24)]
    [InlineData(0x27)]
    [InlineData(0x2D)]
    [InlineData(0x35)]
    [InlineData(0x3A)]
    [InlineData(0x3E)]
    public void EachCoinSound_IsHeard_ThenEndsByItself(int soundNumber)
    {
        (SoundBoard board, SoundBoardRenderer renderer) = StartBoard();

        board.SendSoundNumber(soundNumber);

        Assert.True(board.CanPlay(soundNumber));
        float whileItPlays = Loudness(renderer, seconds: 1.0);
        Loudness(renderer, seconds: 9.0);
        Assert.True(whileItPlays > Silence, $"Coin sound ${soundNumber:X2} was silent.");
        Assert.True(Loudness(renderer, seconds: 0.5) < Silence, $"Coin sound ${soundNumber:X2} had not ended after ten seconds.");
    }

    /// <summary>
    /// Every one of the board's 63 sounds (<see cref="BoardSounds"/>, the sound test page's list) has a routine, and each can be
    /// played for three seconds without the board failing or going quiet when it should not: only the three that
    /// are silent in the source (<c>BGEND</c>, <c>ORGANT</c>, <c>ORGANN</c>) make no sound.
    /// </summary>
    [Fact]
    public void EveryBoardSound_HasARoutine_AndIsHeardUnlessTheSourceMakesItSilent()
    {
        int[] silent = [0x13, 0x1B, 0x1C];
        foreach (BoardSound sound in BoardSounds.All)
        {
            (SoundBoard board, SoundBoardRenderer renderer) = StartBoard();
            Assert.True(board.CanPlay(sound.Number), $"{sound.Name} has no routine.");

            board.SendSoundNumber(sound.Number);

            float loudness = Loudness(renderer, seconds: 3);
            Assert.Equal(silent.Contains(sound.Number), loudness < Silence);
        }
    }

    private static (SoundBoard Board, SoundBoardRenderer Renderer) StartBoard()
    {
        var board = new SoundBoard();
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
