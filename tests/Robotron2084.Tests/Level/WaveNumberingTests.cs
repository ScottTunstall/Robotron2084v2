using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>Wave 1 introduces Gorf, and every wave the arcade has comes one wave later (port only; notes §150).</summary>
public sealed class WaveNumberingTests
{
    [Fact]
    public void WaveOne_IsTheGorfIntroduction_WithGorfAloneUntilTheAuthorSpecifiesIt()
    {
        var waveParameters = new WaveParameterGenerator().Generate(1);

        Assert.Equal(1, waveParameters.GorfCount);
        Assert.Equal(0, waveParameters.GruntCount);
        Assert.Equal(0, waveParameters.HulkCount);
        Assert.Equal(0, waveParameters.SpheroidCount);
        Assert.Equal(0, waveParameters.QuarkCount);
        Assert.Equal(0, waveParameters.BrainCount);
        Assert.Equal(0, waveParameters.ElectrodeCount);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(6, 5)]
    [InlineData(41, 40)]
    [InlineData(42, 41)]
    public void EveryOtherWave_IsTheArcadesWaveOneNumberLower(int portWave, int arcadeWave)
    {
        var generator = new WaveParameterGenerator();

        var shifted = generator.Generate(portWave);
        var arcade = generator.GenerateArcadeWave(arcadeWave);

        Assert.Equal(arcade, shifted);
        Assert.Equal(0, shifted.GorfCount);
    }

    [Fact]
    public void TheArcadesFirstWave_IsNowWaveTwo_AndSoItsOwnNumberIsStillOne()
    {
        var waveParameters = new WaveParameterGenerator().Generate(WaveNumbering.FirstArcadeWave);

        Assert.Equal(1, waveParameters.WaveNumber);
        Assert.Equal(WaveTable.GetParameters(1).GruntCount, waveParameters.GruntCount);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(10, 9)]
    public void ToArcadeWave_TakesOneOff_AndNeverGoesBelowOne(int portWave, int arcadeWave)
    {
        Assert.Equal(arcadeWave, WaveNumbering.ToArcadeWave(portWave));
    }
}
