using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class StereoPannerTests
{
    private const int GlideSettledSamples = 4000;

    [Fact]
    public void ASoundInTheMiddle_IsEquallyLoudInBothSpeakers_AtAboutSeventyOnePercent()
    {
        var panner = new StereoPanner();

        (short left, short right) = LastFrame(panner, samples: 10);

        Assert.Equal(left, right);
        Assert.InRange(left, 23000, 23400);
    }

    [Fact]
    public void ASoundPannedFullyLeft_EndsUpOnlyInTheLeftSpeaker()
    {
        var panner = new StereoPanner();

        panner.PanTo(-1f);
        (short left, short right) = LastFrame(panner, GlideSettledSamples);

        Assert.InRange(left, 32000, short.MaxValue);
        Assert.InRange(right, (short)0, (short)100);
    }

    [Fact]
    public void MovingTheSound_GlidesRatherThanJumping()
    {
        var panner = new StereoPanner();

        panner.PanTo(1f);
        (short left, short right) = LastFrame(panner, samples: 1);

        Assert.InRange(left, 23000, 23400);
        Assert.InRange(right, 23000, 23400);
    }

    [Fact]
    public void ALoudSample_IsClippedRatherThanWrappingAround()
    {
        var panner = new StereoPanner();
        float[] samples = [4f];
        var stereo = new short[2];

        panner.Spread(samples, 1f, stereo);

        Assert.Equal(short.MaxValue, stereo[0]);
    }

    private static (short Left, short Right) LastFrame(StereoPanner panner, int samples)
    {
        float[] full = Enumerable.Repeat(1f, samples).ToArray();
        var stereo = new short[samples * 2];
        panner.Spread(full, 1f, stereo);
        return (stereo[^2], stereo[^1]);
    }
}
