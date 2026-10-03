using System.Text;

namespace Robotron2084.Persistence;

/// <summary>
/// Persists the port's game settings (notes §131) in a plain INI file beside the controls and the
/// high score table: <c>%LocalAppData%\Robotron2084\settings.ini</c>. The INI form is deliberate, as
/// for <see cref="ControlSettingsStore"/>: the file can be read and hand-edited, and every value uses
/// the same words the GAME ADJUSTMENT page shows.
///
/// <code>
/// [game]
/// extramanevery=25
/// turnsperplayer=3
/// difficulty=5
/// </code>
///
/// A missing file, or one with anything unreadable in it, yields the factory settings rather than
/// throwing — the same "a fresh cabinet comes up with its defaults" rule the other stores follow.
/// A value outside the arcade's own range (or, for EXTRA MAN EVERY, off its five-stop list) is
/// ignored, so a hand edit cannot put the game into a state the cabinet could not reach.
/// </summary>
public sealed class GameSettingsStore
{
    private const string Section = "game";

    private static readonly string FilePath = AppDataPaths.GetFilePath("settings.ini");

    /// <summary>Loads from a given file — the seam the tests use.</summary>
    public static GameSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return GameSettings.CreateFactoryDefaults();
        }

        try
        {
            return Parse(File.ReadAllLines(path));
        }
        catch (Exception)
        {
            return GameSettings.CreateFactoryDefaults();
        }
    }

    /// <summary>Parses the file's text; anything missing or out of range keeps the factory value.</summary>
    public static GameSettings Parse(IEnumerable<string> lines)
    {
        GameSettings settings = GameSettings.CreateFactoryDefaults();
        string section = string.Empty;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#')
            {
                continue;
            }

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim().ToLowerInvariant();
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0 || section != Section)
            {
                continue;
            }

            string name = line[..separator].Trim().ToLowerInvariant();
            if (!int.TryParse(line[(separator + 1)..].Trim(), out int value))
            {
                continue;
            }

            Apply(settings, name, value);
        }

        return settings;
    }

    /// <summary>Saves to a given file — the seam the tests use.</summary>
    public static void Save(string path, GameSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Write(settings), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Builds the file's text (also the tests' seam).</summary>
    public static string Write(GameSettings settings)
    {
        var text = new StringBuilder();
        text.AppendLine("; Robotron 2084 (port) game settings - see notes section 131.");
        text.AppendLine("; Edit by hand, or press F5 on the title screen and use the GAME ADJUSTMENT page.");
        text.AppendLine("; Difficulty 0-10 (5 recommended), turns per player 1-20 (3 recommended), and");
        text.AppendLine("; extramanevery in thousands of points: 0, 20, 25, 30 or 50. attractsound is 1 or 0.");
        text.AppendLine();
        text.AppendLine($"[{Section}]");
        text.AppendLine($"extramanevery={settings.ExtraManEvery}");
        text.AppendLine($"turnsperplayer={settings.TurnsPerPlayer}");
        text.AppendLine($"difficulty={settings.Difficulty}");
        text.AppendLine($"attractsound={(settings.AttractModeSound ? 1 : 0)}");
        return text.ToString();
    }

    /// <summary>Loads the saved settings, or the factory settings when there are none.</summary>
    public GameSettings Load() => Load(FilePath);

    /// <summary>Saves the settings.</summary>
    public void Save(GameSettings settings) => Save(FilePath, settings);

    /// <summary>Applies one key when the value is one the cabinet could hold, ignoring it otherwise.</summary>
    private static void Apply(GameSettings settings, string name, int value)
    {
        switch (name)
        {
            case "extramanevery" when GameSettings.IsExtraManEveryValue(value):
                settings.ExtraManEvery = value;
                break;
            case "turnsperplayer" when value is >= GameSettings.MinimumTurnsPerPlayer and <= GameSettings.MaximumTurnsPerPlayer:
                settings.TurnsPerPlayer = value;
                break;
            case "difficulty" when value is >= GameSettings.MinimumDifficulty and <= GameSettings.MaximumDifficulty:
                settings.Difficulty = value;
                break;
            case "attractsound" when value is 0 or 1:
                settings.AttractModeSound = value == 1;
                break;
        }
    }
}
