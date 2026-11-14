namespace Robotron2084.Audio;

/// <summary>The Berzerk robot's voice setting: the pitch the game asks its speech board for, from 0 to 7.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: MAME <c>berzerk.cpp</c>, <c>berzerk_state::audio_w</c>. The game writes the pitch to its
///             speech board, which divides the speech chip's clock by 16 less the pitch. The chip makes one sample a clock,
///             so the pitch changes how fast the same samples play: a higher pitch is higher and quicker.
///         </item>
///         <item>
///             The word samples in <c>Content/Speech/Berzerk/</c> were recorded at <see cref="SamplePitch" />. Another pitch
///             plays them faster or slower by the ratio of the two clocks, so nothing is lost or made up.
///         </item>
///     </list>
/// </remarks>
public static class BerzerkVoice
{
    /// <summary>The lowest pitch: the slowest and deepest voice.</summary>
    public const int MinimumPitch = 0;

    /// <summary>The highest pitch: the quickest and highest voice.</summary>
    public const int MaximumPitch = 7;

    /// <summary>The pitch the word samples were recorded at. It is the game's pitch for "INTRUDER ALERT".</summary>
    public const int SamplePitch = 3;

    /// <summary>
    ///     How long a <see cref="BerzerkWord.Pause" /> lasts, in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>:
    ///     a quarter of a second. A port addition: the chip has no pause.
    /// </summary>
    public const int PauseTicks = 15;

    /// <summary>Gets the name of a word's speech line: its file name in <c>Content/Speech/Berzerk/</c>, in capitals.</summary>
    /// <param name="berzerkWord">The word. It must not be <see cref="BerzerkWord.Pause" />, which has no line.</param>
    /// <exception cref="ArgumentOutOfRangeException">The word is <see cref="BerzerkWord.Pause" />.</exception>
    public static string GetLineName(BerzerkWord berzerkWord)
    {
        if (berzerkWord == BerzerkWord.Pause) throw new ArgumentOutOfRangeException(nameof(berzerkWord), berzerkWord, "A pause has no speech line.");

        return berzerkWord.ToString().ToUpperInvariant();
    }

    /// <summary>The speech board divides the chip's clock by this number less the pitch.</summary>
    private const int PitchDividerBase = 16;

    /// <summary>Gets how far to raise or lower the word samples to play them at a pitch.</summary>
    /// <param name="pitch">The pitch, from <see cref="MinimumPitch" /> to <see cref="MaximumPitch" />.</param>
    /// <returns>The change in octaves: 0 at <see cref="SamplePitch" />, less than 0 lower, more than 0 higher.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pitch is not from 0 to 7.</exception>
    public static float GetPitchOctaves(int pitch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pitch, MinimumPitch);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pitch, MaximumPitch);

        double sampleDivider = PitchDividerBase - SamplePitch;
        double divider = PitchDividerBase - pitch;
        return (float)Math.Log2(sampleDivider / divider);
    }
}
