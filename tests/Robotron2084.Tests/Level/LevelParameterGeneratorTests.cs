using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

public sealed class LevelParameterGeneratorTests
{
    [Fact]
    public void Generate_UsesRomWaveTable_ExactValues()
    {
        LevelParameters p = new LevelParameterGenerator().Generate(1);

        Assert.Equal(1, p.LevelNumber);
        // ROM $2E24 wave 1 (arcade-fidelity-notes §11.1).
        Assert.Equal(15, p.GruntCount);
        Assert.Equal(5, p.ElectrodeCount);
        Assert.Equal(1, p.MomCount);
        Assert.Equal(1, p.DadCount);
        Assert.Equal(0, p.MikeyCount);
        Assert.Equal(0, p.HulkCount);
        Assert.Equal(0, p.BrainCount);
        Assert.Equal(0, p.SpheroidCount);
        Assert.Equal(0, p.QuarkCount);
        // ROM $2C20 wave 1 (notes §11.2).
        Assert.Equal(20, p.GruntMoveDelay);
        Assert.Equal(9, p.GruntSpeedFloor);
        Assert.Equal(10, p.MaxDropsX2);
        Assert.Equal(0, p.EnemySpeedBonus); // no per-level bonus in the ROM
    }

    [Fact]
    public void Generate_Wave5_BrainAndSpheroidWave()
    {
        LevelParameters p = new LevelParameterGenerator().Generate(5);

        Assert.Equal(20, p.GruntCount);
        Assert.Equal(20, p.ElectrodeCount);
        Assert.Equal(15, p.MomCount);
        Assert.Equal(0, p.DadCount);
        Assert.Equal(1, p.MikeyCount);
        Assert.Equal(15, p.BrainCount);
        Assert.Equal(1, p.SpheroidCount);
        Assert.Equal(0, p.QuarkCount);
    }

    [Fact]
    public void Generate_Wave9_HeavyGruntWave()
    {
        LevelParameters p = new LevelParameterGenerator().Generate(9);

        Assert.Equal(60, p.GruntCount);
        Assert.Equal(0, p.ElectrodeCount);
        Assert.Equal(5, p.SpheroidCount);
    }

    [Fact]
    public void Generate_LevelsAbove40_RepeatWaves21To40()
    {
        LevelParameterGenerator generator = new();

        LevelParameters w41 = generator.Generate(41);
        LevelParameters w21 = generator.Generate(21);
        LevelParameters w60 = generator.Generate(60);
        LevelParameters w40 = generator.Generate(40);
        LevelParameters w61 = generator.Generate(61);

        Assert.Equal(w21.GruntCount, w41.GruntCount);
        Assert.Equal(w40.GruntCount, w60.GruntCount);
        Assert.Equal(w21.GruntCount, w61.GruntCount);
    }

    [Fact]
    public void Generate_WithLevelTable_UsesExactRowsAndWrapsPastTheEnd()
    {
        string path = Path.Combine(Path.GetTempPath(), $"robotron-table-{Guid.NewGuid():N}.csv");
        File.WriteAllText(
            path,
            "Level,GruntCount,HulkCount,SpheroidCount,QuarkCount,ElectrodeCount,MaxEnforcersPerSpheroid,MaxTanksPerQuark\n" +
            "1,5,2,1,1,10,3,2\n" +
            "2,9,3,2,2,12,4,3\n");
        try
        {
            var generator = new LevelParameterGenerator(new Random(1), path);

            LevelParameters level1 = generator.Generate(1);
            Assert.Equal(5, level1.GruntCount);
            Assert.Equal(2, level1.HulkCount);
            Assert.Equal(1, level1.SpheroidCount);
            Assert.Equal(1, level1.QuarkCount);
            Assert.Equal(10, level1.ElectrodeCount);
            Assert.Equal(3, level1.MaxEnforcersPerSpheroid);
            Assert.Equal(2, level1.MaxTanksPerQuark);
            Assert.Equal(0, level1.EnemySpeedBonus);

            LevelParameters level2 = generator.Generate(2);
            Assert.Equal(9, level2.GruntCount);
            Assert.Equal(4, level2.MaxEnforcersPerSpheroid);
            Assert.Equal(1, level2.EnemySpeedBonus); // still computed from the level number

            LevelParameters level3 = generator.Generate(3); // wraps back to row 1
            Assert.Equal(5, level3.GruntCount);
            Assert.Equal(2, level3.EnemySpeedBonus);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Generate_MissingTable_FallsBackToRomWaveTable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"robotron-missing-{Guid.NewGuid():N}.csv");
        var generator = new LevelParameterGenerator(new Random(7), path);

        LevelParameters p = generator.Generate(1);

        Assert.Equal(15, p.GruntCount); // ROM wave 1
        Assert.Equal(5, p.ElectrodeCount);
    }

    [Fact]
    public void Generate_MalformedTable_FallsBackToRomWaveTable()
    {
        string path = Path.Combine(Path.GetTempPath(), $"robotron-bad-{Guid.NewGuid():N}.csv");
        File.WriteAllText(
            path,
            "Level,GruntCount,HulkCount,SpheroidCount,QuarkCount,ElectrodeCount,MaxEnforcersPerSpheroid,MaxTanksPerQuark\n" +
            "1,notanumber,2,1,1,10,3,2\n");
        try
        {
            var generator = new LevelParameterGenerator(new Random(7), path);
            LevelParameters p = generator.Generate(1);

            Assert.Equal(15, p.GruntCount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
