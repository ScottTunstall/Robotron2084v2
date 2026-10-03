using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>
/// The arcade's DIFFICULTY OF PLAY adjustment (notes §131) — the ROM routine at <c>$2B7C</c>
/// (<c>INITIALISE_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE</c>). It reads the difficulty
/// byte, nudges each of the twelve wave-tuning values away from their "recommended" value, and clamps
/// each to the range its own <c>$2C20</c> record carries.
///
/// The arithmetic is the ROM's, byte for byte:
/// <list type="bullet">
/// <item>at the recommended difficulty (5) nothing moves — the stored value IS the recommended one;</item>
/// <item>otherwise the value is nudged by <c>round(value × (|difficulty − 5| × multiplier5bits) / 256)</c>
/// — the ROM multiplies twice and rounds with <c>ADCA #0</c>;</item>
/// <item>the nudge is ADDED or SUBTRACTED according to <c>sign(difficulty − 5) XOR bit 7 of the
/// multiplier byte</c>, so bit 7 flags the values that fall as difficulty rises (a move delay, say);</item>
/// <item>the result is clamped to the record's minimum and maximum.</item>
/// </list>
///
/// <para>
/// The ROM's "easy set up" mercy also applies first (<c>$2B8C-$2B9E</c>): an easy setting is quietly
/// raised to the recommended one for a player who is already doing well — at wave 14 or beyond, or
/// from wave 5 with three or more men left — so a liberal setting cannot carry a strong player
/// through the late waves.
/// </para>
/// </summary>
public static class DifficultyTuning
{
    /// <summary>Bit 7 of a record's first byte: this value falls as difficulty rises.</summary>
    private const int ReversedFlag = 0x80;

    /// <summary>Bits 0-4 of a record's first byte: the multiplier the nudge is scaled by.</summary>
    private const int MultiplierMask = 0x1F;

    /// <summary>ROM <c>$2B90</c> <c>CMPA #$0E</c> — a player at this wave or beyond gets no mercy.</summary>
    private const int WellPlayedWave = 14;

    /// <summary>ROM <c>$2B94</c> <c>CMPA #$05</c> — the second mercy test starts here.</summary>
    private const int EarlyWave = 5;

    /// <summary>ROM <c>$2B9A</c> <c>CMPA #$03</c> — three or more men left means the player is fine.</summary>
    private const int ComfortableLives = 3;

    /// <summary>ROBSPD @ $2C20 — grunt move delay (lower = faster).</summary>
    private static readonly Header GruntMoveDelayHeader = new(0x8E, 10, 20);

    /// <summary>RMXSPD @ $2C4B — the floor the per-kill grunt speed-up clamps to.</summary>
    private static readonly Header GruntSpeedFloorHeader = new(0x8E, 3, 10);

    /// <summary>ENFNUM @ $2C76 — drops per spheroid/quark (higher = more).</summary>
    private static readonly Header MaxDropsX2Header = new(0x0E, 8, 12);

    /// <summary>ENSTIM @ $2CA1 — enforcer spark fire delay.</summary>
    private static readonly Header EnforcerFireDelayHeader = new(0x8E, 13, 40);

    /// <summary>CDPTIM @ $2CCC — spheroid enforcer-drop delay.</summary>
    private static readonly Header SpheroidDropDelayHeader = new(0x8E, 12, 40);

    /// <summary>HLKSPD @ $2CF7 — hulk update rate (lower = faster).</summary>
    private static readonly Header HulkBeatIntervalHeader = new(0x8E, 5, 9);

    /// <summary>BSHTIM @ $2D22 — brain cruise-missile fire delay.</summary>
    private static readonly Header BrainFireDelayHeader = new(0x8E, 25, 80);

    /// <summary>BRNSPD @ $2D4D — brain speed.</summary>
    private static readonly Header BrainBeatWaitHeader = new(0x8E, 6, 10);

    /// <summary>TNKSHT @ $2D78 — tank shell fire rate.</summary>
    private static readonly Header TankFireDelayHeader = new(0x8E, 20, 40);

    /// <summary>SHLSPD @ $2DA3 — shell speed/accuracy (higher = harder).</summary>
    private static readonly Header ShellSpeedHeader = new(0x0E, 160, 255);

    /// <summary>TDPTIM @ $2DCE — quark tank-drop delay.</summary>
    private static readonly Header QuarkDropDelayHeader = new(0x8E, 12, 48);

    /// <summary>SQSPD @ $2DF9 — quark movement (higher = faster).</summary>
    private static readonly Header QuarkSpeedCapHeader = new(0x0E, 40, 68);

    /// <summary>
    /// The wave's parameters at the chosen difficulty, with the ROM's mercy applied. Returns
    /// <paramref name="parameters"/> unchanged at the recommended difficulty.
    /// </summary>
    /// <param name="parameters">The wave's stored (recommended-difficulty) parameters. Apply
    /// <see cref="BozoMode"/> first: the ROM's Bozo mercy (<c>$2B26</c>) runs before the difficulty
    /// routine.</param>
    /// <param name="difficulty">DIFFICULTY OF PLAY, 0-10 (ROM <c>difficulty_of_play</c> $CC14).</param>
    /// <param name="lives">The player's men INCLUDING the life in play (ROM <c>PLAS</c> $0008,X).</param>
    public static LevelParameters Apply(LevelParameters parameters, int difficulty, int lives)
    {
        int effective = GetEffectiveDifficulty(parameters.LevelNumber, difficulty, lives);
        if (effective == GameSettings.RecommendedDifficulty)
        {
            return parameters;
        }

        int delta = effective - GameSettings.RecommendedDifficulty;
        int magnitude = Math.Abs(delta);

        // The drops value's two derived fields (ceil(ENFNUM/2)) must follow it, exactly as
        // LevelParameters.CreateFromWave derives them from the unadjusted table.
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

    /// <summary>
    /// The difficulty actually used for a wave — the setting itself, unless the ROM's mercy quietly
    /// replaces an easier-than-recommended setting with the recommended one (<c>$2B8C-$2B9E</c>).
    /// </summary>
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

    /// <summary>One <c>$2C20</c> record's header: the multiplier byte, then the min and max clamps.</summary>
    private readonly record struct Header(int Multiplier, int Min, int Max);

    /// <summary>
    /// One value nudged and clamped as the ROM does at <c>$2BC4</c>. <paramref name="delta"/> is the
    /// signed setting (difficulty − 5) and <paramref name="magnitude"/> its absolute value.
    /// </summary>
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
