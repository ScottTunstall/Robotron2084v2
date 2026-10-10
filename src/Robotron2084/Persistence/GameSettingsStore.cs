using System.Text;

namespace Robotron2084.Persistence;

/// <summary>
///     Persists the port's game settings (notes §131) in a plain INI file beside the controls and the
///     high score table: <c>%LocalAppData%\Robotron2084\settings.ini</c>. The INI form is deliberate, as
///     for <see cref="ControlSettingsStore" />: the file can be read and hand-edited, and every value uses
///     the same words the GAME ADJUSTMENT page shows.
///     <code>
/// [game]
/// extramanevery=25
/// turnsperplayer=3
/// difficulty=5
/// attractsound=0
/// tankshellbug=1
/// brainschasemikeybug=1
/// bozomode=1
/// </code>
///     A missing file yields the factory settings — the same "a fresh cabinet comes up with its
///     defaults" rule the other stores follow. A value outside the arcade's own range (or, for
///     EXTRA MAN EVERY, off its five-stop list) is ignored, so a hand edit cannot put the game
///     into a state the cabinet could not reach. A file that EXISTS but cannot be opened (locked,
///     access denied) throws a <see cref="PersistenceException" /> naming it (ERR-1): silently
///     falling back would let the next save overwrite the user's settings.
/// </summary>
public sealed class GameSettingsStore
{
    private const string Section = "game";

    private static readonly string FilePath = AppDataPaths.GetFilePath("gameSettings.ini");

    /// <summary>Loads from a given file — the seam the tests use.</summary>
    public static GameSettings Load(string path)
    {
        if (!File.Exists(path)) return GameSettings.CreateFactoryDefaults();

        try
        {
            return Parse(File.ReadAllLines(path));
        }
        catch (FileNotFoundException)
        {
            return GameSettings.CreateFactoryDefaults(); // removed between the check and the read
        }
        catch (IOException exception)
        {
            throw new PersistenceException($"The game settings file {path} could not be read: {exception.Message}",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PersistenceException($"The game settings file {path} could not be opened: {exception.Message}",
                exception);
        }
    }

    /// <summary>Parses the file's text; anything missing or out of range keeps the factory value.</summary>
    public static GameSettings Parse(IEnumerable<string> lines)
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();
        var section = string.Empty;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim().ToLowerInvariant();
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0 || section != Section) continue;

            var name = line[..separator].Trim().ToLowerInvariant();
            if (!int.TryParse(line[(separator + 1)..].Trim(), out var value)) continue;

            Apply(gameSettings, name, value);
        }

        return gameSettings;
    }

    /// <summary>Saves to a given file — the seam the tests use.</summary>
    public static void Save(string path, GameSettings gameSettings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Write(gameSettings), new UTF8Encoding(false));
    }

    /// <summary>Builds the file's text (also the tests' seam).</summary>
    public static string Write(GameSettings gameSettings)
    {
        var text = new StringBuilder();
        text.AppendLine("; Robotron 2084 (port) game gameSettings - see notes section 131.");
        text.AppendLine("; Edit by hand, or press F5 on the title screen and use the GAME ADJUSTMENT page.");
        text.AppendLine("; Difficulty 0-10 (5 recommended), turns per player 1-20 (3 recommended), and");
        text.AppendLine("; extramanevery in thousands of points: 0, 20, 25, 30 or 50. attractsound is 1 or 0.");
        text.AppendLine("; tankshellbug, brainschasemikeybug and bozomode are 1 (as the arcade) or 0.");
        text.AppendLine();
        text.AppendLine($"[{Section}]");
        text.AppendLine($"extramanevery={gameSettings.ExtraManEvery}");
        text.AppendLine($"turnsperplayer={gameSettings.TurnsPerPlayer}");
        text.AppendLine($"difficulty={gameSettings.Difficulty}");
        text.AppendLine($"attractsound={(gameSettings.AttractModeSoundEnabled ? 1 : 0)}");
        text.AppendLine($"tankshellbug={(gameSettings.TankShellBugEnabled ? 1 : 0)}");
        text.AppendLine($"brainschasemikeybug={(gameSettings.BrainsChaseMikeyBugEnabled ? 1 : 0)}");
        text.AppendLine($"bozomode={(gameSettings.BozoModeEnabled ? 1 : 0)}");
        return text.ToString();
    }

    /// <summary>Loads the saved settings, or the factory settings when there are none.</summary>
    public GameSettings Load()
    {
        return Load(FilePath);
    }

    /// <summary>Saves the settings.</summary>
    public void Save(GameSettings gameSettings)
    {
        Save(FilePath, gameSettings);
    }

    /// <summary>Applies one key when the value is one the cabinet could hold, ignoring it otherwise.</summary>
    private static void Apply(GameSettings gameSettings, string name, int value)
    {
        switch (name)
        {
            case "extramanevery" when GameSettings.IsExtraManEveryValue(value):
                gameSettings.ExtraManEvery = value;
                break;
            case "turnsperplayer"
                when value is >= GameSettings.MinimumTurnsPerPlayer and <= GameSettings.MaximumTurnsPerPlayer:
                gameSettings.TurnsPerPlayer = value;
                break;
            case "difficulty" when value is >= GameSettings.MinimumDifficulty and <= GameSettings.MaximumDifficulty:
                gameSettings.Difficulty = value;
                break;
            case "attractsound" when value is 0 or 1:
                gameSettings.AttractModeSoundEnabled = value == 1;
                break;
            case "tankshellbug" when value is 0 or 1:
                gameSettings.TankShellBugEnabled = value == 1;
                break;
            case "brainschasemikeybug" when value is 0 or 1:
                gameSettings.BrainsChaseMikeyBugEnabled = value == 1;
                break;
            case "bozomode" when value is 0 or 1:
                gameSettings.BozoModeEnabled = value == 1;
                break;
        }
    }
}
