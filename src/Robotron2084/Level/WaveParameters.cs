namespace Robotron2084.Level;

/// <summary>All arcade parameters for one wave (values as stored at default difficulty).</summary>
public sealed record WaveParameters(
    int ResolvedWave,
    int GruntCount,
    int ElectrodeCount,
    int MommyCount,
    int DaddyCount,
    int MikeyCount,
    int HulkCount,
    int BrainCount,
    int SpheroidCount,
    int QuarkCount,
    int MaxDropsX2,
    int GruntMoveDelay,
    int GruntSpeedFloor,
    int EnforcerFireDelay,
    int SpheroidDropDelay,
    int HulkBeatIntervalRomFrames,
    int BrainFireDelay,
    int BrainBeatWaitRomFrames,
    int TankFireDelay,
    int ShellSpeed,
    int QuarkDropDelay,
    int QuarkSpeedCap);
