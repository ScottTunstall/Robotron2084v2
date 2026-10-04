using Robotron2084.Persistence;

namespace Robotron2084.Hud;

/// <summary>What Enter on an action row asked the screen to do.</summary>
public enum SettingsAction
{
    /// <summary>Nothing (no action row highlighted, or it was not set to YES).</summary>
    None,

    /// <summary>RESTORE FACTORY SETTINGS — put all three settings back to the cabinet's defaults.</summary>
    RestoreFactorySettings,

    /// <summary>HIGH SCORE TABLE RESET — throw the saved high score table away.</summary>
    ResetHighScores,
}

/// <summary>
/// The GAME ADJUSTMENT page's logic, with no MonoGame in it (notes §131) — the same split the
/// DEFINITIONS page uses (<see cref="DefineInputsModel"/>): the state draws and reads hardware, this
/// decides what a keypress MEANS.
///
/// The page is the arcade's own gameplay rows, in the arcade's order, plus the port's own sound row
/// (its coin/pricing rows are not built):
/// <list type="number">
/// <item>EXTRA MAN EVERY — the score that earns a spare man, stepping the ROM's own five stops
/// (0, 20, 25, 30, 50 thousand);</item>
/// <item>TURNS PER PLAYER — the men a player starts with, 1-20;</item>
/// <item>DIFFICULTY OF PLAY — 0-10;</item>
/// <item>ATTRACT MODE SOUND — port-only: whether the attract sequence (the demo machine playing
/// itself) keeps its sound;</item>
/// <item>BOZO MODE — port-only: whether the arcade's mercy for a player losing ships early is given;</item>
/// <item>BRAINS CHASE MIKEY BUG — port-only: whether every brain starts the wave chasing Mikey, as in the arcade;</item>
/// <item>TANK SHELL BUG — port-only: whether a fizzled shell stays on the wave's shell count, as in the arcade;</item>
/// <item>RESTORE FACTORY SETTINGS — an action row;</item>
/// <item>HIGH SCORE TABLE RESET — an action row.</item>
/// </list>
///
/// Up/Down move the cursor round the rows, Left/Right step the highlighted row's value, and the
/// two action rows take the arcade's own two steps: Left/Right set them to NO or YES, then Enter
/// performs them while they read YES (<c>YES ADVANCE TO ACTIVATE</c>).
///
/// The descriptive words under each value are the ROM's own option lists (the metadata at $6FD5 and
/// the strings it points at): "RECOMMENDED" is the stop the arcade's factory settings use.
/// </summary>
public sealed class SettingsModel
{
    /// <summary>Every line on the page. It is the size of <see cref="_armedLines"/>.</summary>
    public const int LineCount = 9;

    /// <summary>EXTRA MAN EVERY — the first row, as on the cabinet.</summary>
    public const int ExtraManLine = 0;

    /// <summary>TURNS PER PLAYER — how many men a player starts with.</summary>
    public const int TurnsLine = 1;

    /// <summary>DIFFICULTY OF PLAY — the wave-table adjustment.</summary>
    public const int DifficultyLine = 2;

    /// <summary>ATTRACT MODE SOUND — port-only: whether the attract sequence makes its noises.</summary>
    public const int AttractSoundLine = 3;

    /// <summary>BOZO MODE — port-only: whether the arcade's mercy for a losing player is given.</summary>
    public const int BozoModeLine = 4;

    /// <summary>BRAINS CHASE MIKEY BUG — port-only: whether the arcade's brain target bug is kept.</summary>
    public const int BrainsChaseMikeyBugLine = 5;

    /// <summary>TANK SHELL BUG — port-only: whether the arcade's shell count bug is kept.</summary>
    public const int TankShellBugLine = 6;

    /// <summary>RESTORE FACTORY SETTINGS — an action row.</summary>
    public const int RestoreFactoryLine = 7;

    /// <summary>HIGH SCORE TABLE RESET — an action row.</summary>
    public const int HighScoreResetLine = 8;

    /// <summary>The YES/NO state of each action row.</summary>
    private readonly bool[] _armedLines = new bool[LineCount];

    /// <summary>The highlighted line, 0 to <see cref="LineCount"/> − 1.</summary>
    public int Line { get; private set; }

    /// <summary>True for the rows that perform something rather than hold a value.</summary>
    public static bool IsActionLine(int line) => line is RestoreFactoryLine or HighScoreResetLine;

    /// <summary>True once an action row has been set to YES (Enter will perform it).</summary>
    public bool IsArmed(int line) => _armedLines[line];

    /// <summary>Scrolls the highlight down one line, wrapping round the page.</summary>
    public void MoveDown() => Line = Line == LineCount - 1 ? 0 : Line + 1;

    /// <summary>Scrolls the highlight up one line, wrapping round the page.</summary>
    public void MoveUp() => Line = Line == 0 ? LineCount - 1 : Line - 1;

    /// <summary>
    /// Left (<paramref name="direction"/> −1) or right (+1) on the highlighted line: a value row
    /// steps to the next stop, an action row is set to NO (left) or YES (right).
    /// </summary>
    public void Change(GameSettings settings, int direction)
    {
        switch (Line)
        {
            case ExtraManLine:
                settings.BumpExtraManEvery(direction);
                break;
            case TurnsLine:
                settings.BumpTurnsPerPlayer(direction);
                break;
            case DifficultyLine:
                settings.BumpDifficulty(direction);
                break;
            case AttractSoundLine:
                settings.BumpAttractModeSound(direction);
                break;
            case TankShellBugLine:
                settings.BumpTankShellBug(direction);
                break;
            case BrainsChaseMikeyBugLine:
                settings.BumpBrainsChaseMikeyBug(direction);
                break;
            case BozoModeLine:
                settings.BumpBozoModeEnabled(direction);
                break;
            default:
                _armedLines[Line] = direction > 0;
                break;
        }
    }

    /// <summary>
    /// Enter on the highlighted line: performs an action row that reads YES, and does nothing
    /// otherwise (the arcade's "advance to activate").
    /// </summary>
    public SettingsAction Activate(GameSettings settings)
    {
        if (!IsActionLine(Line) || !_armedLines[Line])
        {
            return SettingsAction.None;
        }

        _armedLines[Line] = false;

        if (Line == RestoreFactoryLine)
        {
            settings.ResetToFactory();
            return SettingsAction.RestoreFactorySettings;
        }

        return SettingsAction.ResetHighScores;
    }

    /// <summary>The row's name, in the arcade's own words (the ON/OFF rows are the port's own).</summary>
    public static string GetLabel(int line) => line switch
    {
        ExtraManLine => "EXTRA MAN EVERY",
        TurnsLine => "TURNS PER PLAYER",
        DifficultyLine => "DIFFICULTY OF PLAY",
        AttractSoundLine => "ATTRACT MODE SOUND",
        TankShellBugLine => "TANK SHELL BUG",
        BrainsChaseMikeyBugLine => "BRAINS CHASE MIKEY BUG",
        BozoModeLine => "BOZO MODE",
        RestoreFactoryLine => "RESTORE FACTORY SETTINGS",
        HighScoreResetLine => "HIGH SCORE TABLE RESET",
        _ => throw new ArgumentOutOfRangeException(nameof(line), line, null),
    };

    /// <summary>The row's value: the setting itself, ON/OFF for the port's own rows, or NO/YES on an action row.</summary>
    public string GetValue(GameSettings settings, int line) => line switch
    {
        ExtraManLine => settings.ExtraManEveryPoints.ToString(),
        TurnsLine => settings.TurnsPerPlayer.ToString(),
        DifficultyLine => settings.Difficulty.ToString(),
        AttractSoundLine => GetOnOff(settings.AttractModeSoundEnabled),
        TankShellBugLine => GetOnOff(settings.TankShellBugEnabled),
        BrainsChaseMikeyBugLine => GetOnOff(settings.BrainsChaseMikeyBugEnabled),
        BozoModeLine => GetOnOff(settings.BozoModeEnabled),
        _ => _armedLines[line] ? "YES" : "NO",
    };

    /// <summary>
    /// The descriptive word under a value — the ROM's own option lists at $7041 (EXTRA MAN EVERY),
    /// $704C (TURNS PER PLAYER) and $7079 (DIFFICULTY OF PLAY). Empty where the arcade prints nothing.
    /// The port's bug and Bozo rows borrow "RECOMMENDED" for ON, which is the arcade as it shipped.
    /// </summary>
    public static string GetNote(GameSettings settings, int line) => line switch
    {
        ExtraManLine => settings.ExtraManEvery switch
        {
            < 20 => "NO EXTRA MEN",
            < 25 => "LIBERAL",
            < 30 => "RECOMMENDED",
            < 50 => "CONSERVATIVE",
            _ => "EXTRA CONSERVATIVE",
        },
        TurnsLine => settings.TurnsPerPlayer switch
        {
            < 2 => string.Empty,
            < 3 => "HIGH VOLUME ARCADES",
            < 4 => "RECOMMENDED",
            < 5 => "FOR WEAKER PLAYERS",
            _ => string.Empty,
        },
        DifficultyLine => settings.Difficulty switch
        {
            < 3 => "EXTRA LIBERAL",
            < 5 => "LIBERAL",
            < 6 => "RECOMMENDED",
            < 8 => "CONSERVATIVE",
            _ => "EXTRA CONSERVATIVE",
        },
        TankShellBugLine => GetArcadeNote(settings.TankShellBugEnabled),
        BrainsChaseMikeyBugLine => GetArcadeNote(settings.BrainsChaseMikeyBugEnabled),
        BozoModeLine => GetArcadeNote(settings.BozoModeEnabled),
        _ => string.Empty,
    };

    /// <summary>The word for an ON/OFF row's value.</summary>
    private static string GetOnOff(bool isOn) => isOn ? "ON" : "OFF";

    /// <summary>The word beside a row that is ON in the arcade: "RECOMMENDED" while it is on.</summary>
    private static string GetArcadeNote(bool isOn) => isOn ? "RECOMMENDED" : string.Empty;

    /// <summary>The hint under an action row once it has been set to YES.</summary>
    public string GetActionHint(int line) =>
        IsActionLine(line) && _armedLines[line] ? "PRESS ENTER" : string.Empty;
}
