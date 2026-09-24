using System.Text;
using Robotron2084.Input;

namespace Robotron2084.Persistence;

/// <summary>
/// Persists the port's control definitions (notes §101) in a plain INI file beside the
/// high score table: <c>%LocalAppData%\Robotron2084\controls.ini</c>. The INI form is
/// deliberate: the file can be read and hand-edited, and every value uses exactly the
/// vocabulary the DEFINE INPUTS page shows ("W", "P1 LEFT STICK UP"), so the file and the
/// page describe the same thing in the same words.
///
/// <code>
/// [player1]
/// moveup.key=W
/// moveup.pad=P1 LEFT STICK UP
/// ...
///
/// [player2]
/// ...
///
/// [pause]
/// input=P
/// </code>
///
/// A missing file, or one with anything unreadable in it, yields the factory scheme
/// rather than throwing — the same "a fresh cabinet comes up with its defaults" rule
/// <see cref="HighScoreStore"/> follows. Unknown sections, names and values are
/// ignored, so a file written by a later build still loads.
/// </summary>
public sealed class ControlSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Robotron2084",
        "controls.ini");

    /// <summary>Loads the saved definitions, or the factory scheme when there are none.</summary>
    public ControlSettings Load() => Load(FilePath);

    /// <summary>Saves the definitions.</summary>
    public void Save(ControlSettings settings) => Save(FilePath, settings);

    /// <summary>Loads from a given file — the seam the tests use.</summary>
    public static ControlSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return ControlSettings.Defaults();
        }

        try
        {
            return Parse(File.ReadAllLines(path));
        }
        catch (Exception)
        {
            return ControlSettings.Defaults();
        }
    }

    /// <summary>Saves to a given file — the seam the tests use.</summary>
    public static void Save(string path, ControlSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Write(settings), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Builds the file's text (also the tests' seam).</summary>
    public static string Write(ControlSettings settings)
    {
        var text = new StringBuilder();
        text.AppendLine("; Robotron 2084 (port) control definitions - see notes section 101.");
        text.AppendLine("; Edit by hand, or press F10 on the title screen and use the page.");
        text.AppendLine("; A value is what the page shows: a key name (W, UP, INSERT), or");
        text.AppendLine("; P<n> <BUTTON> / P<n> LS <DIR> / P<n> RS <DIR>. NONE means unbound.");
        text.AppendLine("; (Colons or dashes are accepted in place of the spaces when reading.)");

        for (int player = 0; player < ControlSettings.PlayerCount; player++)
        {
            text.AppendLine();
            text.AppendLine($"[player{player + 1}]");
            foreach (InputAction action in InputActions.All)
            {
                ActionBinding binding = settings[player][action];
                text.AppendLine($"{Name(action)}.key={binding.Key.DisplayName}");
                text.AppendLine($"{Name(action)}.pad={binding.Pad.DisplayName}");
            }
        }

        text.AppendLine();
        text.AppendLine("[pause]");
        text.AppendLine($"input={settings.Pause.DisplayName}");
        return text.ToString();
    }

    /// <summary>Parses the file's text; anything missing or unreadable keeps the factory value.</summary>
    public static ControlSettings Parse(IEnumerable<string> lines)
    {
        ControlSettings settings = ControlSettings.Defaults();
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
            if (separator <= 0)
            {
                continue;
            }

            string name = line[..separator].Trim().ToLowerInvariant();
            if (!InputBinding.TryParse(line[(separator + 1)..].Trim(), out InputBinding binding))
            {
                continue; // an unrecognised value leaves that slot at its default
            }

            Apply(settings, section, name, binding);
        }

        return settings;
    }

    private static void Apply(ControlSettings settings, string section, string name, InputBinding binding)
    {
        if (section == "pause")
        {
            // PAUSE is a single binding with no dot in its name, so any of these names,
            // old or new, sets the whole line.
            if (name is "input" or "key" or "pad")
            {
                settings.Pause = binding;
            }

            return;
        }

        bool padSlot = name.EndsWith(".pad", StringComparison.Ordinal);
        if (!padSlot && !name.EndsWith(".key", StringComparison.Ordinal))
        {
            return;
        }

        string key = name[..name.LastIndexOf('.')];

        if (!section.StartsWith("player", StringComparison.Ordinal)
            || !int.TryParse(section["player".Length..], out int number)
            || number is < 1 or > ControlSettings.PlayerCount)
        {
            return;
        }

        if (FindAction(key) is not { } action)
        {
            return;
        }

        PlayerControls controls = settings[number - 1];
        controls[action] = AssignSlot(controls[action], padSlot, binding);
    }

    /// <summary>Sets ONE device slot of a line — which is why <c>key=-</c> cannot wipe the pad.</summary>
    private static ActionBinding AssignSlot(ActionBinding line, bool padSlot, InputBinding binding) =>
        padSlot ? line with { Pad = binding } : line with { Key = binding };

    private static InputAction? FindAction(string name)
    {
        foreach (InputAction action in InputActions.All)
        {
            if (string.Equals(Name(action), name, StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }

        return null;
    }

    /// <summary>The INI name of an action: "moveup", "shootright", …</summary>
    private static string Name(InputAction action) => action.ToString().ToLowerInvariant();
}
