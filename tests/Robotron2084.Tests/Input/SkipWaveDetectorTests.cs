using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Input;

/// <summary>One press of a skip-wave key skips one wave, and the menu's Enter and Alt+Enter skip none.</summary>
public sealed class SkipWaveDetectorTests
{
    [Fact]
    public void AKeyAlreadyHeldAtTheStart_DoesNotSkip_UntilItIsLetGoAndPressedAgain()
    {
        var skipWaveDetector = new SkipWaveDetector();

        Assert.False(skipWaveDetector.Advance(true, false));
        Assert.False(skipWaveDetector.Advance(true, false));
        Assert.False(skipWaveDetector.Advance(false, false));
        Assert.True(skipWaveDetector.Advance(true, false));
    }

    [Fact]
    public void AHeldKey_SkipsOnlyOnce()
    {
        var skipWaveDetector = new SkipWaveDetector();
        skipWaveDetector.Advance(false, false);

        Assert.True(skipWaveDetector.Advance(true, false));
        Assert.False(skipWaveDetector.Advance(true, false));
    }

    [Fact]
    public void AltEnter_DoesNotSkip_EvenWhenAltIsLetGoFirst()
    {
        var skipWaveDetector = new SkipWaveDetector();
        skipWaveDetector.Advance(false, true);

        Assert.False(skipWaveDetector.Advance(true, true));
        Assert.False(skipWaveDetector.Advance(true, false));
    }
}
