using Robotron2084.Audio.Models;

namespace Robotron2084.Audio;

/// <summary>
///     The Berzerk robot's voice: the pitch and the volume the game asks its speech board for, each from 0 to 7
///     (<see cref="BerzerkVoiceSetting" />).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: MAME <c>berzerk.cpp</c>, <c>berzerk_state::audio_w</c>. The game writes the pitch to its
///             speech board, which divides the speech chip's clock by 16 less the pitch. The chip makes one sample a clock,
///             so the pitch changes how fast the same samples play: a higher pitch is higher and quicker. The volume sets
///             the board's loudness to the volume's sevenths: 0 is silent and 7 is full.
///         </item>
///         <item>
///             The word samples in <c>Content/Speech/Berzerk/</c> were recorded at <see cref="SamplePitch" /> and
///             <see cref="SampleVolume" />. Another pitch plays them faster or slower by the ratio of the two clocks, so
///             nothing is lost or made up, and a lower volume plays them more quietly.
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

    /// <summary>The lowest volume: silent.</summary>
    public const int MinimumVolume = 0;

    /// <summary>The highest volume: the board as loud as it goes.</summary>
    public const int MaximumVolume = 7;

    /// <summary>
    ///     The volume the word samples are taken to have been recorded at: the loudest. It is the game's volume for
    ///     "INTRUDER ALERT", which is said at <see cref="SamplePitch" />.
    /// </summary>
    /// <remarks>Original source: the author's Berzerk disassembly, <c>berzerk.asm</c> <c>INTRUDER_ALERT_INTRUDER_ALERT</c>, whose control byte is <c>$7B</c>. Disassembly: <c>$2C4A</c>.</remarks>
    public const int SampleVolume = 7;

    /// <summary>
    ///     How long a <see cref="BerzerkWord.Pause" /> lasts, in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>:
    ///     a sixth of a second. A port addition: the chip has no pause. Tuned by ear: a quarter of a second was slightly too
    ///     long between the two halves of "INTRUDER ALERT INTRUDER ALERT".
    /// </summary>
    public const int PauseTicks = 10;

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

    /// <summary>Gets how loud to play the word samples at a volume, as a share of how loud they were recorded.</summary>
    /// <param name="volume">The volume, from <see cref="MinimumVolume" /> to <see cref="MaximumVolume" />.</param>
    /// <returns>The share, from 0 (silent) to 1 at <see cref="SampleVolume" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The volume is not from 0 to 7.</exception>
    public static float GetVolumeShare(int volume)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(volume, MinimumVolume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, MaximumVolume);

        return (float)volume / SampleVolume;
    }
}
