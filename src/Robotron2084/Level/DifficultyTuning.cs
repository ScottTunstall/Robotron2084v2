using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>Makes a wave easier or harder to match the difficulty setting, by moving the speeds and delays the wave table gives.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the difficulty setting and adjusts the wave</item>
/// <item>Disassembly: <c>INITIALISE_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c> (<c>$2B7C</c>)</item>
/// </list>
/// Each of the twelve values is moved away from the recommended one, by an amount that grows with how far the setting is from the recommended one, and is
/// then kept between the minimum and maximum its record in the table at <c>$2C20</c> allows (notes §131). At the recommended setting nothing moves.
/// The amount is worked out as the arcade works it out, byte for byte: the value times the distance from the recommended setting times the record's
/// multiplier, with the multiplier's top bit saying that the value falls as the difficulty rises, as a delay does.
/// An easy setting is quietly raised to the recommended one for a player who is doing well, which stops an easy setting carrying a strong player through the late waves
/// (<c>$2B8C</c> to <c>$2B9E</c>).
/// </remarks>
public static class DifficultyTuning
{
    /// <summary>The top bit of a record's first byte. When it is set, the value falls as the difficulty rises.</summary>
    private const int ReversedFlag = 0x80;

    /// <summary>The part of a record's first byte that says how big a step to take.</summary>
    private const int MultiplierMask = 0x1F;

    /// <summary>The wave from which a player is thought to be doing well, so an easy setting is not allowed.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, <c>CMPA #14</c>. Disassembly: <c>$2B90</c>, <c>CMPA #$0E</c>.</remarks>
    private const int WellPlayedWave = 14;

    /// <summary>The wave from which a player with plenty of men is thought to be doing well.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, <c>CMPA #5</c>. Disassembly: <c>$2B94</c>, <c>CMPA #$05</c>.</remarks>
    private const int EarlyWave = 5;

    /// <summary>How many men a player on an early wave must have to be thought to be doing well.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, <c>CMPA #3</c>. Disassembly: <c>$2B9A</c>, <c>CMPA #$03</c>.</remarks>
    private const int ComfortableLives = 3;

    /// <summary>The settings for the longest a grunt waits between moves, in beats. A smaller number is a faster grunt.</summary>
    /// <remarks>Original source: <c>ROBSPD</c>. Disassembly: the record at <c>$2C20</c>.</remarks>
    private static readonly Header GruntMoveDelayHeader = new(0x8E, 10, 20);

    /// <summary>The settings for the fewest beats that the grunts' speed-ups may bring a grunt's longest wait down to.</summary>
    /// <remarks>Original source: <c>RMXSPD</c>. Disassembly: the record at <c>$2C4B</c>.</remarks>
    private static readonly Header GruntSpeedFloorHeader = new(0x8E, 3, 10);

    /// <summary>The settings for how much a spheroid or quark may drop. A bigger number is more.</summary>
    /// <remarks>Original source: <c>ENFNUM</c>. Disassembly: the record at <c>$2C76</c>.</remarks>
    private static readonly Header MaxDropsX2Header = new(0x0E, 8, 12);

    /// <summary>The settings for how long an enforcer waits between sparks.</summary>
    /// <remarks>Original source: <c>ENSTIM</c>. Disassembly: the record at <c>$2CA1</c>.</remarks>
    private static readonly Header EnforcerFireDelayHeader = new(0x8E, 13, 40);

    /// <summary>The settings for how long a spheroid waits before dropping an enforcer.</summary>
    /// <remarks>Original source: <c>CDPTIM</c>. Disassembly: the record at <c>$2CCC</c>.</remarks>
    private static readonly Header SpheroidDropDelayHeader = new(0x8E, 12, 40);

    /// <summary>The settings for how long a hulk waits between beats. A smaller number is a faster hulk.</summary>
    /// <remarks>Original source: <c>HLKSPD</c>. Disassembly: the record at <c>$2CF7</c>.</remarks>
    private static readonly Header HulkBeatIntervalHeader = new(0x8E, 5, 9);

    /// <summary>The settings for how long a brain waits between cruise missiles.</summary>
    /// <remarks>Original source: <c>BSHTIM</c>. Disassembly: the record at <c>$2D22</c>.</remarks>
    private static readonly Header BrainFireDelayHeader = new(0x8E, 25, 80);

    /// <summary>The settings for how long a brain waits after each beat. A smaller number is a faster brain.</summary>
    /// <remarks>Original source: <c>BRNSPD</c>. Disassembly: the record at <c>$2D4D</c>.</remarks>
    private static readonly Header BrainBeatWaitHeader = new(0x8E, 6, 10);

    /// <summary>The settings for how long a tank waits between shells.</summary>
    /// <remarks>Original source: <c>TNKSHT</c>. Disassembly: the record at <c>$2D78</c>.</remarks>
    private static readonly Header TankFireDelayHeader = new(0x8E, 20, 40);

    /// <summary>The settings for how fast a tank shell flies and how well it is aimed. A bigger number is harder.</summary>
    /// <remarks>Original source: <c>SHLSPD</c>. Disassembly: the record at <c>$2DA3</c>.</remarks>
    private static readonly Header ShellSpeedHeader = new(0x0E, 160, 255);

    /// <summary>The settings for how long a quark waits before dropping a tank.</summary>
    /// <remarks>Original source: <c>TDPTIM</c>. Disassembly: the record at <c>$2DCE</c>.</remarks>
    private static readonly Header QuarkDropDelayHeader = new(0x8E, 12, 48);

    /// <summary>The settings for how fast a quark may drift. A bigger number is faster.</summary>
    /// <remarks>Original source: <c>SQSPD</c>. Disassembly: the record at <c>$2DF9</c>.</remarks>
    private static readonly Header QuarkSpeedCapHeader = new(0x0E, 40, 68);

    /// <summary>Works out what a wave is like at the chosen difficulty. At the recommended setting the wave is returned as it was.</summary>
    /// <param name="parameters">The wave as the table gives it, for the recommended setting. Apply <see cref="BozoMode"/> first, as the arcade does.</param>
    /// <param name="difficulty">The difficulty setting, from the lowest to the highest the game allows.</param>
    /// <param name="lives">The men the player has, counting the one in play.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, with the difficulty in <c>difficulty_of_play</c> (<c>$CC14</c>) and the men in <c>PLAS</c>. Disassembly: <c>$2B7C</c>.</remarks>
    public static LevelParameters Apply(LevelParameters parameters, int difficulty, int lives)
    {
        int effective = GetEffectiveDifficulty(parameters.LevelNumber, difficulty, lives);
        if (effective == GameSettings.RecommendedDifficulty)
        {
            return parameters;
        }

        int delta = effective - GameSettings.RecommendedDifficulty;
        int magnitude = Math.Abs(delta);

        // The two fields worked out from the drops value must follow it, as LevelParameters.CreateFromWave works them out.
        int drops = Adjust(parameters.MaxDropsX2, MaxDropsX2Header, delta, magnitude);

        return parameters with
        {
            GruntMoveDelay = Adjust(parameters.GruntMoveDelay, GruntMoveDelayHeader, delta, magnitude),
            GruntSpeedFloor = Adjust(parameters.GruntSpeedFloor, GruntSpeedFloorHeader, delta, magnitude),
            MaxDropsX2 = drops,
            MaxEnforcersPerSpheroid = (drops + 1) / 2,
            MaxTanksPerQuark = (drops + 1) / 2,
            EnforcerFireDelay = Adjust(parameters.EnforcerFireDelay, EnforcerFireDelayHeader, delta, magnitude),
            SpheroidDropDelay = Adjust(parameters.SpheroidDropDelay, SpheroidDropDelayHeader, delta, magnitude),
            HulkBeatIntervalRomFrames = Adjust(parameters.HulkBeatIntervalRomFrames, HulkBeatIntervalHeader, delta, magnitude),
            BrainFireDelay = Adjust(parameters.BrainFireDelay, BrainFireDelayHeader, delta, magnitude),
            BrainBeatWaitRomFrames = Adjust(parameters.BrainBeatWaitRomFrames, BrainBeatWaitHeader, delta, magnitude),
            TankFireDelay = Adjust(parameters.TankFireDelay, TankFireDelayHeader, delta, magnitude),
            ShellSpeed = Adjust(parameters.ShellSpeed, ShellSpeedHeader, delta, magnitude),
            QuarkDropDelay = Adjust(parameters.QuarkDropDelay, QuarkDropDelayHeader, delta, magnitude),
            QuarkSpeedCap = Adjust(parameters.QuarkSpeedCap, QuarkSpeedCapHeader, delta, magnitude),
        };
    }

    /// <summary>Works out the difficulty a wave really uses. It is the setting, except that an easy setting becomes the recommended one for a player who is doing well.</summary>
    /// <param name="wave">The wave number.</param>
    /// <param name="difficulty">The difficulty setting.</param>
    /// <param name="lives">The men the player has, counting the one in play.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GETWV</c>. Disassembly: <c>$2B8C</c> to <c>$2B9E</c>.</remarks>
    private static int GetEffectiveDifficulty(int wave, int difficulty, int lives)
    {
        int effective = Math.Clamp(difficulty, GameSettings.MinimumDifficulty, GameSettings.MaximumDifficulty);
        if (effective >= GameSettings.RecommendedDifficulty)
        {
            return effective;
        }

        if (wave >= WellPlayedWave || (wave >= EarlyWave && lives >= ComfortableLives))
        {
            return GameSettings.RecommendedDifficulty;
        }

        return effective;
    }

    /// <summary>The start of one record in the table of settings.</summary>
    /// <remarks>Disassembly: the records at <c>$2C20</c>.</remarks>
    /// <param name="Multiplier">The byte that says how big a step to take, and whether the value falls as the difficulty rises.</param>
    /// <param name="Min">The smallest value the setting may have.</param>
    /// <param name="Max">The biggest value the setting may have.</param>
    private readonly record struct Header(int Multiplier, int Min, int Max);

    /// <summary>Moves one value for the difficulty, and keeps it between its minimum and maximum.</summary>
    /// <param name="value">The value the table gives.</param>
    /// <param name="header">The record for this value.</param>
    /// <param name="delta">How far the difficulty is from the recommended one, below or above it.</param>
    /// <param name="magnitude">How far the difficulty is from the recommended one, without the sign.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>GETWV</c>. Disassembly: <c>$2BC4</c>.</remarks>
    private static int Adjust(int value, Header header, int delta, int magnitude)
    {
        int fraction = (magnitude * (header.Multiplier & MultiplierMask)) & 0xFF;
        int product = value * fraction;

        // The ROM takes the high byte of the product and rounds it with ADCA #0, which carries
        // bit 7 of the product's low byte.
        int step = (product >> 8) + ((product >> 7) & 1);

        bool subtract = (((delta & 0xFF) ^ header.Multiplier) & ReversedFlag) != 0;
        int adjusted = subtract ? value - step : value + step;
        return Math.Clamp(adjusted, header.Min, header.Max);
    }
}
