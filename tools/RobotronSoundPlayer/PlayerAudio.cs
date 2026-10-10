namespace RobotronSoundPlayer;

/// <summary>The player's sound format: the same rate and port tick the game plays at.</summary>
internal static class PlayerAudio
{
    /// <summary>Samples a second.</summary>
    public const int SampleRate = 44_100;

    /// <summary>The game's fixed update rate: port ticks a second.</summary>
    public const int PortTicksPerSecond = 60;

    /// <summary>Samples in one port tick.</summary>
    public const int SamplesPerPortTick = SampleRate / PortTicksPerSecond;

    /// <summary>The largest value a 16-bit sample can hold.</summary>
    private const float FullScale16Bit = short.MaxValue;

    /// <summary>Turns samples from -1 to 1 into 16-bit little-endian bytes, scaled by a volume.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="volume">How loud, from 0 to 1.</param>
    /// <returns>Two bytes per sample.</returns>
    public static byte[] ToPcm16(ReadOnlySpan<float> samples, float volume)
    {
        var pcm = new byte[samples.Length * sizeof(short)];
        for (var i = 0; i < samples.Length; i++)
        {
            var scaled = Math.Clamp(samples[i] * volume, -1f, 1f) * FullScale16Bit;
            var value = (short)MathF.Round(scaled);
            pcm[i * sizeof(short)] = (byte)value;
            pcm[i * sizeof(short) + 1] = (byte)(value >> 8);
        }

        return pcm;
    }
}
