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
/// <item>RESTORE FACTORY SETTINGS — an action row;</item>
/// <item>HIGH SCORE TABLE RESET — an action row.</item>
/// </list>
///
/// Up/Down move the cursor round the five rows, Left/Right step the highlighted row's value, and the
/// two action rows take the arcade's own two steps: Left/Right set them to NO or YES, then Enter
/// performs them while they read YES (<c>YES ADVANCE TO ACTIVATE</c>).
///
/// The descriptive words under each value are the ROM's own option lists (the metadata at $6FD5 and
/// the strings it points at): "RECOMMENDED" is the stop the arcade's factory settings use.
/// </summary>
public sealed class SettingsModel
{
    /// <summary>Every line on the page.</summary>
    public const int LineCount = 6;

    /// <summary>EXTRA MAN EVERY — the first row, as on the cabinet.</summary>
    public const int ExtraManLine = 0;

    /// <summary>TURNS PER PLAYER — how many men a player starts with.</summary>
    public const int TurnsLine = 1;

    /// <summary>DIFFICULTY OF PLAY — the wave-table adjustment.</summary>
    public const int DifficultyLine = 2;

    /// <summary>ATTRACT MODE SOUND — port-only: whether the attract sequence makes its noises.</summary>
    public const int AttractSoundLine = 3;

    /// <summary>RESTORE FACTORY SETTINGS — an action row.</summary>
    public const int RestoreFactoryLine = 4;

    /// <summary>HIGH SCORE TABLE RESET — an action row.</summary>
    public const int HighScoreResetLine = 5;

    /// <summary>The YES/NO state of each action row.</summary>
    private readonly bool[] _armed = new bool[LineCount];

    /// <summary>The highlighted line, 0 to <see cref="LineCount"/> − 1.</summary>
    public int Line { get; private set; }

    /// <summary>True for the rows that perform something rather than hold a value.</summary>
    public static bool IsActionLine(int line) => line is RestoreFactoryLine or HighScoreResetLine;

    /// <summary>True once an action row has been set to YES (Enter will perform it).</summary>
    public bool IsArmed(int line) => _armed[line];

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
            default:
                _armed[Line] = direction > 0;
                break;
        }
    }

    /// <summary>
    /// Enter on the highlighted line: performs an action row that reads YES, and does nothing
    /// otherwise (the arcade's "advance to activate").
    /// </summary>
    public SettingsAction Activate(GameSettings settings)
    {
        if (!IsActionLine(Line) || !_armed[Line])
        {
            return SettingsAction.None;
        }

        _armed[Line] = false;

        if (Line == RestoreFactoryLine)
        {
            settings.ResetToFactory();
            return SettingsAction.RestoreFactorySettings;
        }

        return SettingsAction.ResetHighScores;
    }

    /// <summary>The row's name, in the arcade's own words (the last is the port's own).</summary>
    public static string GetLabel(int line) => line switch
    {
        ExtraManLine => "EXTRA MAN EVERY",
        TurnsLine => "TURNS PER PLAYER",
        DifficultyLine => "DIFFICULTY OF PLAY",
        AttractSoundLine => "ATTRACT MODE SOUND",
        RestoreFactoryLine => "RESTORE FACTORY SETTINGS",
        HighScoreResetLine => "HIGH SCORE TABLE RESET",
        _ => throw new ArgumentOutOfRangeException(nameof(line), line, null),
    };

    /// <summary>The row's value: the setting itself, ON/OFF for the sound row, or NO/YES on an action row.</summary>
    public string GetValue(GameSettings settings, int line) => line switch
    {
        ExtraManLine => settings.ExtraManEveryPoints.ToString(),
        TurnsLine => settings.TurnsPerPlayer.ToString(),
        DifficultyLine => settings.Difficulty.ToString(),
        AttractSoundLine => settings.AttractModeSound ? "ON" : "OFF",
        _ => _armed[line] ? "YES" : "NO",
    };

    /// <summary>
    /// The descriptive word under a value — the ROM's own option lists at $7041 (EXTRA MAN EVERY),
    /// $704C (TURNS PER PLAYER) and $7079 (DIFFICULTY OF PLAY). Empty where the arcade prints nothing.
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
        _ => string.Empty,
    };

    /// <summary>The hint under an action row once it has been set to YES.</summary>
    public string GetActionHint(int line) =>
        IsActionLine(line) && _armed[line] ? "PRESS ENTER" : string.Empty;
}
