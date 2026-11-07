using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>Wave 1 introduces Gorf, and every wave the arcade has comes one wave later (port only; notes §150).</summary>
public sealed class WaveNumberingTests
{
    [Fact]
    public void WaveOne_IsTheGorfIntroduction_WithGorfAndElectrodesAndNoneOfTheArcadesRobots_UntilTheAuthorSpecifiesIt()
    {
        var waveParameters = new WaveParameterGenerator().Generate(1);

        Assert.True(waveParameters.HasGorf());
        Assert.Equal(1, waveParameters.GorfCount);
        Assert.Equal(0, waveParameters.GruntCount);
        Assert.Equal(0, waveParameters.HulkCount);
        Assert.Equal(0, waveParameters.SpheroidCount);
        Assert.Equal(0, waveParameters.QuarkCount);
        Assert.Equal(0, waveParameters.BrainCount);
        Assert.False(waveParameters.PlayfieldWallsOpen);
    }

    [Fact]
    public void TheGorfIntroduction_DropsAsManyGruntsAsTheWaveAfterItHas()
    {
        var generator = new WaveParameterGenerator();

        var intro = generator.Generate(WaveNumbering.GorfIntroWave);
        var nextWave = generator.Generate(WaveNumbering.FirstArcadeWave);

        Assert.Equal(nextWave.GruntCount, intro.GorfGruntDropCount);
        Assert.True(intro.GorfGruntDropCount > 0);
        Assert.Equal(0, intro.GruntCount); // the wave starts with none: they are all dropped by Gorf
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(6, 6)]
    [InlineData(7, 5)]
    [InlineData(8, 7)]
    [InlineData(41, 40)]
    [InlineData(42, 41)]
    public void EveryOtherWave_IsTheArcadesWaveOneNumberLower_ExceptThatTheBrainWaveIsOneLater(int portWave, int arcadeWave)
    {
        var generator = new WaveParameterGenerator();

        var shifted = generator.Generate(portWave);
        var arcade = generator.GenerateArcadeWave(arcadeWave);

        Assert.Equal(arcade, shifted);
        Assert.False(shifted.HasGorf());
        Assert.Equal(0, shifted.GorfCount);
    }

    [Fact]
    public void TheBrainWave_IsWaveSeven_AndWaveSixIsTheArcadesWaveSix()
    {
        var generator = new WaveParameterGenerator();

        Assert.Equal(0, generator.Generate(6).BrainCount);
        Assert.Equal(WaveTable.GetParameters(5).BrainCount, generator.Generate(7).BrainCount);
        Assert.True(generator.Generate(7).BrainCount > 0);
        Assert.Equal(WaveTable.GetParameters(10).BrainCount, generator.Generate(11).BrainCount); // the later brain waves stay where they were
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
    [InlineData(6, 6)]
    [InlineData(7, 5)]
    [InlineData(10, 9)]
    public void ToArcadeWave_TakesOneOff_AndNeverGoesBelowOne(int portWave, int arcadeWave)
    {
        Assert.Equal(arcadeWave, WaveNumbering.ToArcadeWave(portWave));
    }
}
