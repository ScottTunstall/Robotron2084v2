namespace Robotron2084.Persistence;

/// <summary>
/// The arcade's GAME ADJUSTMENT settings the port honours (notes §131) — the rows of the cabinet's
/// service-mode page that change how a game PLAYS.
///
/// The nine coin/pricing rows are deliberately absent (the port has no coin handling), as are the
/// rows that change only the attract presentation (FANCY ATTRACT MODE, AUTO CYCLE, the operator
/// message and the highest-score name). The rows that remain are:
/// <list type="bullet">
/// <item><b>EXTRA MAN EVERY</b> (ROM <c>extra_man_every</c> $CC00) — the score that earns a spare man,
/// one of the five stops the ROM's own list allows (<c>$71DB</c>: 0, 20, 25, 30, 50 thousand);</item>
/// <item><b>TURNS PER PLAYER</b> (ROM <c>turns_per_player</c> $CC02) — the men a player starts with
/// (ROM <c>NSHIP</c>, loaded into <c>PLAS</c> by <c>START1</c>);</item>
/// <item><b>DIFFICULTY OF PLAY</b> (ROM <c>difficulty_of_play</c> $CC14) — 0-10, 5 "RECOMMENDED",
/// applied to the wave tables by <see cref="Level.DifficultyTuning"/>;</item>
/// <item><b>ATTRACT MODE SOUND</b> — PORT-ONLY, with no arcade counterpart: the attract sequence
/// (the demo machine playing itself) keeps its sound or is silenced.</item>
/// </list>
///
/// The ranges and the factory values are the ROM's (<c>GAME_ADJUSTMENT_SETTING_METADATA</c>, $6FD5;
/// the recommended stops are named in the option lists at $7041/$7079). The ROM packs each value into
/// two CMOS bytes as BCD digits; the port keeps plain integers and writes them beside the controls and
/// the high scores (<see cref="GameSettingsStore"/>).
/// </summary>
public sealed class GameSettings
{
    /// <summary>The EXTRA MAN EVERY stops the ROM allows (its <c>$71DB</c> list), in thousands of points.</summary>
    public static readonly int[] ExtraManEveryValues = [0, 20, 25, 30, 50];

    /// <summary>DIFFICULTY OF PLAY's floor — "EXTRA LIBERAL" (ROM metadata $7079: 00).</summary>
    public const int MinimumDifficulty = 0;

    /// <summary>DIFFICULTY OF PLAY's ceiling — "EXTRA CONSERVATIVE" (ROM metadata $7079: 10).</summary>
    public const int MaximumDifficulty = 10;

    /// <summary>The recommended difficulty: 5 — no wave-table adjustment at all (the ROM's "RECOMMENDED").</summary>
    public const int RecommendedDifficulty = 5;

    /// <summary>TURNS PER PLAYER's floor (ROM metadata $704C: 01).</summary>
    public const int MinimumTurnsPerPlayer = 1;

    /// <summary>TURNS PER PLAYER's ceiling (ROM metadata $704C: 20).</summary>
    public const int MaximumTurnsPerPlayer = 20;

    /// <summary>The factory EXTRA MAN EVERY: 25 (25000 points), the ROM's "RECOMMENDED" stop.</summary>
    public const int FactoryExtraManEveryThousands = 25;

    /// <summary>The factory EXTRA MAN EVERY in points, as the game compares scores.</summary>
    public const int FactoryExtraManEveryPoints = FactoryExtraManEveryThousands * 1000;

    /// <summary>The factory TURNS PER PLAYER: 3, the ROM's "RECOMMENDED" (and the arcade's own default).</summary>
    public const int FactoryTurnsPerPlayer = 3;

    /// <summary>Factory DIFFICULTY OF PLAY: 5, the ROM's "RECOMMENDED".</summary>
    public const int FactoryDifficulty = RecommendedDifficulty;

    /// <summary>
    /// Factory ATTRACT MODE SOUND: off. The arcade's attract demo is a real game and makes its real
    /// noises, but the port's demo plays on a machine someone is usually sitting at, so it is silent
    /// until the operator turns it on (notes §140). The setting is on the GAME ADJUSTMENT page.
    /// </summary>
    public const bool FactoryAttractModeSound = false;

    /// <summary>
    /// The score that earns a spare man, in thousands — 0 turns extra men off. One of
    /// <see cref="ExtraManEveryValues"/>; 25 by default, the arcade's factory "RECOMMENDED".
    /// </summary>
    public int ExtraManEvery { get; set; } = FactoryExtraManEveryThousands;

    /// <summary>How many men a player starts with (ROM <c>NSHIP</c>/<c>PLAS</c>), 1-20.</summary>
    public int TurnsPerPlayer { get; set; } = FactoryTurnsPerPlayer;

    /// <summary>DIFFICULTY OF PLAY, 0-10 (5 is the ROM's "RECOMMENDED").</summary>
    public int Difficulty { get; set; } = FactoryDifficulty;

    /// <summary>
    /// Whether the attract sequence plays its sounds (port-only; the arcade has no such row). Off
    /// silences the demo machine playing itself, while a real game still has sound.
    /// </summary>
    public bool AttractModeSound { get; set; } = FactoryAttractModeSound;

    /// <summary>The EXTRA MAN EVERY score in points, as the game compares scores (<c>0</c> = never).</summary>
    public int ExtraManEveryPoints => ExtraManEvery * 1000;

    /// <summary>
    /// Whether the attract sequence should be silent (notes §131): true while an attract screen is
    /// showing and ATTRACT MODE SOUND is off. The shell hands this to <c>Sound.AttractMuted</c>.
    /// </summary>
    /// <param name="attractShowing">True while an attract screen is on screen.</param>
    public bool AttractIsSilent(bool attractShowing) => attractShowing && !AttractModeSound;

    /// <summary>A fresh cabinet's settings.</summary>
    public static GameSettings CreateFactoryDefaults() => new();

    /// <summary>True for one of the five EXTRA MAN EVERY stops (ROM <c>$71DB</c>).</summary>
    public static bool IsExtraManEveryValue(int value) => Array.IndexOf(ExtraManEveryValues, value) >= 0;

    /// <summary>Restores every setting to its factory value.</summary>
    public void ResetToFactory()
    {
        ExtraManEvery = FactoryExtraManEveryThousands;
        TurnsPerPlayer = FactoryTurnsPerPlayer;
        Difficulty = FactoryDifficulty;
        AttractModeSound = FactoryAttractModeSound;
    }

    /// <summary>
    /// Moves EXTRA MAN EVERY up (<paramref name="direction"/> 1) or down (-1) one stop of the ROM's
    /// five-stop progression (0 → 20000 → 25000 → 30000 → 50000), stopping at the ends.
    /// </summary>
    public void BumpExtraManEvery(int direction)
    {
        int index = Array.IndexOf(ExtraManEveryValues, ExtraManEvery);
        if (index < 0)
        {
            index = Array.IndexOf(ExtraManEveryValues, FactoryExtraManEveryThousands);
        }

        ExtraManEvery = ExtraManEveryValues[Math.Clamp(index + direction, 0, ExtraManEveryValues.Length - 1)];
    }

    /// <summary>Moves TURNS PER PLAYER one step, clamped to the ROM's 1-20 range.</summary>
    public void BumpTurnsPerPlayer(int direction) =>
        TurnsPerPlayer = Math.Clamp(TurnsPerPlayer + direction, MinimumTurnsPerPlayer, MaximumTurnsPerPlayer);

    /// <summary>Moves DIFFICULTY OF PLAY one step, clamped to the ROM's 0-10 range.</summary>
    public void BumpDifficulty(int direction) =>
        Difficulty = Math.Clamp(Difficulty + direction, MinimumDifficulty, MaximumDifficulty);

    /// <summary>Turns ATTRACT MODE SOUND on (right) or off (left).</summary>
    public void BumpAttractModeSound(int direction) => AttractModeSound = direction > 0;
}
