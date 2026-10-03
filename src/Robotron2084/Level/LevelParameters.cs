namespace Robotron2084.Level;

/// <summary>
/// All arcade parameters for one level. They come
/// from the Release-5 ROM tables (<see cref="WaveTable"/> /
/// arcade-fidelity-notes §11) — waves 1-40 unique, 41+ repeat 21-40.
///
/// New fields have defaults so hand-built parameter sets in tests and the
/// content pipeline keep compiling; <c>LevelParameterGenerator</c> fills
/// every field from the ROM.
///
/// Field notes (ROM names in arcade-fidelity-notes §11.2):
/// <list type="bullet">
/// <item><c>MaxDropsX2</c> = ENFNUM — at spawn time a spheroid/quark rolls
/// RND(0..ENFNUM) and drops ceil(result/2) children (ROM 4B66-4B6F / $1193).</item>
/// <item>Timed fields are in ROM game ticks (NAP units); the port converts
/// at the use sites (notes §11 + the tempo mapping recorded in the notes
/// progress log).</item>
/// </list>
/// </summary>
public sealed record LevelParameters(
    int LevelNumber,
    int GruntCount = 0,
    int ElectrodeCount = 0,
    int MommyCount = 0,
    int DaddyCount = 0,
    int MikeyCount = 0,
    int HulkCount = 0,
    int BrainCount = 0,
    int SpheroidCount = 0,
    int QuarkCount = 0,
    int MaxDropsX2 = 10,
    int MaxEnforcersPerSpheroid = 5,
    int MaxTanksPerQuark = 5,
    int GruntMoveDelay = 15,
    int GruntSpeedFloor = 4,
    int EnforcerFireDelay = 24,
    int SpheroidDropDelay = 24,
    int HulkBeatIntervalRomFrames = 7,
    int BrainFireDelay = 40,
    int BrainBeatWaitRomFrames = 8,
    int TankFireDelay = 32,
    int ShellSpeed = 176,
    int QuarkDropDelay = 16,
    int QuarkSpeedCap = 50,
    int EnemySpeedBonus = 0)
{
    /// <summary>
    /// Fills every field from the ROM wave table. The legacy
    /// <c>MaxEnforcersPerSpheroid</c>/<c>MaxTanksPerQuark</c> fields are kept
    /// in sync with ceil(ENFNUM/2) for anything still reading them.
    /// </summary>
    public static LevelParameters CreateFromWave(int levelNumber, WaveParameters wave) => new(
        LevelNumber: levelNumber,
        GruntCount: wave.GruntCount,
        ElectrodeCount: wave.ElectrodeCount,
        MommyCount: wave.MommyCount,
        DaddyCount: wave.DaddyCount,
        MikeyCount: wave.MikeyCount,
        HulkCount: wave.HulkCount,
        BrainCount: wave.BrainCount,
        SpheroidCount: wave.SpheroidCount,
        QuarkCount: wave.QuarkCount,
        MaxDropsX2: wave.MaxDropsX2,
        MaxEnforcersPerSpheroid: (wave.MaxDropsX2 + 1) / 2,
        MaxTanksPerQuark: (wave.MaxDropsX2 + 1) / 2,
        GruntMoveDelay: wave.GruntMoveDelay,
        GruntSpeedFloor: wave.GruntSpeedFloor,
        EnforcerFireDelay: wave.EnforcerFireDelay,
        SpheroidDropDelay: wave.SpheroidDropDelay,
        HulkBeatIntervalRomFrames: wave.HulkBeatIntervalRomFrames,
        BrainFireDelay: wave.BrainFireDelay,
        BrainBeatWaitRomFrames: wave.BrainBeatWaitRomFrames,
        TankFireDelay: wave.TankFireDelay,
        ShellSpeed: wave.ShellSpeed,
        QuarkDropDelay: wave.QuarkDropDelay,
        QuarkSpeedCap: wave.QuarkSpeedCap,
        EnemySpeedBonus: 0);
}
