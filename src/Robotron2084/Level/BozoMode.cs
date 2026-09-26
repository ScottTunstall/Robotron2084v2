namespace Robotron2084.Level;

/// <summary>
/// The arcade's mercy for a player who is losing ships early: the second release's "BOZO MODE" (RRG23 <c>PLRES</c>,
/// R5 $2B26-$2B68). Larry DeMar: it was added after field complaints that the game was too brutal for new players.
/// </summary>
/// <remarks>
/// It runs each time a player's wave is loaded. On waves 1 to 4, a player with no spare men left, or on waves 1 and 2
/// a player down on the ships the operator awards per game, gets the wave's enforcer, spheroid and grunt settings
/// dialled down to a four-row table (<c>BOZOTB</c>, R5 $2B59).
/// </remarks>
public static class BozoMode
{
    /// <summary>The last wave the mercy applies to (<c>CMPA #4 / BHI</c>).</summary>
    private const int LastWave = 4;

    /// <summary>The last wave a merely-behind player gets it on (<c>CMPA #2 / BHI</c>).</summary>
    private const int LastWaveForBehindPlayer = 2;

    /// <summary>The ships the operator awards a player (ROM <c>NSHIP</c>, the CMOS "turns per player"); the default is 3.</summary>
    private const int ShipsPerGame = 3;

    /// <summary>One row of <c>BOZOTB</c>: the four settings for a wave, in the table's own byte order.</summary>
    /// <param name="SpheroidDropDelay">ROM <c>CDPTIM</c>.</param>
    /// <param name="EnforcerFireDelay">ROM <c>ENSTIM</c>.</param>
    /// <param name="GruntMoveDelay">ROM <c>ROBSPD</c>.</param>
    /// <param name="GruntSpeedFloor">ROM <c>RMXSPD</c>.</param>
    private sealed record Row(int SpheroidDropDelay, int EnforcerFireDelay, int GruntMoveDelay, int GruntSpeedFloor);

    /// <summary>ROM <c>BOZOTB</c> (R5 $2B59), waves 1 to 4.</summary>
    private static readonly Row[] Table =
    [
        new(38, 96, 30, 15),
        new(38, 96, 25, 12),
        new(36, 48, 20, 10),
        new(30, 30, 15, 7),
    ];

    /// <summary>Whether the mercy applies to a player about to play a wave.</summary>
    /// <param name="wave">The wave, 1-based.</param>
    /// <param name="spareMen">The men the player has left besides the one in play (ROM <c>PLAS</c>).</param>
    public static bool AppliesTo(int wave, int spareMen)
    {
        if (wave > LastWave)
        {
            return false;
        }

        if (spareMen == 0)
        {
            return true;
        }

        return wave <= LastWaveForBehindPlayer && ShipsPerGame - 1 > spareMen;
    }

    /// <summary>The wave's parameters, dialled down when the mercy applies.</summary>
    /// <param name="parameters">The wave table's parameters.</param>
    /// <param name="spareMen">The men the player has left besides the one in play.</param>
    public static LevelParameters Apply(LevelParameters parameters, int spareMen)
    {
        if (!AppliesTo(parameters.LevelNumber, spareMen))
        {
            return parameters;
        }

        Row row = Table[parameters.LevelNumber - 1];
        return parameters with
        {
            SpheroidDropDelay = row.SpheroidDropDelay,
            EnforcerFireDelay = row.EnforcerFireDelay,
            GruntMoveDelay = row.GruntMoveDelay,
            GruntSpeedFloor = row.GruntSpeedFloor,
        };
    }
}
