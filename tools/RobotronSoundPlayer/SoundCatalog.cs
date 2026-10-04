using System.Globalization;
using System.Reflection;
using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>
/// Everything the player can play: each of the game's sound tables, the transporter's hum, and each of
/// the board's sound numbers on its own. The tables are read from <see cref="SoundTables"/> itself, so a
/// table added there shows up here without a change.
/// </summary>
internal sealed class SoundCatalog
{
    /// <summary>How many sound numbers the board has: six sound lines (<c>RRF.ASM</c>: "B0-B5 SOUND").</summary>
    private const int SoundNumberCount = 64;

    /// <summary>Creates the catalog.</summary>
    public SoundCatalog()
    {
        GameSounds = [.. ReadSoundTables(), new TransporterRequest()];
        SoundNumbers = [.. Enumerable.Range(0, SoundNumberCount).Select(number => new SoundNumberRequest(number))];
    }

    /// <summary>The sounds the game asks for, by name.</summary>
    public IReadOnlyList<ISoundRequest> GameSounds { get; }

    /// <summary>Every sound number the board can be sent, in order.</summary>
    public IReadOnlyList<ISoundRequest> SoundNumbers { get; }

    /// <summary>
    /// Finds what the user asked for: a game sound's name (any case), a game sound's place in the
    /// list (from 1), or a sound number written as <c>$0E</c>, <c>0x0E</c> or <c>#14</c>.
    /// </summary>
    /// <param name="choice">What the user typed.</param>
    /// <returns>The sound, or null when nothing matches.</returns>
    public ISoundRequest? Find(string choice)
    {
        string trimmed = choice.Trim();
        ISoundRequest? byName = GameSounds.FirstOrDefault(sound => sound.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return byName;
        }

        int? soundNumber = ParseSoundNumber(trimmed);
        if (soundNumber is not null)
        {
            return SoundNumbers[soundNumber.Value];
        }

        bool isPlace = int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int place);
        return isPlace && place >= 1 && place <= GameSounds.Count ? GameSounds[place - 1] : null;
    }

    /// <summary>Reads every table in <see cref="SoundTables"/>, in name order.</summary>
    private static IEnumerable<ISoundRequest> ReadSoundTables()
    {
        return typeof(SoundTables)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(SoundSequence))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => new SoundTableRequest(field.Name, (SoundSequence)field.GetValue(null)!));
    }

    /// <summary>Reads a sound number written as <c>$0E</c>, <c>0x0E</c> or <c>#14</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The sound number, or null when the text is not one or it is out of range.</returns>
    private static int? ParseSoundNumber(string text)
    {
        int? value = ParseWithPrefix(text, "$", NumberStyles.HexNumber)
            ?? ParseWithPrefix(text, "0x", NumberStyles.HexNumber)
            ?? ParseWithPrefix(text, "#", NumberStyles.None);
        return value is >= 0 and < SoundNumberCount ? value : null;
    }

    /// <summary>Reads a number that follows a prefix.</summary>
    /// <param name="text">The text.</param>
    /// <param name="prefix">The prefix that must start it.</param>
    /// <param name="style">How the digits are written.</param>
    /// <returns>The number, or null when the text does not start with the prefix or the digits do not read.</returns>
    private static int? ParseWithPrefix(string text, string prefix, NumberStyles style)
    {
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        bool isNumber = int.TryParse(text.AsSpan(prefix.Length), style, CultureInfo.InvariantCulture, out int value);
        return isNumber ? value : null;
    }
}
