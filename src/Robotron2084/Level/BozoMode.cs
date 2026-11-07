using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
///     Makes the first few waves easier for a player who is losing lives early. The arcade's second release added it,
///     because new players found the game too hard.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>PLRES</c>, with the table
///             <c>BOZOTB</c>
///         </item>
///         <item>Disassembly: <c>$2B26</c> to <c>$2B68</c>, with the table at <c>$2B59</c></item>
///     </list>
///     It runs
///     each time a player's wave is loaded. On the first four waves, a player with no spare lives left gets
///     it. On the first two waves, so does a player who has lost a life. The wave's enforcers, spheroids
///     and grunts are then slowed to the values in the table.</item>
///     </list>
/// </remarks>
public static class BozoMode
{
    /// <summary>The last wave the mercy is given on.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLRES</c>, <c>CMPA #4 / BHI</c>.</item>
    ///         <item>Disassembly: <c>LOAD_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c> ($2B0B), the <c>CMPA #$04</c> at $2B22.</item>
    ///     </list>
    /// </remarks>
    private const int LastWave = 4;

    /// <summary>The last wave a player who has lost a life, but still has spare ones, is given the mercy on.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLRES</c>, <c>CMPA #2 / BHI</c>.</item>
    ///         <item>Disassembly: <c>LOAD_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c> ($2B0B), the <c>CMPA #$02</c> at $2B2D.</item>
    ///     </list>
    /// </remarks>
    private const int LastWaveForBehindPlayer = 2;

    /// <summary>The settings for waves 1 to 4.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>BOZOTB</c>.</item>
    ///         <item>Disassembly: <c>$2B59</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly BozoModeRow[] Table =
    [
        new(38, 96, 30, 15),
        new(38, 96, 25, 12),
        new(36, 48, 20, 10),
        new(30, 30, 15, 7)
    ];

    /// <summary>Says whether the mercy is given to a player who is about to play a wave.</summary>
    /// <param name="wave">The wave, starting at 1.</param>
    /// <param name="spareLives">The lives the player has left besides the one in play.</param>
    /// <param name="startingLives">The lives each player starts with: the TURNS PER PLAYER setting (notes §131).</param>
    public static bool AppliesTo(int wave, int spareLives, int startingLives = PlayerTuning.StartingLives)
    {
        if (wave > LastWave) return false;

        if (spareLives == 0) return true;

        return wave <= LastWaveForBehindPlayer && startingLives - 1 > spareLives;
    }

    /// <summary>Gives a wave's parameters, slowed down when the mercy is given.</summary>
    /// <param name="waveParameters">The wave table's parameters.</param>
    /// <param name="spareLives">The lives the player has left besides the one in play.</param>
    /// <param name="startingLives">The lives each player starts with (see <see cref="AppliesTo" />).</param>
    public static WaveParameters Apply(WaveParameters waveParameters, int spareLives,
        int startingLives = PlayerTuning.StartingLives)
    {
        if (!AppliesTo(waveParameters.WaveNumber, spareLives, startingLives)) return waveParameters;

        var row = Table[waveParameters.WaveNumber - 1];
        return waveParameters with
        {
            SpheroidDropDelay = row.SpheroidDropDelay,
            EnforcerFireDelay = row.EnforcerFireDelay,
            GruntMoveDelay = row.GruntMoveDelay,
            GruntSpeedFloor = row.GruntSpeedFloor
        };
    }
}
