namespace Robotron2084.Level.Models;

/// <summary>Everything that makes one wave what it is: how many of each robot it has, and how fast and how often they act.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the wave's
///             counts and settings when a wave begins
///         </item>
///         <item>
///             Disassembly: <c>INITIALISE_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c>
///             (<c>$2B7C</c>)
///         </item>
///         <item>The values come from the arcade's wave tables (<see cref="WaveTable" />, notes §11).</item>
///         <item>A parameter left out gets a value that a hand-built wave in a test can use.</item>
///         <item>Times are counted in fiftieths of a second, and the port turns them into its own ticks where they are used.</item>
///     </list>
/// </remarks>
/// <param name="WaveNumber">The wave's number, counted from 1.</param>
/// <param name="GruntCount">How many grunts the wave starts with.</param>
/// <param name="ElectrodeCount">How many electrodes.</param>
/// <param name="MommyCount">How many Mommies.</param>
/// <param name="DaddyCount">How many Daddies.</param>
/// <param name="MikeyCount">How many Mikeys.</param>
/// <param name="HulkCount">How many hulks.</param>
/// <param name="BrainCount">How many brains.</param>
/// <param name="SpheroidCount">How many spheroids.</param>
/// <param name="QuarkCount">How many quarks.</param>
/// <param name="MaxDropsX2">
///     The arcade's <c>ENFNUM</c>: a spheroid or quark picks a random number up to it and drops half of it, rounded up.
/// </param>
/// <param name="MaxEnforcersPerSpheroid">The most enforcers a spheroid may drop, kept in step with <paramref name="MaxDropsX2" />.</param>
/// <param name="MaxTanksPerQuark">The most tanks a quark may drop, kept in step with <paramref name="MaxDropsX2" />.</param>
/// <param name="GruntMoveDelay">The most beats a grunt waits between steps.</param>
/// <param name="GruntSpeedFloor">The fewest beats a grunt's wait can be cut down to as the wave goes on.</param>
/// <param name="EnforcerFireDelay">The most beats an enforcer waits between sparks.</param>
/// <param name="SpheroidDropDelay">The most times a spheroid spins before it drops an enforcer.</param>
/// <param name="HulkBeatIntervalArcadeFrames">How many arcade frames a hulk waits between steps.</param>
/// <param name="BrainFireDelay">The longest a brain waits between cruise missiles, in beats.</param>
/// <param name="BrainBeatWaitArcadeFrames">How long a brain waits after each beat, in arcade frames.</param>
/// <param name="TankFireDelay">How many beats a tank waits between shots.</param>
/// <param name="ShellSpeed">The wave's shell setting. A bigger number is a faster shell.</param>
/// <param name="QuarkDropDelay">The most beats a quark waits between tank drops.</param>
/// <param name="QuarkMaxSpeed">A quark's top drifting speed.</param>
/// <param name="EnemySpeedBonus">How much faster the enemies are on this wave: 0 on wave 1, rising by one a wave to a limit.</param>
/// <param name="TankCount">
///     The tanks already on the field when a life starts. The wave table never has any, because quarks drop them, but a
///     death keeps the ones that were alive (notes §134).
/// </param>
/// <param name="BerzerkRobotCount">How many of the author's own Berzerk robots the wave has. No wave has any yet.</param>
/// <param name="EnableBerzerkSpeak">
///     Whether the Berzerk robot speaks on this wave. Only a <see cref="GameStyle.Extended" /> wave can have it on; a
///     <see cref="GameStyle.Classic" /> wave always has it off.
/// </param>
/// <param name="EnableGorfSpeak">
///     Whether Gorf speaks on this wave. Only a <see cref="GameStyle.Extended" /> wave can have it on; a
///     <see cref="GameStyle.Classic" /> wave always has it off.
/// </param>
public sealed record WaveParams(
    int WaveNumber,
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
    int HulkBeatIntervalArcadeFrames = 7,
    int BrainFireDelay = 40,
    int BrainBeatWaitArcadeFrames = 8,
    int TankFireDelay = 32,
    int ShellSpeed = 176,
    int QuarkDropDelay = 16,
    int QuarkMaxSpeed = 50,
    int EnemySpeedBonus = 0,
    int TankCount = 0,
    int BerzerkRobotCount = 0,
    bool EnableBerzerkSpeak = false,
    bool EnableGorfSpeak = false)
{
    /// <summary>Makes the parameters for a wave from the arcade's wave table.</summary>
    /// <param name="waveNumber">The wave number.</param>
    /// <param name="waveTableRow">The wave table's row for it.</param>
    /// <remarks>
    ///     <c>MaxEnforcersPerSpheroid</c> and <c>MaxTanksPerQuark</c> are kept in step with <c>MaxDropsX2</c>, for
    ///     anything that still reads them.
    /// </remarks>
    public static WaveParams CreateFromWave(int waveNumber, WaveTableRow waveTableRow)
    {
        return new WaveParams(
            waveNumber,
            waveTableRow.GruntCount,
            waveTableRow.ElectrodeCount,
            waveTableRow.MommyCount,
            waveTableRow.DaddyCount,
            waveTableRow.MikeyCount,
            waveTableRow.HulkCount,
            waveTableRow.BrainCount,
            waveTableRow.SpheroidCount,
            waveTableRow.QuarkCount,
            waveTableRow.MaxDropsX2,
            (waveTableRow.MaxDropsX2 + 1) / 2,
            (waveTableRow.MaxDropsX2 + 1) / 2,
            waveTableRow.GruntMoveDelay,
            waveTableRow.GruntSpeedFloor,
            waveTableRow.EnforcerFireDelay,
            waveTableRow.SpheroidDropDelay,
            waveTableRow.HulkBeatIntervalArcadeFrames,
            waveTableRow.BrainFireDelay,
            waveTableRow.BrainBeatWaitArcadeFrames,
            waveTableRow.TankFireDelay,
            waveTableRow.ShellSpeed,
            waveTableRow.QuarkDropDelay,
            waveTableRow.QuarkMaxSpeed,
            0);
    }
}
