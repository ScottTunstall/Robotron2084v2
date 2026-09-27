namespace Robotron2084.Tuning;

/// <summary>How the sound board is played through the computer's speakers. Port settings, not from the ROM.</summary>
public static class SoundTuning
{
    /// <summary>How loud the sound board plays, from 0 (silent) to 1 (as loud as the output allows).</summary>
    public const float MasterVolume = 0.5f;

    /// <summary>
    /// How far towards one speaker a sound from the playfield's edge is placed: 0 keeps every sound in the
    /// middle, 1 puts a sound from the very edge wholly in one speaker.
    /// </summary>
    public const float StereoWidth = 0.8f;
}
