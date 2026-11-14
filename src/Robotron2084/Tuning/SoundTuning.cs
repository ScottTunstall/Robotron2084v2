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
    ///     How loud the speech plays, from 0 (silent) to 1 (as loud as the output allows). It is separate from the
    ///     sound board's volume.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const float SpeechVolume = 0.8f;

    /// <summary>
    ///     The loudest a Berzerk word's sample can be and still count as the silence around the word, from 0 to 32767.
    /// </summary>
    /// <remarks>
    ///     Port only. Every word sample has about 60 milliseconds of silence before the word and 70 to 105 after it, so two
    ///     words played one after the other had a gap of about 150 milliseconds. That silence is exact zeros, so a low level
    ///     cuts it without touching the word.
    /// </remarks>
    public const int BerzerkWordSilenceLevel = 64;

    /// <summary>How much of the silence is kept at each end of a Berzerk word, in milliseconds.</summary>
    /// <remarks>Port only. A few milliseconds, so each word still starts and stops on silence.</remarks>
    public const int BerzerkWordSilenceMarginMilliseconds = 5;

    /// <summary>
    ///     How far towards one speaker a sound from the playfield's edge is placed: 0 keeps every sound in the
    ///     middle, 1 puts a sound from the very edge wholly in one speaker.
    /// </summary>
    public const float StereoWidth = 0.8f;

    /// <summary>
    ///     How long the voice is held for the wave-end music, in
    ///     <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">port ticks</see> from the moment the music is asked for.
    /// </summary>
    /// <remarks>
    ///     Notes §128. The board loops sound <c>$0E</c>. Its phrase lasts 183 ticks, and the ROM's table asks for it 29
    ///     times, 4 ticks apart, so the last ask lands on tick 113 and the phrase that follows it plays out to about tick
    ///     296. Tick 113 is the shortest a hold can usefully be and the play-out is the longest. The value is tuned by ear
    ///     and sits a little under the full play-out, which was too long.
    /// </remarks>
    public const int WaveEndMusicTicks = 236;
}
