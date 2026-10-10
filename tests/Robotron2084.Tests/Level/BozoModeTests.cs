using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The arcade's "BOZO MODE" mercy (RRG23 <c>PLRES</c>, R5 $2B26).</summary>
public sealed class BozoModeTests
{
    [Theory]
    [InlineData(1, 2, false)] // a fresh game: two spare men, doing fine
    [InlineData(1, 1, true)] // down a ship on wave 1
    [InlineData(2, 1, true)] // ...and on wave 2
    [InlineData(3, 1, false)] // ...but not on wave 3
    [InlineData(3, 0, true)] // no spare men left: waves 1-4 all get it
    [InlineData(4, 0, true)]
    [InlineData(5, 0, false)] // never past wave 4
    public void TheMercyAppliesToWavesOneToFourForAPlayerLosingShips(int wave, int spareMen, bool expected)
    {
        Assert.Equal(expected, BozoMode.AppliesTo(wave, spareMen));
    }

    [Fact]
    public void WaveOneGetsTheFirstRowOfTheTable()
    {
        var eased = BozoMode.Apply(new LevelParameters(1), 0);

        Assert.Equal(38, eased.SpheroidDropDelay);
        Assert.Equal(96, eased.EnforcerFireDelay);
        Assert.Equal(30, eased.GruntMoveDelay);
        Assert.Equal(15, eased.GruntSpeedFloor);
    }

    [Fact]
    public void ParametersAreUntouchedWhenTheMercyDoesNotApply()
    {
        var parameters = new LevelParameters(3);

        Assert.Same(parameters, BozoMode.Apply(parameters, 2));
    }
}
