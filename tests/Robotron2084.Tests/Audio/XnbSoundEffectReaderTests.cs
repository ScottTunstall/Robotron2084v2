using System.Text;
using Microsoft.Xna.Framework.Audio;
using Robotron2084.Audio;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>Reading the samples out of a sound effect the content pipeline built.</summary>
public sealed class XnbSoundEffectReaderTests
{
    /// <summary>The <c>WAVEFORMATEX</c> format tag of plain samples (PCM).</summary>
    private const ushort PcmFormatTag = 1;

    /// <summary>The sample rate of the made-up files.</summary>
    private const int SampleRate = 22050;

    [Fact]
    public void EveryBuiltBerzerkWordReadsAndLosesItsSilence()
    {
        var wordNames = SpeechFolders.GetLineNames(SpeechFolders.Berzerk);
        Assert.NotEmpty(wordNames);

        foreach (var wordName in wordNames)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Content", "Speech", SpeechFolders.Berzerk,
                wordName.ToLowerInvariant() + ".xnb");
            using var stream = File.OpenRead(path);

            var pcmSound = XnbSoundEffectReader.Read(stream);
            var trimmed = pcmSound.GetTrimmed(SoundTuning.BerzerkWordSilenceLevel, SoundTuning.BerzerkWordSilenceMarginMilliseconds);

            Assert.True(trimmed.Samples.Length < pcmSound.Samples.Length, wordName);
            Assert.True(trimmed.Samples.Length > 0, wordName);
        }
    }

    [Fact]
    public void ReadsTheSamplesTheirRateAndTheirChannels()
    {
        byte[] samples = [1, 2, 3, 4, 5, 6, 7, 8];

        var pcmSound = XnbSoundEffectReader.Read(CreateBuiltFile(samples, 2));

        Assert.Equal(samples, pcmSound.Samples);
        Assert.Equal(SampleRate, pcmSound.SampleRate);
        Assert.Equal(AudioChannels.Stereo, pcmSound.Channels);
    }

    [Fact]
    public void RejectsACompressedFile()
    {
        Assert.Throws<InvalidDataException>(() => XnbSoundEffectReader.Read(CreateBuiltFile([0, 0], flags: 0x80)));
    }

    [Fact]
    public void RejectsAFileThatEndsTooSoon()
    {
        var whole = CreateBuiltFile([1, 2, 3, 4, 5, 6]).ToArray();
        using MemoryStream cut = new(whole[..^16]);

        Assert.Throws<InvalidDataException>(() => XnbSoundEffectReader.Read(cut));
    }

    [Fact]
    public void RejectsAFileThatIsNotABuiltContentFile()
    {
        using MemoryStream stream = new("RIFF0000WAVE"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => XnbSoundEffectReader.Read(stream));
    }

    [Fact]
    public void RejectsSamplesThatAreNotPcm()
    {
        Assert.Throws<InvalidDataException>(() => XnbSoundEffectReader.Read(CreateBuiltFile([0, 0], formatTag: 2)));
    }

    [Fact]
    public void RejectsSamplesThatAreNotSixteenBits()
    {
        Assert.Throws<InvalidDataException>(() => XnbSoundEffectReader.Read(CreateBuiltFile([0, 0], bitsPerSample: 8)));
    }

    /// <summary>Makes a built sound effect file the way the content pipeline writes one.</summary>
    private static MemoryStream CreateBuiltFile(byte[] samples, ushort channels = 1, ushort formatTag = PcmFormatTag,
        ushort bitsPerSample = 16, byte flags = 0)
    {
        MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, true))
        {
            writer.Write("XNB"u8.ToArray());
            writer.Write((byte)'d');
            writer.Write((byte)5);
            writer.Write(flags);
            writer.Write(0); // the file's length, which the reader does not need
            writer.Write7BitEncodedInt(1);
            writer.Write("Microsoft.Xna.Framework.Content.SoundEffectReader");
            writer.Write(0);
            writer.Write7BitEncodedInt(0);
            writer.Write7BitEncodedInt(1);
            writer.Write(18); // WAVEFORMATEX, with its cbSize
            writer.Write(formatTag);
            writer.Write(channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * channels * bitsPerSample / 8);
            writer.Write((ushort)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write((ushort)0);
            writer.Write(samples.Length);
            writer.Write(samples);
            writer.Write(0); // the loop start
            writer.Write(0); // the loop length
            writer.Write(0); // the duration
        }

        stream.Position = 0;
        return stream;
    }
}
