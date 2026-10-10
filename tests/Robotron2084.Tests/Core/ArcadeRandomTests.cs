using Robotron2084.Core;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>The arcade's pick up to a limit, which halves a random byte until it fits (disassembly <c>$D6B6</c>).</summary>
public sealed class ArcadeRandomTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(30)]
    public void APick_IsAlwaysFromOneUpToTheLimit(int limit)
    {
        var random = new Random(7);
        for (var i = 0; i < 20000; i++)
        {
            var value = ArcadeRandom.PickUpTo(random, limit);
            Assert.InRange(value, 1, limit);
        }
    }

    [Fact]
    public void APick_FavoursLongerWaitsThanAnEvenSpreadWould()
    {
        const int Limit = 30;
        const int Samples = 100000;
        var random = new Random(42);
        long total = 0;
        for (var i = 0; i < Samples; i++) total += ArcadeRandom.PickUpTo(random, Limit);

        // An even spread from 1 to 30 averages 15.5. The arcade's halving averages about 21.5.
        var mean = (double)total / Samples;
        Assert.InRange(mean, 20.5, 22.5);
    }
}
