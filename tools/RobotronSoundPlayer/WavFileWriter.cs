using System.Text;
using Robotron2084.Tuning;

namespace RobotronSoundPlayer;

/// <summary>Writes a sound to a 16-bit mono WAV file instead of the speakers.</summary>
internal static class WavFileWriter
{
    /// <summary>Bytes in the header before the samples.</summary>
    private const int HeaderBytes = 44;

    /// <summary>Bytes in the format chunk's body.</summary>
    private const int FormatChunkBytes = 16;

    /// <summary>The format code for plain PCM.</summary>
    private const short PcmFormat = 1;

    /// <summary>Mono: one channel.</summary>
    private const short Channels = 1;

    /// <summary>Bits in one sample.</summary>
    private const short BitsPerSample = 16;

    /// <summary>The bytes before the RIFF size that the RIFF size does not count.</summary>
    private const int RiffPreambleBytes = 8;

    /// <summary>Makes a sound until its length says to stop, and writes it to a file.</summary>
    /// <param name="path">The WAV file to write.</param>
    /// <param name="source">Makes the sound.</param>
    /// <param name="length">Says when to stop.</param>
    public static void Write(string path, ISoundSource source, PlaybackLength length)
    {
        using var pcm = new MemoryStream();
        var samples = new float[PlayerAudio.SamplesPerPortTick];
        while (!length.IsOver)
        {
            source.RenderPortTick(samples);
            length.Count(samples);
            pcm.Write(PlayerAudio.ToPcm16(samples, SoundTuning.MasterVolume));
        }

        using var file = new BinaryWriter(File.Create(path));
        WriteHeader(file, (int)pcm.Length);
        pcm.WriteTo(file.BaseStream);
    }

    /// <summary>Writes the RIFF/WAVE header for a given number of sample bytes.</summary>
    private static void WriteHeader(BinaryWriter file, int dataBytes)
    {
        const short blockAlign = Channels * BitsPerSample / 8;
        file.Write(Encoding.ASCII.GetBytes("RIFF"));
        file.Write(HeaderBytes - RiffPreambleBytes + dataBytes);
        file.Write(Encoding.ASCII.GetBytes("WAVE"));
        file.Write(Encoding.ASCII.GetBytes("fmt "));
        file.Write(FormatChunkBytes);
        file.Write(PcmFormat);
        file.Write(Channels);
        file.Write(PlayerAudio.SampleRate);
        file.Write(PlayerAudio.SampleRate * blockAlign);
        file.Write(blockAlign);
        file.Write(BitsPerSample);
        file.Write(Encoding.ASCII.GetBytes("data"));
        file.Write(dataBytes);
    }
}
