using System.Globalization;
using RobotronSoundPlayer.Emulation;

namespace RobotronSoundPlayer;

/// <summary>Plays one request on the emulated board, through the speakers or into a WAV file, and says how it went.</summary>
/// <param name="soundRom">The sound ROM's bytes.</param>
internal sealed class SoundPlayback(byte[] soundRom)
{
    /// <summary>Plays a request through the speakers until it ends, its time runs out, or a key is pressed.</summary>
    /// <param name="request">What to play.</param>
    /// <param name="seconds">How long to play, or null to stop when it goes quiet.</param>
    public void PlayToSpeakers(ISoundRequest request, double? seconds)
    {
        var length = new PlaybackLength(seconds);
        Console.WriteLine($"Playing {request.Name} ({request.Description}). Press any key to stop.");
        bool wasStopped = SpeakerOutput.Play(StartSource(request), length);
        Console.WriteLine(wasStopped ? "Stopped." : DescribeEnding(length, seconds));
    }

    /// <summary>Writes a request to a WAV file.</summary>
    /// <param name="request">What to play.</param>
    /// <param name="seconds">How long to write, or null to stop when it goes quiet.</param>
    /// <param name="wavPath">The file to write.</param>
    public void WriteToFile(ISoundRequest request, double? seconds, string wavPath)
    {
        var length = new PlaybackLength(seconds);
        WavFileWriter.Write(wavPath, StartSource(request), length);
        Console.WriteLine($"Wrote {request.Name} to {wavPath}. {DescribeEnding(length, seconds)}");
    }

    /// <summary>Switches a fresh emulated board on and asks it for the request.</summary>
    /// <param name="request">What to play.</param>
    private BoardSoundSource StartSource(ISoundRequest request) => new(new EmulatedSoundBoard(soundRom), request);

    /// <summary>Says why the sound stopped.</summary>
    /// <param name="length">The length that stopped it.</param>
    /// <param name="seconds">The time the user gave, or null.</param>
    private static string DescribeEnding(PlaybackLength length, double? seconds)
    {
        string heard = length.HeardSeconds.ToString("F1", CultureInfo.InvariantCulture);
        if (seconds is not null)
        {
            return $"Played {heard} s, as asked.";
        }

        if (length.FellSilent)
        {
            return $"It went quiet after {heard} s.";
        }

        return $"Still sounding at the {PlaybackLength.LimitSeconds} s limit: this sound does not stop by itself. Use --seconds to hear more.";
    }
}
