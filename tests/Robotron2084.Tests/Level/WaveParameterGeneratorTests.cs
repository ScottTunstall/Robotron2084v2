using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

public sealed class WaveParameterGeneratorTests
{
    [Fact]
    public void Generate_UsesArcadeWaveTable_ExactValues()
    {
        var p = new WaveParameterGenerator().GenerateArcadeWave(1);

        Assert.Equal(1, p.WaveNumber);
        // ROM $2E24 wave 1 (arcade-fidelity-notes §11.1).
        Assert.Equal(15, p.GruntCount);
        Assert.Equal(5, p.ElectrodeCount);
        Assert.Equal(1, p.MommyCount);
        Assert.Equal(1, p.DaddyCount);
        Assert.Equal(0, p.MikeyCount);
        Assert.Equal(0, p.HulkCount);
        Assert.Equal(0, p.BrainCount);
        Assert.Equal(0, p.SpheroidCount);
        Assert.Equal(0, p.QuarkCount);
        // ROM $2C20 wave 1 (notes §11.2).
        Assert.Equal(20, p.GruntMoveDelay);
        Assert.Equal(9, p.GruntSpeedFloor);
        Assert.Equal(10, p.MaxDropsX2);
        Assert.Equal(0, p.EnemySpeedBonus); // no per-wave bonus in the ROM
    }

    [Fact]
    public void Generate_Wave5_BrainAndSpheroidWave()
    {
        var p = new WaveParameterGenerator().GenerateArcadeWave(5);

        Assert.Equal(20, p.GruntCount);
        Assert.Equal(20, p.ElectrodeCount);
        Assert.Equal(15, p.MommyCount);
        Assert.Equal(0, p.DaddyCount);
        Assert.Equal(1, p.MikeyCount);
        Assert.Equal(15, p.BrainCount);
        Assert.Equal(1, p.SpheroidCount);
        Assert.Equal(0, p.QuarkCount);
    }

    [Fact]
    public void Generate_Wave9_HeavyGruntWave()
    {
        var p = new WaveParameterGenerator().GenerateArcadeWave(9);

        Assert.Equal(60, p.GruntCount);
        Assert.Equal(0, p.ElectrodeCount);
        Assert.Equal(5, p.SpheroidCount);
    }

    [Fact]
    public void Generate_WavesAbove40_RepeatWaves21To40()
    {
        WaveParameterGenerator waveParameterGenerator = new();

        var w41 = waveParameterGenerator.GenerateArcadeWave(41);
        var w21 = waveParameterGenerator.GenerateArcadeWave(21);
        var w60 = waveParameterGenerator.GenerateArcadeWave(60);
        var w40 = waveParameterGenerator.GenerateArcadeWave(40);
        var w61 = waveParameterGenerator.GenerateArcadeWave(61);

        Assert.Equal(w21.GruntCount, w41.GruntCount);
        Assert.Equal(w40.GruntCount, w60.GruntCount);
        Assert.Equal(w21.GruntCount, w61.GruntCount);
    }

    [Fact]
    public void Generate_WithCustomWaveTable_UsesExactRowsAndWrapsPastTheEnd()
    {
        var path = Path.Combine(Path.GetTempPath(), $"robotron-table-{Guid.NewGuid():N}.csv");
        File.WriteAllText(
            path,
            "Wave,GruntCount,HulkCount,SpheroidCount,QuarkCount,ElectrodeCount,MaxEnforcersPerSpheroid,MaxTanksPerQuark\n" +
            "1,5,2,1,1,10,3,2\n" +
            "2,9,3,2,2,12,4,3\n");
        try
        {
            var waveParameterGenerator = new WaveParameterGenerator(path);

            var level1 = waveParameterGenerator.GenerateArcadeWave(1);
            Assert.Equal(5, level1.GruntCount);
            Assert.Equal(2, level1.HulkCount);
            Assert.Equal(1, level1.SpheroidCount);
            Assert.Equal(1, level1.QuarkCount);
            Assert.Equal(10, level1.ElectrodeCount);
            Assert.Equal(3, level1.MaxEnforcersPerSpheroid);
            Assert.Equal(2, level1.MaxTanksPerQuark);
            Assert.Equal(0, level1.EnemySpeedBonus);

            var level2 = waveParameterGenerator.GenerateArcadeWave(2);
            Assert.Equal(9, level2.GruntCount);
            Assert.Equal(4, level2.MaxEnforcersPerSpheroid);
            Assert.Equal(1, level2.EnemySpeedBonus); // still computed from the wave number

            var level3 = waveParameterGenerator.GenerateArcadeWave(3); // wraps back to row 1
            Assert.Equal(5, level3.GruntCount);
            Assert.Equal(2, level3.EnemySpeedBonus);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Generate_MissingTable_FallsBackToArcadeWaveTable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"robotron-missing-{Guid.NewGuid():N}.csv");
        var waveParameterGenerator = new WaveParameterGenerator(path);

        var p = waveParameterGenerator.GenerateArcadeWave(1);

        Assert.Equal(15, p.GruntCount); // ROM wave 1
        Assert.Equal(5, p.ElectrodeCount);
    }

    [Fact]
    public void Generate_MalformedTable_FallsBackToArcadeWaveTable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"robotron-bad-{Guid.NewGuid():N}.csv");
        File.WriteAllText(
            path,
            "Wave,GruntCount,HulkCount,SpheroidCount,QuarkCount,ElectrodeCount,MaxEnforcersPerSpheroid,MaxTanksPerQuark\n" +
            "1,notanumber,2,1,1,10,3,2\n");
        try
        {
            var waveParameterGenerator = new WaveParameterGenerator(path);
            var p = waveParameterGenerator.GenerateArcadeWave(1);

            Assert.Equal(15, p.GruntCount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
