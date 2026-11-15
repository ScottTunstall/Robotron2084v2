using Robotron2084.Level.Models;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Level;

public sealed class WaveParamsGeneratorTests
{
    /// <summary>The waves in the arcade's wave table.</summary>
    private const int ArcadeWaveCount = 40;

    /// <summary>The last wave the style tests look at: past the table's end, since waves 41 to 60 repeat 21 to 40.</summary>
    private const int LastWaveChecked = 60;

    [Fact]
    public void Generate_UsesArcadeWaveTable_ExactValues()
    {
        var p = new WaveParamsGenerator().Generate(1, GameStyle.Classic);

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
        var p = new WaveParamsGenerator().Generate(5, GameStyle.Classic);

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
        var p = new WaveParamsGenerator().Generate(9, GameStyle.Classic);

        Assert.Equal(60, p.GruntCount);
        Assert.Equal(0, p.ElectrodeCount);
        Assert.Equal(5, p.SpheroidCount);
    }

    [Fact]
    public void Generate_WavesAbove40_RepeatWaves21To40()
    {
        WaveParamsGenerator waveParamsGenerator = new();

        var w41 = waveParamsGenerator.Generate(41, GameStyle.Classic);
        var w21 = waveParamsGenerator.Generate(21, GameStyle.Classic);
        var w60 = waveParamsGenerator.Generate(60, GameStyle.Classic);
        var w40 = waveParamsGenerator.Generate(40, GameStyle.Classic);
        var w61 = waveParamsGenerator.Generate(61, GameStyle.Classic);

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
            var waveParamsGenerator = new WaveParamsGenerator(path);

            var level1 = waveParamsGenerator.Generate(1, GameStyle.Classic);
            Assert.Equal(5, level1.GruntCount);
            Assert.Equal(2, level1.HulkCount);
            Assert.Equal(1, level1.SpheroidCount);
            Assert.Equal(1, level1.QuarkCount);
            Assert.Equal(10, level1.ElectrodeCount);
            Assert.Equal(3, level1.MaxEnforcersPerSpheroid);
            Assert.Equal(2, level1.MaxTanksPerQuark);
            Assert.Equal(0, level1.EnemySpeedBonus);

            var level2 = waveParamsGenerator.Generate(2, GameStyle.Classic);
            Assert.Equal(9, level2.GruntCount);
            Assert.Equal(4, level2.MaxEnforcersPerSpheroid);
            Assert.Equal(1, level2.EnemySpeedBonus); // still computed from the wave number

            var level3 = waveParamsGenerator.Generate(3, GameStyle.Classic); // wraps back to row 1
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
        var waveParamsGenerator = new WaveParamsGenerator(path);

        var p = waveParamsGenerator.Generate(1, GameStyle.Classic);

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
            var waveParamsGenerator = new WaveParamsGenerator(path);
            var p = waveParamsGenerator.Generate(1, GameStyle.Classic);

            Assert.Equal(15, p.GruntCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ClassicWavesHaveBerzerkSpeakAndGorfSpeakOff()
    {
        WaveParamsGenerator waveParamsGenerator = new();

        for (var waveNumber = 1; waveNumber <= LastWaveChecked; waveNumber++)
        {
            var waveParams = waveParamsGenerator.Generate(waveNumber, GameStyle.Classic);

            Assert.False(waveParams.EnableBerzerkSpeak, $"wave {waveNumber}");
            Assert.False(waveParams.EnableGorfSpeak, $"wave {waveNumber}");
        }
    }

    [Fact]
    public void ExtendedWaveIsTheClassicWaveApartFromItsExtendedSettings()
    {
        WaveParamsGenerator waveParamsGenerator = new();

        for (var waveNumber = 1; waveNumber <= LastWaveChecked; waveNumber++)
        {
            var classic = waveParamsGenerator.Generate(waveNumber, GameStyle.Classic);
            var extended = waveParamsGenerator.Generate(waveNumber, GameStyle.Extended);

            Assert.Equal(classic, extended with { EnableBerzerkSpeak = false, EnableGorfSpeak = false });
        }
    }

    [Fact]
    public void ExtendedWavesHaveBerzerkSpeakOnTheGruntWavesWithNoBrainsOrQuarksOnly()
    {
        WaveParamsGenerator waveParamsGenerator = new();
        int[] berzerkSpeakWaves = [1, 2, 3, 4, 6, 8, 9, 11, 13, 14, 16, 18, 19, 21, 23, 26];

        for (var waveNumber = 1; waveNumber <= ArcadeWaveCount; waveNumber++)
        {
            var waveParams = waveParamsGenerator.Generate(waveNumber, GameStyle.Extended);

            Assert.Equal(berzerkSpeakWaves.Contains(waveNumber), waveParams.EnableBerzerkSpeak);
        }
    }

    [Theory]
    [InlineData(5)] // 20 grunts and 15 brains
    [InlineData(40)] // 30 grunts and 25 brains
    public void ExtendedBrainWaveHasNoBerzerkSpeak(int waveNumber)
    {
        var waveParams = new WaveParamsGenerator().Generate(waveNumber, GameStyle.Extended);

        Assert.True(waveParams.GruntCount > 0);
        Assert.True(waveParams.BrainCount > 0);
        Assert.False(waveParams.EnableBerzerkSpeak);
    }

    [Theory]
    [InlineData(7)] // quarks and no grunts
    [InlineData(28)] // 35 grunts and 1 quark
    [InlineData(34)] // 30 grunts and 2 quarks
    public void ExtendedTankWaveHasNoBerzerkSpeak(int waveNumber)
    {
        var waveParams = new WaveParamsGenerator().Generate(waveNumber, GameStyle.Extended);

        Assert.True(waveParams.QuarkCount > 0);
        Assert.False(waveParams.EnableBerzerkSpeak);
    }

    [Fact]
    public void ExtendedWavesHaveGorfSpeakOff()
    {
        WaveParamsGenerator waveParamsGenerator = new();

        for (var waveNumber = 1; waveNumber <= LastWaveChecked; waveNumber++)
            Assert.False(waveParamsGenerator.Generate(waveNumber, GameStyle.Extended).EnableGorfSpeak, $"wave {waveNumber}");
    }

    [Fact]
    public void GenerateRejectsAGameStyleThatIsNotOne()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new WaveParamsGenerator().Generate(1, (GameStyle)99));

        Assert.Equal("gameStyle", exception.ParamName);
    }
}
