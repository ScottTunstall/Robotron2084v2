namespace Robotron2084.Audio;

/// <summary>
///     Decides which sound the sound board plays. The board plays one sound at a time, so a new sound
///     only takes over when it is more important than the one playing; then it sends that sound's
///     numbers to the board, one by one, at the right moments.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRS22.ASM</c>, routines <c>SNDLDV</c> ("SOUND LOADER": the request),
///             <c>SNDSEQ</c> ("SOUND SEQUENCER": the per-vblank step) and <c>SNDOUT</c> (the send).
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>$D3C7</c> (request), <c>$D3E0</c> (step) and
///             <c>$D3B6</c> (send).
///         </item>
///     </list>
///     A request plays only when its priority is strictly higher than the one playing (<c>BLO SNDLDX</c>).
///     The step counts <c>SNDTMR</c> down each vblank; at zero it counts <c>SNDREP</c> down and sends the
///     same number again, and when both are spent it moves to the next line. A repeat count of zero ends
///     the table and frees the voice. Each sound also carries where it is heard between the speakers,
///     which is a port addition: the arcade's speaker is mono.
/// </remarks>
public sealed class SoundEngine
{
    private readonly IAudioSink _sink;
    private SoundEntry[] _entries = [];
    private int _entryIndex = -1;
    private int _holdTicksLeft;
    private float _pan;
    private int _repetitionsLeft;
    private int _ticksLeftInSend;

    /// <summary>Creates a sequencer that sends its sound numbers to a sink.</summary>
    /// <param name="sink">Where the sound numbers go.</param>
    public SoundEngine(IAudioSink sink)
    {
        _sink = sink;
    }

    /// <summary>True while the board is still playing the last number sent to it.</summary>
    public bool IsBoardPlaying => _sink.IsPlaying;

    /// <summary>The priority of the sound holding the voice, or 0 when the voice is free (<c>SNDPRI</c>).</summary>
    public int CurrentPriority { get; private set; }

    /// <summary>
    ///     Asks for a sound. It takes the voice only if it is more important than the one playing; its first
    ///     sound number goes to the board on the next <see cref="Tick" />.
    /// </summary>
    /// <param name="sequence">The sound's table.</param>
    /// <param name="pan">Where the sound is heard: -1 is wholly left, 0 the middle, 1 wholly right.</param>
    public void Play(SoundSequence sequence, float pan)
    {
        if (sequence.Priority <= CurrentPriority) return;

        _holdTicksLeft = 0;
        CurrentPriority = sequence.Priority;
        _entries = sequence.Entries;
        _pan = pan;
        _entryIndex = -1;
        _repetitionsLeft = 1;
        _ticksLeftInSend = 1;
    }

    /// <summary>
    ///     Keeps the voice for <paramref name="ticks" /> more port ticks. A table's end normally frees the
    ///     voice, but some sounds outlive their table on the BOARD — the wave-end music is a looping sound
    ///     the board plays on until another number arrives — so this stops a lower-priority sound cutting
    ///     one short (notes §128). A sound important enough to take the voice anyway cancels the hold.
    /// </summary>
    /// <param name="ticks">How many port ticks to keep the voice for.</param>
    public void HoldVoice(int ticks)
    {
        _holdTicksLeft = Math.Max(_holdTicksLeft, ticks);
    }

    /// <summary>
    ///     Sends a sound number straight to the board, skipping the priority check and the tables (the
    ///     original's <c>SDOUT</c> vector). For routines that time their own sounds, such as the transporter.
    /// </summary>
    /// <param name="soundNumber">The sound number (<c>SND#</c>).</param>
    /// <param name="pan">Where the sound is heard: -1 is wholly left, 0 the middle, 1 wholly right.</param>
    public void SendDirect(int soundNumber, float pan)
    {
        _sink.SendSoundNumber(soundNumber, pan);
    }

    /// <summary>One port tick (one vblank): counts the current send down, and sends the next sound number when it is due.</summary>
    public void Tick()
    {
        _sink.Tick();
        AdvanceVoiceHold();

        if (_entries.Length == 0) return;

        if (_ticksLeftInSend > 0 && --_ticksLeftInSend > 0) return;

        if (--_repetitionsLeft > 0)
        {
            // SNDREP is not reloaded here: only moving to the next line reloads it.
            Send(_entries[_entryIndex]);
            return;
        }

        MoveToNextEntry();
    }

    /// <summary>Moves to the table's next line and sends it, or frees the voice at the table's end.</summary>
    private void MoveToNextEntry()
    {
        _entryIndex++;
        if (_entryIndex >= _entries.Length)
        {
            EndSequence();
            return;
        }

        var entry = _entries[_entryIndex];
        _repetitionsLeft = entry.Repetitions;
        Send(entry);
    }

    /// <summary>Sends a line's sound number to the board and starts timing it.</summary>
    private void Send(SoundEntry entry)
    {
        _ticksLeftInSend = entry.LengthVblanks;
        _sink.SendSoundNumber(entry.SoundNumber, _pan);
    }

    /// <summary>Counts a held voice down, freeing it once the sound has played itself out.</summary>
    private void AdvanceVoiceHold()
    {
        if (_holdTicksLeft > 0 && --_holdTicksLeft == 0) CurrentPriority = 0;
    }

    /// <summary>Frees the voice: the table has ended (<c>STA SNDPRI</c> with the zero repeat count).</summary>
    private void EndSequence()
    {
        if (_holdTicksLeft == 0) CurrentPriority = 0;

        _entries = [];
        _entryIndex = -1;
    }
}
