namespace Robotron2084.Audio.Models;

/// <summary>How the Berzerk robot's voice is set for a sentence: its pitch, which is also its speed, and its volume.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: the author's Berzerk disassembly, <c>berzerk.asm</c>. Each sentence starts with a control
///             byte for the speech board, such as <c>$7B</c> for "INTRUDER ALERT" (<c>INTRUDER_ALERT_INTRUDER_ALERT</c>,
///             <c>$2C4A</c>).
///         </item>
///         <item>
///             MAME <c>berzerk.cpp</c> <c>berzerk_state::audio_w</c> reads that byte: bits 0 to 2 are the pitch and bits 3
///             to 5 the volume (<see cref="BerzerkVoice" />).
///         </item>
///     </list>
/// </remarks>
/// <param name="Pitch">
///     The pitch, from <see cref="BerzerkVoice.MinimumPitch" /> (deepest and slowest) to
///     <see cref="BerzerkVoice.MaximumPitch" /> (highest and quickest).
/// </param>
/// <param name="Volume">The volume, from <see cref="BerzerkVoice.MinimumVolume" /> (silent) to <see cref="BerzerkVoice.MaximumVolume" />.</param>
public readonly record struct BerzerkVoiceSetting(int Pitch, int Volume)
{
    /// <summary>The bits of a control byte that hold the pitch.</summary>
    private const int PitchBits = 0x07;

    /// <summary>How far up a control byte the volume starts, in bits.</summary>
    private const int VolumeShift = 3;

    /// <summary>The bits that hold the volume, once moved down by <see cref="VolumeShift" />.</summary>
    private const int VolumeBits = 0x07;

    /// <summary>The setting the word samples were recorded at, which plays them as they are.</summary>
    public static BerzerkVoiceSetting Sample { get; } = new(BerzerkVoice.SamplePitch, BerzerkVoice.SampleVolume);

    /// <summary>Makes the setting a control byte for the speech board asks for.</summary>
    /// <param name="controlByte">The control byte, such as <c>$7B</c>.</param>
    /// <returns>The pitch from its bits 0 to 2, and the volume from its bits 3 to 5.</returns>
    public static BerzerkVoiceSetting CreateFromControlByte(int controlByte)
    {
        return new BerzerkVoiceSetting(controlByte & PitchBits, (controlByte >> VolumeShift) & VolumeBits);
    }
}
