using System.Globalization;
using Robotron2084.Audio;

namespace Robotron2084.States;

/// <summary>The list of things the sound test page can play, in the order the page steps through them.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>The board's 63 sounds come first, in sound-number order (<see cref="BoardSounds" />).</item>
///         <item>The transporter comes next.</item>
///         <item>
///             The speech lines come last, in the order of <see cref="SpeechLinePaths" />. Speech is port only: the arcade
///             has no speech sound chip.
///         </item>
///     </list>
/// </remarks>
internal static class SoundTestEntries
{
    /// <summary>The heading of a speech entry, before its number.</summary>
    private const string SpeechHeading = "SPEECH ";

    /// <summary>The words shown under a speech entry's name.</summary>
    private const string SpeechDescription = "SPEECH SAMPLE";

    /// <summary>Every entry the page steps through.</summary>
    public static readonly IReadOnlyList<SoundTestEntry> All = CreateAll();

    private static List<SoundTestEntry> CreateAll()
    {
        var entries = new List<SoundTestEntry>();
        foreach (var boardSound in BoardSounds.All)
        {
            var heading = "SOUND " + boardSound.Number.ToString("X2", CultureInfo.InvariantCulture);
            entries.Add(new SoundTestEntry(heading, boardSound.Name, boardSound.Description, boardSound.Number));
        }

        entries.Add(new SoundTestEntry("TRANSPORTER", "TRSPRC", "BRAIN WAVE", null));
        entries.AddRange(CreateSpeechEntries());
        return entries;
    }

    private static List<SoundTestEntry> CreateSpeechEntries()
    {
        var entries = new List<SoundTestEntry>();
        for (var index = 0; index < SpeechLinePaths.All.Length; index++)
        {
            var lineName = Path.GetFileName(SpeechLinePaths.All[index]).ToUpperInvariant();
            var number = (index + 1).ToString(CultureInfo.InvariantCulture);
            entries.Add(new SoundTestEntry(SpeechHeading + number, lineName, SpeechDescription, null, lineName));
        }

        return entries;
    }
}
