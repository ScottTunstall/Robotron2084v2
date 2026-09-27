using Robotron2084.Audio;
using Robotron2084.Audio.Hardware;
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
