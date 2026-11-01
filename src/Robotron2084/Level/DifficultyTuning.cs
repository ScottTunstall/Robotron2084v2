using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>
///     Makes a wave easier or harder to match the difficulty setting, by moving the speeds and delays the wave table
///     gives.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the
///             difficulty setting and adjusts the wave
///         </item>
///         <item>
///             Disassembly: <c>INITIALISE_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c>
///             (<c>$2B7C</c>)
///         </item>
///     </list>
///     Each of the twelve values is moved away from the recommended one, by
///     an amount that grows with how far the setting is from the recommended one, and is then kept
///     between the minimum and maximum its record in the table at <c>$2C20</c> allows (notes §131). At
///     the recommended setting nothing moves. The amount is worked out as the arcade works it out, byte
///     for byte: the value times the distance from the recommended setting times the record's multiplier,
///     with the multiplier's top bit saying that the value falls as the difficulty rises, as a delay
///     does. An easy setting is quietly raised to the recommended one for a player who is doing well,
///     which stops an easy setting carrying a strong player through the late waves (<c>$2B8C</c> to
///     <c>$2B9E</c>).
/// </remarks>
public static class DifficultyTuning
{
    /// <summary>How many lives a player on an early wave must have to be thought to be doing well.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, <c>CMPA #3</c>.</item>
    ///         <item>Disassembly: <c>$2B9A</c>, <c>CMPA #$03</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ComfortableLives = 3;

    /// <summary>The wave from which a player with plenty of lives is thought to be doing well.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, <c>CMPA #5</c>.</item>
    ///         <item>Disassembly: <c>$2B94</c>, <c>CMPA #$05</c>.</item>
    ///     </list>
    /// </remarks>
    private const int EarlyWave = 5;

    /// <summary>The part of a record's first byte that says how big a step to take.</summary>
    private const int MultiplierMask = 0x1F;

    /// <summary>The top bit of a record's first byte. When it is set, the value falls as the difficulty rises.</summary>
    private const int ReversedFlag = 0x80;

    /// <summary>The wave from which a player is thought to be doing well, so an easy setting is not allowed.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, <c>CMPA #14</c>.</item>
    ///         <item>Disassembly: <c>$2B90</c>, <c>CMPA #$0E</c>.</item>
    ///     </list>
    /// </remarks>
    private const int WellPlayedWave = 14;

    /// <summary>The settings for how long a brain waits after each beat. A smaller number is a faster brain.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>BRNSPD</c>.</item>
    ///         <item>Disassembly: the record at <c>$2D4D</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader BrainBeatWaitHeader = new(0x8E, 6, 10);

    /// <summary>The settings for how long a brain waits between cruise missiles.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>BSHTIM</c>.</item>
    ///         <item>Disassembly: the record at <c>$2D22</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader BrainFireDelayHeader = new(0x8E, 25, 80);

    /// <summary>The settings for how long an enforcer waits between sparks.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>ENSTIM</c>.</item>
    ///         <item>Disassembly: the record at <c>$2CA1</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader EnforcerFireDelayHeader = new(0x8E, 13, 40);

    /// <summary>The settings for the longest a grunt waits between moves, in beats. A smaller number is a faster grunt.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>ROBSPD</c>.</item>
    ///         <item>Disassembly: the record at <c>$2C20</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader GruntMoveDelayHeader = new(0x8E, 10, 20);

    /// <summary>The settings for the fewest beats that the grunts' speed-ups may bring a grunt's longest wait down to.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RMXSPD</c>.</item>
    ///         <item>Disassembly: the record at <c>$2C4B</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader GruntSpeedFloorHeader = new(0x8E, 3, 10);

    /// <summary>The settings for how long a hulk waits between beats. A smaller number is a faster hulk.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>HLKSPD</c>.</item>
    ///         <item>Disassembly: the record at <c>$2CF7</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader HulkBeatIntervalHeader = new(0x8E, 5, 9);

    /// <summary>The settings for how much a spheroid or quark may drop. A bigger number is more.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>ENFNUM</c>.</item>
    ///         <item>Disassembly: the record at <c>$2C76</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader MaxDropsX2Header = new(0x0E, 8, 12);

    /// <summary>The settings for how long a quark waits before dropping a tank.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>TDPTIM</c>.</item>
    ///         <item>Disassembly: the record at <c>$2DCE</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader QuarkDropDelayHeader = new(0x8E, 12, 48);

    /// <summary>The settings for how fast a quark may drift. A bigger number is faster.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>SQSPD</c>.</item>
    ///         <item>Disassembly: the record at <c>$2DF9</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader QuarkMaxSpeedHeader = new(0x0E, 40, 68);

    /// <summary>The settings for how fast a tank shell flies and how well it is aimed. A bigger number is harder.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>SHLSPD</c>.</item>
    ///         <item>Disassembly: the record at <c>$2DA3</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader ShellSpeedHeader = new(0x0E, 160, 255);

    /// <summary>The settings for how long a spheroid waits before dropping an enforcer.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>CDPTIM</c>.</item>
    ///         <item>Disassembly: the record at <c>$2CCC</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader SpheroidDropDelayHeader = new(0x8E, 12, 40);

    /// <summary>The settings for how long a tank waits between shells.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>TNKSHT</c>.</item>
    ///         <item>Disassembly: the record at <c>$2D78</c>.</item>
    ///     </list>
    /// </remarks>
    private static readonly DifficultyTableHeader TankFireDelayHeader = new(0x8E, 20, 40);

    /// <summary>
    ///     Works out what a wave is like at the chosen difficulty. At the recommended setting the wave is returned as it
    ///     was.
    /// </summary>
    /// <param name="waveParameters">
    ///     The wave as the table gives it, for the recommended setting. Apply <see cref="BozoMode" />
    ///     first, as the arcade does.
    /// </param>
    /// <param name="difficulty">The difficulty setting, from the lowest to the highest the game allows.</param>
    /// <param name="lives">The lives the player has, counting the one in play.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>GETWV</c>, with the difficulty in
    ///             <c>difficulty_of_play</c> (<c>$CC14</c>) and the lives in <c>PLAS</c>.
    ///         </item>
    ///         <item>Disassembly: <c>$2B7C</c>.</item>
    ///     </list>
    /// </remarks>
    public static WaveParameters Apply(WaveParameters waveParameters, int difficulty, int lives)
    {
        var effectiveDifficulty = GetEffectiveDifficulty(waveParameters.WaveNumber, difficulty, lives);
        if (effectiveDifficulty == GameSettings.RecommendedDifficulty) return waveParameters;

        var delta = effectiveDifficulty - GameSettings.RecommendedDifficulty;
        var magnitude = Math.Abs(delta);

        // The two fields worked out from the maxDropsX2 value must follow it, as WaveParameters.CreateFromWave works them out.
        var maxDropsX2 = Adjust(waveParameters.MaxDropsX2, MaxDropsX2Header, delta, magnitude);

        return waveParameters with
        {
            GruntMoveDelay = Adjust(waveParameters.GruntMoveDelay, GruntMoveDelayHeader, delta, magnitude),
            GruntSpeedFloor = Adjust(waveParameters.GruntSpeedFloor, GruntSpeedFloorHeader, delta, magnitude),
            MaxDropsX2 = maxDropsX2,
            MaxEnforcersPerSpheroid = (maxDropsX2 + 1) / 2,
            MaxTanksPerQuark = (maxDropsX2 + 1) / 2,
            EnforcerFireDelay = Adjust(waveParameters.EnforcerFireDelay, EnforcerFireDelayHeader, delta, magnitude),
            SpheroidDropDelay = Adjust(waveParameters.SpheroidDropDelay, SpheroidDropDelayHeader, delta, magnitude),
            HulkBeatIntervalArcadeFrames =
            Adjust(waveParameters.HulkBeatIntervalArcadeFrames, HulkBeatIntervalHeader, delta, magnitude),
            BrainFireDelay = Adjust(waveParameters.BrainFireDelay, BrainFireDelayHeader, delta, magnitude),
            BrainBeatWaitArcadeFrames = Adjust(waveParameters.BrainBeatWaitArcadeFrames, BrainBeatWaitHeader, delta, magnitude),
            TankFireDelay = Adjust(waveParameters.TankFireDelay, TankFireDelayHeader, delta, magnitude),
            ShellSpeed = Adjust(waveParameters.ShellSpeed, ShellSpeedHeader, delta, magnitude),
            QuarkDropDelay = Adjust(waveParameters.QuarkDropDelay, QuarkDropDelayHeader, delta, magnitude),
            QuarkMaxSpeed = Adjust(waveParameters.QuarkMaxSpeed, QuarkMaxSpeedHeader, delta, magnitude)
        };
    }

    /// <summary>Moves one value for the difficulty, and keeps it between its minimum and maximum.</summary>
    /// <param name="value">The value the table gives.</param>
    /// <param name="header">The record for this value.</param>
    /// <param name="delta">How far the difficulty is from the recommended one, below or above it.</param>
    /// <param name="magnitude">How far the difficulty is from the recommended one, without the sign.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>.</item>
    ///         <item>Disassembly: <c>$2BC4</c>.</item>
    ///     </list>
    /// </remarks>
    private static int Adjust(int value, DifficultyTableHeader header, int delta, int magnitude)
    {
        var fraction = (magnitude * (header.Multiplier & MultiplierMask)) & 0xFF;
        var product = value * fraction;

        // The ROM takes the high byte of the product and rounds it with ADCA #0, which carries
        // bit 7 of the product's low byte.
        var step = (product >> 8) + ((product >> 7) & 1);

        var isSubtracted = (((delta & 0xFF) ^ header.Multiplier) & ReversedFlag) != 0;
        var adjusted = value + step;
        if (isSubtracted) adjusted = value - step;

        return Math.Clamp(adjusted, header.Min, header.Max);
    }

    /// <summary>
    ///     Works out the difficulty a wave really uses. It is the setting, except that an easy setting becomes the
    ///     recommended one for a player who is doing well.
    /// </summary>
    /// <param name="wave">The wave number.</param>
    /// <param name="difficulty">The difficulty setting.</param>
    /// <param name="lives">The lives the player has, counting the one in play.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>.</item>
    ///         <item>Disassembly: <c>$2B8C</c> to <c>$2B9E</c>.</item>
    ///     </list>
    /// </remarks>
    private static int GetEffectiveDifficulty(int wave, int difficulty, int lives)
    {
        var effective = Math.Clamp(difficulty, GameSettings.MinimumDifficulty, GameSettings.MaxDifficulty);
        if (effective >= GameSettings.RecommendedDifficulty) return effective;

        if (wave >= WellPlayedWave || (wave >= EarlyWave && lives >= ComfortableLives))
            return GameSettings.RecommendedDifficulty;

        return effective;
    }
}
