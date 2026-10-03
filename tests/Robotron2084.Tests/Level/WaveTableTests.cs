using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

public sealed class WaveTableTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(40, 40)]
    [InlineData(41, 21)]
    [InlineData(42, 22)]
    [InlineData(60, 40)]
    [InlineData(61, 21)]
    [InlineData(100, 40)]
    [InlineData(0, 1)]
    public void ResolveWave_AppliesRomRepeatRule(int wave, int expected)
    {
        Assert.Equal(expected, WaveTable.ResolveWave(wave));
    }

    [Fact]
    public void ForWave_Wave41_EqualsWave21_WholeRow()
    {
        Assert.Equal(WaveTable.GetParameters(21), WaveTable.GetParameters(41));
    }

    [Fact]
    public void ForWave_KnownRomValues()
    {
        WaveParameters w9 = WaveTable.GetParameters(9);
        Assert.Equal(60, w9.GruntCount);
        Assert.Equal(0, w9.ElectrodeCount);
        Assert.Equal(5, w9.SpheroidCount);

        WaveParameters w40 = WaveTable.GetParameters(40);
        Assert.Equal(30, w40.GruntCount);
        Assert.Equal(25, w40.BrainCount);
        Assert.Equal(10, w40.MommyCount);
        Assert.Equal(1, w40.SpheroidCount);
        Assert.Equal(1, w40.QuarkCount);
    }
}
