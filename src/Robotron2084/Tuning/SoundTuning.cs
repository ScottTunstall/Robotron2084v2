namespace Robotron2084.Tuning;

/// <summary>How the sound board is played through the computer's speakers: the port's own settings, plus the one property of the board's ROM that has to be measured.</summary>
public static class SoundTuning
{
    /// <summary>How loud the sound board plays, from 0 (silent) to 1 (as loud as the output allows).</summary>
    public const float MasterVolume = 0.5f;

    /// <summary>
    /// How far towards one speaker a sound from the playfield's edge is placed: 0 keeps every sound in the
    /// middle, 1 puts a sound from the very edge wholly in one speaker.
    /// </summary>
    public const float StereoWidth = 0.8f;

    /// <summary>
    /// How long the wave-end music sounds, in port ticks, measured from the moment it is asked for
    /// (notes §128). The board LOOPS sound $0E every 183 ticks and the ROM's table asks for it 29 times
    /// at 4 ticks apart, so the last ask lands at tick 113 and the phrase that follows it plays to about
    /// tick 296 — measured through the emulated board rather than read from a comment, because the
    /// board's own ROM holds the routine.
    /// </summary>
    public const int WaveEndMusicTicks = 296;
}
