using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The arcade's "BOZO MODE" mercy (RRG23 <c>PLRES</c>, R5 $2B26).</summary>
public sealed class BozoModeTests
{
    [Theory]
    [InlineData(1, 2, false)] // a fresh game: two spare lives, doing fine
    [InlineData(1, 1, true)] // down a life on wave 1
    [InlineData(2, 1, true)] // ...and on wave 2
    [InlineData(3, 1, false)] // ...but not on wave 3
    [InlineData(3, 0, true)] // no spare lives left: waves 1-4 all get it
    [InlineData(4, 0, true)]
    [InlineData(5, 0, false)] // never past wave 4
    public void TheMercyAppliesToWavesOneToFourForAPlayerLosingShips(int wave, int spareLives, bool expected)
    {
        Assert.Equal(expected, BozoMode.AppliesTo(wave, spareLives));
    }

    [Fact]
    public void WaveOneGetsTheFirstRowOfTheTable()
    {
        var eased = BozoMode.Apply(new WaveParameters(1), 0);

        Assert.Equal(38, eased.SpheroidDropDelay);
        Assert.Equal(96, eased.EnforcerFireDelay);
        Assert.Equal(30, eased.GruntMoveDelay);
        Assert.Equal(15, eased.GruntSpeedFloor);
    }

    [Fact]
    public void ParametersAreUntouchedWhenTheMercyDoesNotApply()
    {
        var waveParameters = new WaveParameters(3);

        Assert.Same(waveParameters, BozoMode.Apply(waveParameters, 2));
    }
}
