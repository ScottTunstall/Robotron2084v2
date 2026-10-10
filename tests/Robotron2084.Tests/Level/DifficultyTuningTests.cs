using Robotron2084.Level;
using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     The arcade's DIFFICULTY OF PLAY adjustment (notes §131) — the ROM routine at $2B7C and the
///     <c>$2C20</c> tables it walks. The expectations are the ROM's own arithmetic worked through by
///     hand: <c>round(value × (|difficulty − 5| × multiplier5bits) / 256)</c>, added or subtracted by
///     the sign bit, clamped to the record's min/max.
/// </summary>
public sealed class DifficultyTuningTests
{
    private static LevelParameters Wave(int number)
    {
        return LevelParameters.CreateFromWave(number, WaveTable.GetParameters(number));
    }

    [Fact]
    public void TheRecommendedDifficultyChangesNothing()
    {
        var parameters = Wave(6);

        Assert.Same(parameters, DifficultyTuning.Apply(parameters, GameSettings.RecommendedDifficulty, 3));
    }

    [Fact]
    public void HarderThanRecommendedFallsTheReversedValuesAndRaisesTheRest()
    {
        // Wave 6 at difficulty 8: |8 − 5| = 3, and every record's multiplier bits are 14.
        var adjusted = DifficultyTuning.Apply(Wave(6), 8, 3);

        Assert.Equal(13, adjusted.GruntMoveDelay); // 15 − round(15×42/256)=15−2
        Assert.Equal(4, adjusted.GruntSpeedFloor); // 5 − 1
        Assert.Equal(12, adjusted.MaxDropsX2); // 10 + 2
        Assert.Equal(6, adjusted.MaxEnforcersPerSpheroid); // ceil(12/2), following ENFNUM
        Assert.Equal(6, adjusted.MaxTanksPerQuark);
        Assert.Equal(17, adjusted.EnforcerFireDelay); // 20 − 3
        Assert.Equal(17, adjusted.SpheroidDropDelay); // 20 − 3
        Assert.Equal(6, adjusted.HulkBeatIntervalRomFrames); // 7 − 1
        Assert.Equal(33, adjusted.BrainFireDelay); // 40 − 7
        Assert.Equal(6, adjusted.BrainBeatWaitRomFrames); // 7 − 1
        Assert.Equal(27, adjusted.TankFireDelay); // 32 − 5
        Assert.Equal(205, adjusted.ShellSpeed); // 176 + 29
        Assert.Equal(13, adjusted.QuarkDropDelay); // 16 − 3
        Assert.Equal(58, adjusted.QuarkSpeedCap); // 50 + 8
    }

    [Fact]
    public void EasierThanRecommendedGoesTheOtherWay()
    {
        // Wave 6 at difficulty 2, two men left so the mercy does not quietly step it back to 5.
        var adjusted = DifficultyTuning.Apply(Wave(6), 2, 2);

        Assert.Equal(17, adjusted.GruntMoveDelay); // 15 + 2
        Assert.Equal(8, adjusted.MaxDropsX2); // 10 − 2
        Assert.Equal(4, adjusted.MaxEnforcersPerSpheroid); // ceil(8/2)
        Assert.Equal(23, adjusted.EnforcerFireDelay); // 20 + 3
        Assert.Equal(8, adjusted.HulkBeatIntervalRomFrames); // 7 + 1
        Assert.Equal(47, adjusted.BrainFireDelay); // 40 + 7
        Assert.Equal(160, adjusted.ShellSpeed); // 176 − 29, clamped to the record's floor
        Assert.Equal(19, adjusted.QuarkDropDelay); // 16 + 3
        Assert.Equal(42, adjusted.QuarkSpeedCap); // 50 − 8
    }

    [Fact]
    public void AnEasierSettingIsRaisedToRecommendedForAPlayerAtWaveFourteen()
    {
        var parameters = Wave(14);

        Assert.Same(parameters, DifficultyTuning.Apply(parameters, 2, 1));
    }

    [Fact]
    public void AnEasierSettingIsRaisedToRecommendedFromWaveFiveWithThreeMenLeft()
    {
        var parameters = Wave(5);

        Assert.Same(parameters, DifficultyTuning.Apply(parameters, 2, 3));
    }

    [Fact]
    public void TheMercyDoesNotApplyFromWaveFiveWithFewerThanThreeMen()
    {
        var adjusted = DifficultyTuning.Apply(Wave(5), 2, 2);

        Assert.Equal(8, adjusted.MaxDropsX2);
    }

    [Fact]
    public void TheMercyDoesNotApplyBeforeWaveFive()
    {
        var adjusted = DifficultyTuning.Apply(Wave(4), 2, 3);

        Assert.Equal(8, adjusted.MaxDropsX2);
    }

    [Theory]
    [InlineData(6, 8)]
    [InlineData(6, 10)]
    [InlineData(1, 6)]
    public void AHarderSettingIsNeverMercied(int wave, int difficulty)
    {
        var parameters = Wave(wave);

        Assert.NotSame(parameters, DifficultyTuning.Apply(parameters, difficulty, 1));
    }
}
