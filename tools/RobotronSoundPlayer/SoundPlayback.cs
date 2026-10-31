using System.Globalization;
using RobotronSoundPlayer.Emulation;

namespace RobotronSoundPlayer;

/// <summary>Plays one request on the emulated board, through the speakers or into a WAV file, and says how it went.</summary>
/// <param name="soundRom">The sound ROM's bytes.</param>
internal sealed class SoundPlayback(byte[] soundRom)
{
    /// <summary>Plays a request through the speakers until it ends, its time runs out, or a key is pressed.</summary>
    /// <param name="soundRequest">What to play.</param>
    /// <param name="seconds">How long to play, or null to stop when it goes quiet.</param>
    public void PlayToSpeakers(ISoundRequest soundRequest, double? seconds)
    {
        var playbackLength = new PlaybackLength(seconds);
        Console.WriteLine($"Playing {soundRequest.Name} ({soundRequest.Description}). Press any key to stop.");
        var wasStopped = SpeakerOutput.Play(StartSource(soundRequest), playbackLength);
        Console.WriteLine(wasStopped ? "Stopped." : DescribeEnding(playbackLength, seconds));
    }

    /// <summary>Writes a request to a WAV file.</summary>
    /// <param name="soundRequest">What to play.</param>
    /// <param name="seconds">How long to write, or null to stop when it goes quiet.</param>
    /// <param name="wavPath">The file to write.</param>
    public void WriteToFile(ISoundRequest soundRequest, double? seconds, string wavPath)
    {
        var playbackLength = new PlaybackLength(seconds);
        WavFileWriter.Write(wavPath, StartSource(soundRequest), playbackLength);
        Console.WriteLine($"Wrote {soundRequest.Name} to {wavPath}. {DescribeEnding(playbackLength, seconds)}");
    }

    /// <summary>Switches a fresh emulated board on and asks it for the request.</summary>
    /// <param name="soundRequest">What to play.</param>
    private BoardSoundSource StartSource(ISoundRequest soundRequest)
    {
        return new BoardSoundSource(new EmulatedSoundBoard(soundRom), soundRequest);
    }

    /// <summary>Says why the sound stopped.</summary>
    /// <param name="playbackLength">The length that stopped it.</param>
    /// <param name="seconds">The time the user gave, or null.</param>
    private static string DescribeEnding(PlaybackLength playbackLength, double? seconds)
    {
        var heard = playbackLength.HeardSeconds.ToString("F1", CultureInfo.InvariantCulture);
        if (seconds is not null) return $"Played {heard} s, as asked.";

        if (playbackLength.FellSilent) return $"It went quiet after {heard} s.";

        return
            $"Still sounding at the {PlaybackLength.LimitSeconds} s limit: this sound does not stop by itself. Use --seconds to hear more.";
    }
}
