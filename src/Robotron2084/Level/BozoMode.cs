using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Makes the first few waves easier for a player who is losing men early. The arcade's second release added it, because new players found the game too hard.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>PLRES</c>, with the table <c>BOZOTB</c></item>
/// <item>Disassembly: <c>$2B26</c> to <c>$2B68</c>, with the table at <c>$2B59</c></item>
/// </list>
/// It runs each time a player's wave is loaded. On the first four waves, a player with no spare men left gets it. On the first two waves, so does a player who has lost a man.
/// The wave's enforcers, spheroids and grunts are then slowed to the values in the table.
/// </remarks>
public static class BozoMode
{
    /// <summary>The last wave the mercy is given on.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLRES</c>, <c>CMPA #4 / BHI</c>. Disassembly: not separately labelled.</remarks>
    private const int LastWave = 4;

    /// <summary>The last wave a player who has lost a man, but still has spare ones, is given the mercy on.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLRES</c>, <c>CMPA #2 / BHI</c>. Disassembly: not separately labelled.</remarks>
    private const int LastWaveForBehindPlayer = 2;

    /// <summary>One row of the table: the four settings for a wave, in the table's own order.</summary>
    /// <param name="SpheroidDropDelay">How long a spheroid waits before dropping an enforcer.</param>
    /// <param name="EnforcerFireDelay">How long an enforcer waits between sparks.</param>
    /// <param name="GruntMoveDelay">The longest a grunt waits between moves, in beats.</param>
    /// <param name="GruntSpeedFloor">The fewest beats the grunts' speed-ups may bring a grunt's longest wait down to.</param>
    private sealed record Row(int SpheroidDropDelay, int EnforcerFireDelay, int GruntMoveDelay, int GruntSpeedFloor);

    /// <summary>The settings for waves 1 to 4.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>BOZOTB</c>. Disassembly: <c>$2B59</c>.</remarks>
    private static readonly Row[] Table =
    [
        new(38, 96, 30, 15),
        new(38, 96, 25, 12),
        new(36, 48, 20, 10),
        new(30, 30, 15, 7),
    ];

    /// <summary>Says whether the mercy is given to a player who is about to play a wave.</summary>
    /// <param name="wave">The wave, starting at 1.</param>
    /// <param name="spareMen">The men the player has left besides the one in play.</param>
    /// <param name="shipsPerGame">The men each player starts with: the TURNS PER PLAYER setting (notes §131).</param>
    public static bool AppliesTo(int wave, int spareMen, int shipsPerGame = PlayerTuning.StartingLives)
    {
        if (wave > LastWave)
        {
            return false;
        }

        if (spareMen == 0)
        {
            return true;
        }

        return wave <= LastWaveForBehindPlayer && shipsPerGame - 1 > spareMen;
    }

    /// <summary>Gives a wave's parameters, slowed down when the mercy is given.</summary>
    /// <param name="parameters">The wave table's parameters.</param>
    /// <param name="spareMen">The men the player has left besides the one in play.</param>
    /// <param name="shipsPerGame">The men each player starts with (see <see cref="AppliesTo"/>).</param>
    public static LevelParameters Apply(LevelParameters parameters, int spareMen, int shipsPerGame = PlayerTuning.StartingLives)
    {
        if (!AppliesTo(parameters.LevelNumber, spareMen, shipsPerGame))
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
