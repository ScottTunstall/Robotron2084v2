using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class StereoPannerTests
{
    private const int GlideSettledSamples = 4000;

    [Fact]
    public void ASoundInTheMiddle_IsEquallyLoudInBothSpeakers_AtAboutSeventyOnePercent()
    {
        var stereoPanner = new StereoPanner();

        var (left, right) = LastFrame(stereoPanner, 10);

        Assert.Equal(left, right);
        Assert.InRange(left, 23000, 23400);
    }

    [Fact]
    public void ASoundPannedFullyLeft_EndsUpOnlyInTheLeftSpeaker()
    {
        var stereoPanner = new StereoPanner();

        stereoPanner.PanTo(-1f);
        var (left, right) = LastFrame(stereoPanner, GlideSettledSamples);

        Assert.InRange(left, 32000, short.MaxValue);
        Assert.InRange(right, (short)0, (short)100);
    }

    [Fact]
    public void MovingTheSound_GlidesRatherThanJumping()
    {
        var stereoPanner = new StereoPanner();

        stereoPanner.PanTo(1f);
        var (left, right) = LastFrame(stereoPanner, 1);

        Assert.InRange(left, 23000, 23400);
        Assert.InRange(right, 23000, 23400);
    }

    [Fact]
    public void ALoudSample_IsClippedRatherThanWrappingAround()
    {
        var stereoPanner = new StereoPanner();
        float[] samples = [4f];
        var stereo = new short[2];

        stereoPanner.Spread(samples, 1f, stereo);

        Assert.Equal(short.MaxValue, stereo[0]);
    }

    private static (short Left, short Right) LastFrame(StereoPanner stereoPanner, int samples)
    {
        var full = Enumerable.Repeat(1f, samples).ToArray();
        var stereo = new short[samples * 2];
        stereoPanner.Spread(full, 1f, stereo);
        return (stereo[^2], stereo[^1]);
    }
}
