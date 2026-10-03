using System.Globalization;

namespace RobotronSoundPlayer;

/// <summary>What the user typed after the player's name: a command, what to play, and the options.</summary>
internal sealed class CommandLine
{
    private readonly List<string> _words = [];

    /// <summary>Reads the command line.</summary>
    /// <param name="args">The words after the player's name.</param>
    public CommandLine(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count && Error is null; i++)
        {
            i = ReadWord(args, i);
        }

        if (Error is null && _words.Count > 2)
        {
            Error = $"Too many words: {string.Join(" ", _words)}";
        }
    }

    /// <summary>The command: <c>list</c>, <c>play</c> or <c>help</c>, or null to show the menu.</summary>
    public string? Command => _words.Count > 0 ? _words[0].ToLowerInvariant() : null;

    /// <summary>What to play: a name, a place in the list or a sound number.</summary>
    public string? Choice => _words.Count > 1 ? _words[1] : null;

    /// <summary>How long to play, in seconds, or null to stop when the sound goes quiet.</summary>
    public double? Seconds { get; private set; }

    /// <summary>The WAV file to write instead of playing, or null to play through the speakers.</summary>
    public string? WavPath { get; private set; }

    /// <summary>Where the sound ROM is, or null to look for it.</summary>
    public string? RomPath { get; private set; }

    /// <summary>What was wrong with the command line, or null when it read cleanly.</summary>
    public string? Error { get; private set; }

    /// <summary>Reads one word, or one option and its value.</summary>
    /// <param name="args">The words.</param>
    /// <param name="index">Where the word is.</param>
    /// <returns>Where the last word read is.</returns>
    private int ReadWord(IReadOnlyList<string> args, int index)
    {
        string word = args[index];
        if (!word.StartsWith("--", StringComparison.Ordinal))
        {
            _words.Add(word);
            return index;
        }

        if (index + 1 >= args.Count)
        {
            Error = $"{word} needs a value after it.";
            return index;
        }

        ReadOption(word, args[index + 1]);
        return index + 1;
    }

    /// <summary>Reads an option's value.</summary>
    /// <param name="option">The option, such as <c>--seconds</c>.</param>
    /// <param name="value">The word after it.</param>
    private void ReadOption(string option, string value)
    {
        switch (option.ToLowerInvariant())
        {
            case "--seconds":
                ReadSeconds(value);
                break;
            case "--wav":
                WavPath = value;
                break;
            case "--rom":
                RomPath = value;
                break;
            default:
                Error = $"Unknown option {option}.";
                break;
        }
    }

    /// <summary>Reads the <c>--seconds</c> value, which must be a number above zero.</summary>
    /// <param name="value">The value.</param>
    private void ReadSeconds(string value)
    {
        bool isNumber = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds);
        if (!isNumber || seconds <= 0)
        {
            Error = $"--seconds needs a number above zero, not {value}.";
            return;
        }

        Seconds = seconds;
    }
}
