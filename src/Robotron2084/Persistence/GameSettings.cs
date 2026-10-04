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
/// (the demo machine playing itself) keeps its sound or is silenced;</item>
/// <item><b>TANK SHELL BUG</b>, <b>BRAINS CHASE MIKEY BUG</b> and <b>BOZO MODE</b> — PORT-ONLY switches for
/// three things the arcade always does. All three are on from the factory, which is the arcade as it shipped.</item>
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

    /// <summary>DIFFICULTY OF PLAY's floor — "EXTRA LIBERAL" (ROM metadata $7079: 00). It is the smallest value <see cref="Difficulty"/> may have.</summary>
    public const int MinimumDifficulty = 0;

    /// <summary>DIFFICULTY OF PLAY's ceiling — "EXTRA CONSERVATIVE" (ROM metadata $7079: 10). It is the biggest value <see cref="Difficulty"/> may have.</summary>
    public const int MaximumDifficulty = 10;

    /// <summary>The recommended difficulty: 5 — no wave-table adjustment at all (the ROM's "RECOMMENDED").</summary>
    public const int RecommendedDifficulty = 5;

    /// <summary>TURNS PER PLAYER's floor (ROM metadata $704C: 01). It is the smallest value <see cref="TurnsPerPlayer"/> may have.</summary>
    public const int MinimumTurnsPerPlayer = 1;

    /// <summary>TURNS PER PLAYER's ceiling (ROM metadata $704C: 20). It is the biggest value <see cref="TurnsPerPlayer"/> may have.</summary>
    public const int MaximumTurnsPerPlayer = 20;

    /// <summary>The factory EXTRA MAN EVERY: 25 (25000 points), the ROM's "RECOMMENDED" stop. It is the starting value of <see cref="ExtraManEvery"/>, and the value it is set back to when the settings are restored.</summary>
    public const int FactoryExtraManEveryThousands = 25;

    /// <summary>The factory EXTRA MAN EVERY in points, as the game compares scores.</summary>
    public const int FactoryExtraManEveryPoints = FactoryExtraManEveryThousands * 1000;

    /// <summary>The factory TURNS PER PLAYER: 3, the ROM's "RECOMMENDED" (and the arcade's own default). It is the starting value of <see cref="TurnsPerPlayer"/>, and the value it is set back to when the settings are restored.</summary>
    public const int FactoryTurnsPerPlayer = 3;

    /// <summary>Factory DIFFICULTY OF PLAY: 5, the ROM's "RECOMMENDED". It is the starting value of <see cref="Difficulty"/>, and the value it is set back to when the settings are restored.</summary>
    public const int FactoryDifficulty = RecommendedDifficulty;

    /// <summary>
    /// Factory ATTRACT MODE SOUND: off. The arcade's attract demo is a real game and makes its real
    /// noises, but the port's demo plays on a machine someone is usually sitting at, so it is silent
    /// until the operator turns it on (notes §140). The setting is on the GAME ADJUSTMENT page.
    ///  It is the starting value of <see cref="AttractModeSound"/>, and the value it is set back to when the settings are restored.</summary>
    public const bool FactoryAttractModeSound = false;

    /// <summary>Factory TANK SHELL BUG: on, as the arcade is. It is the starting value of <see cref="TankShellBug"/>, and the value it is set back to when the settings are restored.</summary>
    public const bool FactoryTankShellBug = true;

    /// <summary>Factory BRAINS CHASE MIKEY BUG: on, as the arcade is. It is the starting value of <see cref="BrainsChaseMikeyBug"/>, and the value it is set back to when the settings are restored.</summary>
    public const bool FactoryBrainsChaseMikeyBug = true;

    /// <summary>Factory BOZO MODE: on, as the arcade's second release is. It is the starting value of <see cref="BozoModeEnabled"/>, and the value it is set back to when the settings are restored.</summary>
    public const bool FactoryBozoModeEnabled = true;

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

    /// <summary>
    /// Whether the arcade's tank shell bug is kept (port-only switch). On, a shell that fizzles out is never taken
    /// off the wave's shell count, so the tanks stop firing once the count is used up. Off, only the shells on
    /// the field count.
    /// </summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SHELL</c>, which has no <c>DEC SHLCNT</c>.</remarks>
    public bool TankShellBug { get; set; } = FactoryTankShellBug;

    /// <summary>
    /// Whether the arcade's "all the brains chase Mikey" bug is kept (port-only switch). On, every brain starts the
    /// wave chasing the first Mikey. Off, each brain starts by chasing the family member nearest to it.
    /// </summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSTV</c>, which calls <c>GETHTG</c> before the family is made (notes §18.8).</remarks>
    public bool BrainsChaseMikeyBug { get; set; } = FactoryBrainsChaseMikeyBug;

    /// <summary>Whether the arcade's mercy for a player losing ships early is given (port-only switch; see <see cref="Level.BozoMode"/>).</summary>
    public bool BozoModeEnabled { get; set; } = FactoryBozoModeEnabled;

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
        TankShellBug = FactoryTankShellBug;
        BrainsChaseMikeyBug = FactoryBrainsChaseMikeyBug;
        BozoModeEnabled = FactoryBozoModeEnabled;
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

    /// <summary>Turns TANK SHELL BUG on (right) or off (left).</summary>
    public void BumpTankShellBug(int direction) => TankShellBug = direction > 0;

    /// <summary>Turns BRAINS CHASE MIKEY BUG on (right) or off (left).</summary>
    public void BumpBrainsChaseMikeyBug(int direction) => BrainsChaseMikeyBug = direction > 0;

    /// <summary>Turns BOZO MODE on (right) or off (left).</summary>
    public void BumpBozoModeEnabled(int direction) => BozoModeEnabled = direction > 0;
}
