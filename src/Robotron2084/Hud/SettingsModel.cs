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
    ResetHighScores
}

/// <summary>
///     The GAME ADJUSTMENT page's logic, with no MonoGame in it (notes §131) — the same split the
///     DEFINITIONS page uses (<see cref="DefineInputsModel" />): the state draws and reads hardware, this
///     decides what a keypress MEANS.
///     The page is the arcade's own gameplay rows, in the arcade's order, plus the port's own sound row
///     (its coin/pricing rows are not built):
///     <list type="number">
///         <item>
///             EXTRA MAN EVERY — the score that earns a spare life, stepping the ROM's own five stops
///             (0, 20, 25, 30, 50 thousand);
///         </item>
///         <item>TURNS PER PLAYER — the lives a player starts with, 1-20;</item>
///         <item>DIFFICULTY OF PLAY — 0-10;</item>
///         <item>
///             ATTRACT MODE SOUND — port-only: whether the attract sequence (the demo machine playing
///             itself) keeps its sound;
///         </item>
///         <item>BOZO MODE — port-only: whether the arcade's mercy for a player losing lives early is given;</item>
///         <item>BRAINS CHASE MIKEY BUG — port-only: whether every brain starts the wave chasing Mikey, as in the arcade;</item>
///         <item>TANK SHELL BUG — port-only: whether a fizzled shell stays on the wave's shell count, as in the arcade;</item>
///         <item>RESTORE FACTORY SETTINGS — an action row;</item>
///         <item>HIGH SCORE TABLE RESET — an action row.</item>
///     </list>
///     Up/Down move the cursor round the rows, Left/Right step the highlighted row's value, and the
///     two action rows take the arcade's own two steps: Left/Right set them to NO or YES, then Enter
///     performs them while they read YES (<c>YES ADVANCE TO ACTIVATE</c>).
///     The descriptive words under each value are the ROM's own option lists (the metadata at $6FD5 and
///     the strings it points at): "RECOMMENDED" is the stop the arcade's factory settings use.
/// </summary>
public sealed class SettingsModel
{
    /// <summary>ATTRACT MODE SOUND — port-only: whether the attract sequence makes its noises.</summary>
    public const int AttractSoundLine = 3;

    /// <summary>BOZO MODE — port-only: whether the arcade's mercy for a losing player is given.</summary>
    public const int BozoModeLine = 4;

    /// <summary>BRAINS CHASE MIKEY BUG — port-only: whether the arcade's brain target bug is kept.</summary>
    public const int BrainsChaseMikeyBugLine = 5;

    /// <summary>DIFFICULTY OF PLAY — the wave-table adjustment.</summary>
    public const int DifficultyLine = 2;

    /// <summary>EXTRA MAN EVERY — the first row, as on the cabinet.</summary>
    public const int ExtraLifeLine = 0;

    /// <summary>HIGH SCORE TABLE RESET — an action row.</summary>
    public const int HighScoreResetLine = 8;

    /// <summary>Every line on the page. It is the size of <see cref="_armedLines" />.</summary>
    public const int LineCount = 9;

    /// <summary>RESTORE FACTORY SETTINGS — an action row.</summary>
    public const int RestoreFactoryLine = 7;

    /// <summary>TANK SHELL BUG — port-only: whether the arcade's shell count bug is kept.</summary>
    public const int TankShellBugLine = 6;

    /// <summary>TURNS PER PLAYER — how many lives a player starts with.</summary>
    public const int TurnsLine = 1;

    /// <summary>The YES/NO state of each action row.</summary>
    private readonly bool[] _armedLines = new bool[LineCount];

    /// <summary>The highlighted line, 0 to <see cref="LineCount" /> − 1.</summary>
    public int Line { get; private set; }

    /// <summary>The row's name, in the arcade's own words (the ON/OFF rows are the port's own).</summary>
    public static string GetLabel(int line)
    {
        return line switch
        {
            ExtraLifeLine => "EXTRA MAN EVERY",
            TurnsLine => "TURNS PER PLAYER",
            DifficultyLine => "DIFFICULTY OF PLAY",
            AttractSoundLine => "ATTRACT MODE SOUND",
            TankShellBugLine => "TANK SHELL BUG",
            BrainsChaseMikeyBugLine => "BRAINS CHASE MIKEY BUG",
            BozoModeLine => "BOZO MODE",
            RestoreFactoryLine => "RESTORE FACTORY SETTINGS",
            HighScoreResetLine => "HIGH SCORE TABLE RESET",
            _ => throw new ArgumentOutOfRangeException(nameof(line), line, null)
        };
    }

    /// <summary>
    ///     The descriptive word under a value — the ROM's own option lists at $7041 (EXTRA MAN EVERY),
    ///     $704C (TURNS PER PLAYER) and $7079 (DIFFICULTY OF PLAY). Empty where the arcade prints nothing.
    ///     The port's bug and Bozo rows borrow "RECOMMENDED" for ON, which is the arcade as it shipped.
    /// </summary>
    public static string GetNote(GameSettings gameSettings, int line)
    {
        return line switch
        {
            ExtraLifeLine => gameSettings.ExtraLifeEvery switch
            {
                < 20 => "NO EXTRA MEN",
                < 25 => "LIBERAL",
                < 30 => "RECOMMENDED",
                < 50 => "CONSERVATIVE",
                _ => "EXTRA CONSERVATIVE"
            },
            TurnsLine => gameSettings.TurnsPerPlayer switch
            {
                < 2 => string.Empty,
                < 3 => "HIGH VOLUME ARCADES",
                < 4 => "RECOMMENDED",
                < 5 => "FOR WEAKER PLAYERS",
                _ => string.Empty
            },
            DifficultyLine => gameSettings.Difficulty switch
            {
                < 3 => "EXTRA LIBERAL",
                < 5 => "LIBERAL",
                < 6 => "RECOMMENDED",
                < 8 => "CONSERVATIVE",
                _ => "EXTRA CONSERVATIVE"
            },
            TankShellBugLine => GetArcadeNote(gameSettings.TankShellBugEnabled),
            BrainsChaseMikeyBugLine => GetArcadeNote(gameSettings.BrainsChaseMikeyBugEnabled),
            BozoModeLine => GetArcadeNote(gameSettings.BozoModeEnabled),
            _ => string.Empty
        };
    }

    /// <summary>True for the rows that perform something rather than hold a value.</summary>
    public static bool IsActionLine(int line)
    {
        return line is RestoreFactoryLine or HighScoreResetLine;
    }

    /// <summary>
    ///     Enter on the highlighted line: performs an action row that reads YES, and does nothing
    ///     otherwise (the arcade's "advance to activate").
    /// </summary>
    public SettingsAction Activate(GameSettings gameSettings)
    {
        if (!IsActionLine(Line) || !_armedLines[Line]) return SettingsAction.None;

        _armedLines[Line] = false;

        if (Line == RestoreFactoryLine)
        {
            gameSettings.ResetToFactory();
            return SettingsAction.RestoreFactorySettings;
        }

        return SettingsAction.ResetHighScores;
    }

    /// <summary>
    ///     Left (<paramref name="direction" /> −1) or right (+1) on the highlighted line: a value row
    ///     steps to the next stop, an action row is set to NO (left) or YES (right).
    /// </summary>
    public void Change(GameSettings gameSettings, int direction)
    {
        switch (Line)
        {
            case ExtraLifeLine:
                gameSettings.BumpExtraLifeEvery(direction);
                break;

            case TurnsLine:
                gameSettings.BumpTurnsPerPlayer(direction);
                break;

            case DifficultyLine:
                gameSettings.BumpDifficulty(direction);
                break;

            case AttractSoundLine:
                gameSettings.BumpAttractModeSound(direction);
                break;

            case TankShellBugLine:
                gameSettings.BumpTankShellBug(direction);
                break;

            case BrainsChaseMikeyBugLine:
                gameSettings.BumpBrainsChaseMikeyBug(direction);
                break;

            case BozoModeLine:
                gameSettings.BumpBozoModeEnabled(direction);
                break;

            default:
                _armedLines[Line] = direction > 0;
                break;
        }
    }

    /// <summary>The hint under an action row once it has been set to YES.</summary>
    public string GetActionHint(int line)
    {
        var isArmedActionLine = IsActionLine(line) && _armedLines[line];
        if (isArmedActionLine) return "PRESS ENTER";

        return string.Empty;
    }

    /// <summary>The row's value: the setting itself, ON/OFF for the port's own rows, or NO/YES on an action row.</summary>
    public string GetValue(GameSettings gameSettings, int line)
    {
        return line switch
        {
            ExtraLifeLine => gameSettings.GetExtraLifeEveryPoints().ToString(),
            TurnsLine => gameSettings.TurnsPerPlayer.ToString(),
            DifficultyLine => gameSettings.Difficulty.ToString(),
            AttractSoundLine => GetOnOff(gameSettings.AttractModeSoundEnabled),
            TankShellBugLine => GetOnOff(gameSettings.TankShellBugEnabled),
            BrainsChaseMikeyBugLine => GetOnOff(gameSettings.BrainsChaseMikeyBugEnabled),
            BozoModeLine => GetOnOff(gameSettings.BozoModeEnabled),
            _ => GetYesNo(_armedLines[line])
        };
    }

    /// <summary>True once an action row has been set to YES (Enter will perform it).</summary>
    public bool IsArmed(int line)
    {
        return _armedLines[line];
    }

    /// <summary>Scrolls the highlight down one line, wrapping round the page.</summary>
    public void MoveDown()
    {
        var isOnLastLine = Line == LineCount - 1;
        if (isOnLastLine) Line = 0;
        else Line++;
    }

    /// <summary>Scrolls the highlight up one line, wrapping round the page.</summary>
    public void MoveUp()
    {
        var isOnFirstLine = Line == 0;
        if (isOnFirstLine) Line = LineCount - 1;
        else Line--;
    }

    /// <summary>The word beside a row that is ON in the arcade: "RECOMMENDED" while it is on.</summary>
    private static string GetArcadeNote(bool isOn)
    {
        return isOn ? "RECOMMENDED" : string.Empty;
    }

    /// <summary>The word for an ON/OFF row's value.</summary>
    private static string GetOnOff(bool isOn)
    {
        return isOn ? "ON" : "OFF";
    }

    /// <summary>The word for a YES/NO row's value.</summary>
    private static string GetYesNo(bool isYes)
    {
        return isYes ? "YES" : "NO";
    }
}
