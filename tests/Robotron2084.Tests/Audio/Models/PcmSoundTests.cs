using Microsoft.Xna.Framework.Audio;
using Robotron2084.Audio.Models;
using Xunit;

namespace Robotron2084.Tests.Audio.Models;

/// <summary>Cutting the silence off the start and end of a sound.</summary>
public sealed class PcmSoundTests
{
    /// <summary>A sample rate where one millisecond is one sample, to keep the numbers small.</summary>
    private const int OneSamplePerMillisecond = 1000;

    [Fact]
    public void ASampleAtTheSilenceLevelIsSilence()
    {
        var trimmed = CreateMono(64, -64, 65, 64).GetTrimmed(64, 0);

        Assert.Equal([65], ToSamples(trimmed.Samples));
    }

    [Fact]
    public void ASoundThatIsAllSilenceIsKeptAsItIs()
    {
        var pcmSound = CreateMono(0, 10, 0);

        Assert.Same(pcmSound, pcmSound.GetTrimmed(64, 0));
    }

    [Fact]
    public void AStereoFrameIsLoudWhenEitherChannelIsLoud()
    {
        // Left, right, left, right ...: only the right channel of the second frame is loud.
        PcmSound pcmSound = new(ToBytes([0, 0, 0, 500, 0, 0]), OneSamplePerMillisecond, AudioChannels.Stereo);

        var trimmed = pcmSound.GetTrimmed(64, 0);

        Assert.Equal([0, 500], ToSamples(trimmed.Samples));
        Assert.Equal(AudioChannels.Stereo, trimmed.Channels);
    }

    [Fact]
    public void CutsTheSilenceAtBothEnds()
    {
        var trimmed = CreateMono(0, 0, 0, 500, -700, 300, 0, 0).GetTrimmed(64, 0);

        Assert.Equal([500, -700, 300], ToSamples(trimmed.Samples));
    }

    [Fact]
    public void KeepsQuietSamplesInsideTheSound()
    {
        var trimmed = CreateMono(0, 500, 0, 0, 10, 0, 500, 0).GetTrimmed(64, 0);

        Assert.Equal([500, 0, 0, 10, 0, 500], ToSamples(trimmed.Samples));
    }

    [Fact]
    public void KeepsTheMarginOfSilenceAtEachEnd()
    {
        var trimmed = CreateMono(0, 0, 0, 0, 500, 0, 0, 0, 0).GetTrimmed(64, 2);

        Assert.Equal([0, 0, 500, 0, 0], ToSamples(trimmed.Samples));
    }

    [Fact]
    public void KeepsTheSampleRate()
    {
        Assert.Equal(OneSamplePerMillisecond, CreateMono(0, 500, 0).GetTrimmed(64, 0).SampleRate);
    }

    [Fact]
    public void RejectsANegativeSilenceLevelOrMargin()
    {
        var pcmSound = CreateMono(500);

        Assert.Equal("silenceLevel", Assert.Throws<ArgumentOutOfRangeException>(() => pcmSound.GetTrimmed(-1, 0)).ParamName);
        Assert.Equal("marginMilliseconds", Assert.Throws<ArgumentOutOfRangeException>(() => pcmSound.GetTrimmed(0, -1)).ParamName);
    }

    [Fact]
    public void TheMarginStopsAtTheSoundsOwnEnds()
    {
        var trimmed = CreateMono(0, 500, 0).GetTrimmed(64, 10);

        Assert.Equal([0, 500, 0], ToSamples(trimmed.Samples));
    }

    private static PcmSound CreateMono(params short[] samples)
    {
        return new PcmSound(ToBytes(samples), OneSamplePerMillisecond, AudioChannels.Mono);
    }

    private static byte[] ToBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static short[] ToSamples(byte[] bytes)
    {
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        return samples;
    }
}
