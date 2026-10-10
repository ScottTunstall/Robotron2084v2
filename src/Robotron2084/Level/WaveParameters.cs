namespace Robotron2084.Level;

/// <summary>One row of the arcade's wave table: how many of each robot a wave has, and how fast and how often they act.</summary>
/// <remarks>
///     The values are the ones the arcade stores for the recommended difficulty. <see cref="DifficultyTuning" />
///     moves them for other settings.
/// </remarks>
/// <param name="ResolvedWave">The wave this row is for, from 1 to 40. Later waves repeat the second half of the table.</param>
/// <param name="GruntCount">How many grunts the wave starts with.</param>
/// <param name="ElectrodeCount">How many electrodes the wave starts with.</param>
/// <param name="MommyCount">How many Mommies the wave starts with.</param>
/// <param name="DaddyCount">How many Daddies the wave starts with.</param>
/// <param name="MikeyCount">How many Mikeys the wave starts with.</param>
/// <param name="HulkCount">How many hulks the wave starts with.</param>
/// <param name="BrainCount">How many brains the wave starts with.</param>
/// <param name="SpheroidCount">How many spheroids the wave starts with.</param>
/// <param name="QuarkCount">How many quarks the wave starts with.</param>
/// <param name="MaxDropsX2">
///     Twice the most that a spheroid or quark may drop. Each rolls a number up to this and drops
///     half of it, rounded up.
/// </param>
/// <param name="GruntMoveDelay">The longest a grunt waits between moves, in beats. A smaller number is a faster grunt.</param>
/// <param name="GruntSpeedFloor">The fewest beats that the grunts' speed-ups may bring a grunt's longest wait down to.</param>
/// <param name="EnforcerFireDelay">How long an enforcer waits between sparks. A smaller number is faster fire.</param>
/// <param name="SpheroidDropDelay">
///     How long a spheroid waits before dropping an enforcer. A smaller number is a quicker
///     drop.
/// </param>
/// <param name="HulkBeatIntervalRomFrames">
///     How many fiftieths of a second pass between a hulk's beats. A smaller number is
///     a faster hulk.
/// </param>
/// <param name="BrainFireDelay">The longest a brain waits between cruise missiles, in beats.</param>
/// <param name="BrainBeatWaitRomFrames">
///     How many fiftieths of a second a brain waits after each beat. A smaller number is
///     a faster brain.
/// </param>
/// <param name="TankFireDelay">How many beats a tank waits between shells. A smaller number is faster fire.</param>
/// <param name="ShellSpeed">How fast tank shells fly. A bigger number is a faster shell.</param>
/// <param name="QuarkDropDelay">How long a quark waits before dropping a tank. A smaller number is a quicker drop.</param>
/// <param name="QuarkSpeedCap">How fast a quark may drift. A bigger number is a faster quark.</param>
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
