namespace Robotron2084.Level;

/// <summary>Everything that makes one wave what it is: how many of each robot it has, and how fast and how often they act.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the wave's
/// counts and settings when a wave begins</item>
/// <item>Disassembly: <c>INITIALISE_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c>
/// (<c>$2B7C</c>)</item>
/// </list> The values come from the arcade's wave tables (<see
/// cref="WaveTable"/>, notes §11). A parameter left out gets a value that a hand-built wave in a test
/// can use. <c>MaxDropsX2</c> is the arcade's <c>ENFNUM</c>: a spheroid or quark rolls a number up to
/// it and drops half of it, rounded up. <c>TankCount</c> is the tanks already on the field when a man
/// starts. The wave table never has any, because quarks drop them, but a death keeps the ones that
/// were alive (notes §134). <c>BerzerkRobotCount</c> and <c>GorfCount</c> are the author's own
/// robots, which no wave has yet. Times are counted in ROM frames, and the port turns them into its
/// own ticks where they are used.</item>
/// </list>
/// </remarks>
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
    int EnemySpeedBonus = 0,
    int TankCount = 0,
    int BerzerkRobotCount = 0,
    int GorfCount = 0)
{
    /// <summary>Makes the parameters for a wave from the arcade's wave table.</summary>
    /// <param name="levelNumber">The wave number.</param>
    /// <param name="wave">The wave table's row for it.</param>
    /// <remarks><c>MaxEnforcersPerSpheroid</c> and <c>MaxTanksPerQuark</c> are kept in step with <c>MaxDropsX2</c>, for anything that still reads them.</remarks>
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
