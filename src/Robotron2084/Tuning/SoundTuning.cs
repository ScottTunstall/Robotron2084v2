namespace Robotron2084.Tuning;

/// <summary>
///     How the sound board is played through the computer's speakers: the port's own settings, plus the one property
///     of the board's ROM that has to be measured.
/// </summary>
public static class SoundTuning
{
    /// <summary>How loud the sound board plays, from 0 (silent) to 1 (as loud as the output allows).</summary>
    public const float MasterVolume = 0.5f;

    /// <summary>
    ///     How far towards one speaker a sound from the playfield's edge is placed: 0 keeps every sound in the
    ///     middle, 1 puts a sound from the very edge wholly in one speaker.
    /// </summary>
    public const float StereoWidth = 0.8f;

    /// <summary>
    ///     How long the voice is held for the wave-end music, in port ticks from the moment it is asked for
    ///     (notes §128). The board LOOPS sound $0E: its phrase is 183 ticks and the ROM's table asks for it
    ///     29 times at 4 ticks apart, so the last ask lands at tick 113 and the phrase that follows it plays
    ///     out to about 296. That is the longest a hold can usefully be and tick 113 is the shortest, and the
    ///     author dialled the value in by ear — the full play-out was *"a little too long"*, so it is a second
    ///     under it (notes §128).
    /// </summary>
    public const int WaveEndMusicTicks = 236;
}
