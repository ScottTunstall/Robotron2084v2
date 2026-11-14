using Microsoft.Xna.Framework.Audio;

namespace Robotron2084.Audio.Models;

/// <summary>A sound as 16-bit samples, ready to be made into a <see cref="SoundEffect" />.</summary>
/// <param name="Samples">The samples, two bytes each, low byte first. With two channels, left and right take turns.</param>
/// <param name="SampleRate">How many samples a second each channel plays.</param>
/// <param name="Channels">One channel or two.</param>
public sealed record PcmSound(byte[] Samples, int SampleRate, AudioChannels Channels)
{
    /// <summary>The bytes in one sample.</summary>
    private const int BytesPerSample = 2;

    /// <summary>The milliseconds in a second.</summary>
    private const int MillisecondsPerSecond = 1000;

    /// <summary>
    ///     Gets the sound with the silence at its start and end cut off. A little of the silence is kept at each end, so
    ///     the sound still starts and stops on quiet samples.
    /// </summary>
    /// <param name="silenceLevel">The loudest a sample can be and still count as silence, from 0 to 32767.</param>
    /// <param name="marginMilliseconds">How much of the silence is kept at each end, in milliseconds.</param>
    /// <returns>The shorter sound, or this sound when it is all silence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="silenceLevel" /> or <paramref name="marginMilliseconds" /> is less than 0.
    /// </exception>
    public PcmSound GetTrimmed(int silenceLevel, int marginMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(silenceLevel);
        ArgumentOutOfRangeException.ThrowIfNegative(marginMilliseconds);

        var bytesPerFrame = BytesPerSample * (int)Channels;
        var frameCount = Samples.Length / bytesPerFrame;
        var firstLoudFrame = -1;
        var lastLoudFrame = -1;
        for (var frame = 0; frame < frameCount; frame++)
        {
            if (!IsLoud(frame, bytesPerFrame, silenceLevel)) continue;

            if (firstLoudFrame < 0) firstLoudFrame = frame;
            lastLoudFrame = frame;
        }

        if (firstLoudFrame < 0) return this;

        var marginFrames = SampleRate * marginMilliseconds / MillisecondsPerSecond;
        var startFrame = Math.Max(0, firstLoudFrame - marginFrames);
        var endFrame = Math.Min(frameCount, lastLoudFrame + 1 + marginFrames);
        var trimmedSamples = Samples.AsSpan(startFrame * bytesPerFrame, (endFrame - startFrame) * bytesPerFrame).ToArray();
        return this with { Samples = trimmedSamples };
    }

    /// <summary>Says whether any channel of one frame is louder than silence.</summary>
    /// <param name="frame">The frame: one sample for each channel.</param>
    /// <param name="bytesPerFrame">The bytes in one frame.</param>
    /// <param name="silenceLevel">The loudest a sample can be and still count as silence.</param>
    /// <returns>True when a sample in the frame is louder than <paramref name="silenceLevel" />.</returns>
    private bool IsLoud(int frame, int bytesPerFrame, int silenceLevel)
    {
        for (var offset = 0; offset < bytesPerFrame; offset += BytesPerSample)
        {
            var sample = BitConverter.ToInt16(Samples, frame * bytesPerFrame + offset);
            if (Math.Abs((int)sample) > silenceLevel) return true;
        }

        return false;
    }
}
