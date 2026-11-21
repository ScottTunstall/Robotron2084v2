namespace Robotron2084.Tuning;

/// <summary>
///     How the sound board is played through the computer's speakers: the port's own settings, plus the one property
///     of the board's ROM that has to be measured.
/// </summary>
public static class SoundTuning
{
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
    ///     How far the sound board's share of its volume moves in one tick, when the speech starts or stops talking.
    /// </summary>
    /// <remarks>Port only. The whole change, from 1 to <see cref="BoardVolumeWhileSpeaking" />, takes four ticks.</remarks>
    public const float BoardVolumeStepPerTick = 0.15f;

    /// <summary>
    ///     The share of <see cref="MasterVolume" /> the sound board plays at while the speech is talking, from 0 (silent)
    ///     to 1 (no quieter).
    /// </summary>
    /// <remarks>
    ///     Port only. Measured: the player's laser is about 0.35 loud (the square root of the mean of the squared samples)
    ///     at <see cref="MasterVolume" />, and the Berzerk words about 0.17 at a <see cref="SpeechVolume" /> of 1. At this
    ///     share the laser drops to about 0.14, below the words.
    /// </remarks>
    public const float BoardVolumeWhileSpeaking = 0.4f;

    /// <summary>How loud the sound board plays, from 0 (silent) to 1 (as loud as the output allows).</summary>
    public const float MasterVolume = 0.5f;

    /// <summary>
    ///     How loud the speech plays, from 0 (silent) to 1 (as loud as the output allows). It is separate from the
    ///     sound board's volume.
    /// </summary>
    /// <remarks>Port only. As loud as it can be: even so, the speech is quieter than the board's loudest sounds.</remarks>
    public const float SpeechVolume = 1f;

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
