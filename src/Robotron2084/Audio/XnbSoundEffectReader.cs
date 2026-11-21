using System.Text;
using Microsoft.Xna.Framework.Audio;
using Robotron2084.Audio.Models;

namespace Robotron2084.Audio;

/// <summary>
///     Reads the samples out of a sound effect that the content pipeline built (an <c>.xnb</c> file). MonoGame loads such a
///     file as a <see cref="SoundEffect" />, which cannot give its samples back.
/// </summary>
/// <remarks>
///     The file is: the letters <c>XNB</c>, a platform letter, a version byte, a flags byte and the file's length; the
///     type readers it needs, each a name and a version; the count of shared resources; the reader number of the sound;
///     then the sound itself, as a <c>WAVEFORMATEX</c> header, the samples, the loop start, the loop length and the
///     duration. Strings and counts are stored as 7-bit encoded numbers, the way <see cref="BinaryReader" /> reads them.
/// </remarks>
public static class XnbSoundEffectReader
{
    /// <summary>The only sample size this reader reads, in bits.</summary>
    private const ushort BitsPerSample = 16;

    /// <summary>The flag bits saying the file is compressed (LZX or LZ4), which this reader cannot read.</summary>
    private const byte CompressedFlags = 0xC0;

    /// <summary>The <c>WAVEFORMATEX</c> format tag of plain samples (PCM).</summary>
    private const ushort PcmFormatTag = 1;

    /// <summary>The bytes in a <c>WAVEFORMATEX</c> header up to and including its bits per sample.</summary>
    private const int ShortestFormatSize = 16;

    /// <summary>The first three bytes of every built file.</summary>
    private const string Signature = "XNB";

    /// <summary>Reads a built sound effect's samples.</summary>
    /// <param name="stream">The built file, from its start.</param>
    /// <returns>The samples, their rate and their channels.</returns>
    /// <exception cref="InvalidDataException">
    ///     The file is not a built sound effect, is compressed, or does not hold 16-bit samples in one or two channels.
    /// </exception>
    public static PcmSound Read(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, true);
        try
        {
            return ReadSound(reader);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The built sound effect ends too soon.", exception);
        }
    }

    /// <summary>Reads the file's header, skips its type readers, and reads the sound.</summary>
    /// <param name="reader">The reader, at the file's start.</param>
    /// <returns>The samples, their rate and their channels.</returns>
    /// <exception cref="InvalidDataException">The file is not one this reader can read.</exception>
    private static PcmSound ReadSound(BinaryReader reader)
    {
        var signature = Encoding.ASCII.GetString(reader.ReadBytes(Signature.Length));
        if (signature != Signature) throw new InvalidDataException("This is not a built content file.");

        _ = reader.ReadByte(); // the platform letter
        _ = reader.ReadByte(); // the version
        var flags = reader.ReadByte();
        if ((flags & CompressedFlags) != 0) throw new InvalidDataException("The built sound effect is compressed.");

        _ = reader.ReadInt32(); // the file's length
        var typeReaderCount = reader.Read7BitEncodedInt();
        for (var typeReader = 0; typeReader < typeReaderCount; typeReader++)
        {
            _ = reader.ReadString(); // the type reader's name
            _ = reader.ReadInt32(); // its version
        }

        _ = reader.Read7BitEncodedInt(); // the shared resources
        _ = reader.Read7BitEncodedInt(); // the sound's type reader

        var formatSize = reader.ReadInt32();
        if (formatSize < ShortestFormatSize) throw new InvalidDataException("The sound's format is too short.");

        var format = reader.ReadBytes(formatSize);
        var formatTag = BitConverter.ToUInt16(format, 0);
        var channels = BitConverter.ToUInt16(format, 2);
        var sampleRate = BitConverter.ToInt32(format, 4);
        var bitsPerSample = BitConverter.ToUInt16(format, 14);
        if (formatTag != PcmFormatTag || bitsPerSample != BitsPerSample || channels is < 1 or > 2)
            throw new InvalidDataException("The sound is not 16-bit samples in one or two channels.");

        var sampleBytes = reader.ReadInt32();
        var samples = reader.ReadBytes(sampleBytes);
        if (samples.Length != sampleBytes) throw new InvalidDataException("The built sound effect ends too soon.");

        return new PcmSound(samples, sampleRate, (AudioChannels)channels);
    }
}
