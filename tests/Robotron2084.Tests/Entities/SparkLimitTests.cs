using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;
using Robotron2084.Level.Models;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>Enforcers never put more than 10 sparks on the screen, however fast they fire.</summary>
public sealed class SparkLimitTests
{
    private static readonly TimeSpan OneTickSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    [Fact]
    public void AtTheFastestFire_TheFieldFillsTheLimitAndNoMore()
    {
        Assert.Equal(SparkTuning.MaxActiveSparks, GetMostSparksAtOnce(1, 4));
    }

    [Theory]
    [InlineData(1, 30)] // wave 1's slowest fire delay
    [InlineData(2, 12)]
    [InlineData(3, 4)]
    [InlineData(4, 1)] // an enforcer fires on every beat
    public void NoMoreThanTheLimitIsOnTheFieldAtOnce(int seed, int fireDelay)
    {
        var mostSparks = GetMostSparksAtOnce(fireDelay, seed);

        Assert.True(mostSparks > 0, "the enforcers never fired");
        Assert.True(mostSparks <= SparkTuning.MaxActiveSparks, $"{mostSparks} sparks at once");
    }

    [Fact]
    public void TheLimitIsTen()
    {
        Assert.Equal(10, SparkTuning.MaxActiveSparks);
    }

    private static int GetMostSparksAtOnce(int fireDelay, int seed)
    {
        var playField = new PlayFieldBuilder().WithParameters(new WaveParams(1, EnforcerFireDelay: fireDelay)).WithSeed(seed).Build();
        for (var enforcer = 0; enforcer < 8; enforcer++)
            playField.SpawnEnforcer(new IntVector2(300 + enforcer * 40, 300 + (enforcer % 3) * 60));

        var mostSparks = 0;
        for (var tick = 0; tick < 6000; tick++)
        {
            playField.Update(new GameTime(TimeSpan.Zero, OneTickSpan));
            mostSparks = Math.Max(mostSparks, playField.FieldEntities.Sparks.CountAlive());
        }

        return mostSparks;
    }
}
