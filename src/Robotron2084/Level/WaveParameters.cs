namespace Robotron2084.Level;

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
///     </list>
///     The values come from the arcade's wave tables (
///     <see
///         cref="WaveTable" />
///     , notes §11). A parameter left out gets a value that a hand-built wave in a test
///     can use. <c>MaxDropsX2</c> is the arcade's <c>ENFNUM</c>: a spheroid or quark picks a random number up to
///     it and drops half of it, rounded up. <c>TankCount</c> is the tanks already on the field when a life
///     starts. The wave table never has any, because quarks drop them, but a death keeps the ones that
///     were alive (notes §134). <c>BerzerkRobotCount</c> and <c>GorfCount</c> are the author's own
///     robots, which no wave has yet. <c>GorfGruntDropCount</c> is how many grunts in all each Gorf drops: 0 leaves
///     it to chance, as in any other wave, and a number makes it drop exactly that many, shared over its stops.
///     <c>PlayfieldWallsOpen</c> says whether doors open in the playfield's walls and let grunts in en masse (port only,
///     notes §151). Any wave can have it. <c>GorfAvoidsPlayer</c> says that Gorf, when it appears, is never on the same
///     level as the player, so it is not on a collision course with the player (port only, notes §152). Without it,
///     Gorf may appear on the player's level and the player has to move out of its way. Times are counted in fiftieths of a second, and the port turns them into its
///     own ticks where they are used.</item>
///     </list>
/// </remarks>
public sealed record WaveParameters(
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
    int GorfCount = 0,
    int GorfGruntDropCount = 0,
    bool PlayfieldWallsOpen = false,
    bool GorfAvoidsPlayer = false)
{
    /// <summary>Gets whether the wave has Gorf: the author's own robot, which the arcade's waves never have.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Port only.</item>
    ///         <item>It is true when <see cref="GorfCount" /> is more than 0, so the flag and the count cannot disagree.</item>
    ///     </list>
    /// </remarks>
    public bool HasGorf()
    {
        return GorfCount > 0;
    }

    /// <summary>Makes the parameters for a wave from the arcade's wave table.</summary>
    /// <param name="waveNumber">The wave number.</param>
    /// <param name="waveTableRow">The wave table's row for it.</param>
    /// <remarks>
    ///     <c>MaxEnforcersPerSpheroid</c> and <c>MaxTanksPerQuark</c> are kept in step with <c>MaxDropsX2</c>, for
    ///     anything that still reads them.
    /// </remarks>
    public static WaveParameters CreateFromWave(int waveNumber, WaveTableRow waveTableRow)
    {
        return new WaveParameters(
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
