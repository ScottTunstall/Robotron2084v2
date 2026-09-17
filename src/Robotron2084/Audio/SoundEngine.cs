namespace Robotron2084.Audio;

/// <summary>
/// One entry of a ROM sound table (notes §36.2, R5 $D3E0): play
/// <see cref="Note"/> <see cref="Dur"/> times, each repetition sounding for
/// <see cref="Len"/> vblanks. A table ends at the first entry whose
/// <see cref="Dur"/> is 0 (the port passes pre-trimmed tables).
/// </summary>
public readonly record struct SoundEntry(byte Dur, byte Len, byte Note);

/// <summary>
/// A decoded ROM sound: the request <see cref="Priority"/> (the byte the ROM
/// call sites point at) and the (dur, len, note) entries that follow it.
/// <see cref="RomTableAddress"/> is the table address in
/// <c>ref/rom/robotron64k.bin</c>, for provenance.
/// </summary>
public sealed record SoundSequence(int Priority, int RomTableAddress, SoundEntry[] Entries);

/// <summary>
/// Audio backend for <see cref="SoundEngine"/>. The port calls
/// <see cref="PlayNote"/> when the sequencer (re)sounds a note; the sink
/// holds the tone for <paramref name="ticks"/> port ticks.
/// <see cref="Tick"/> retires expired tones (called once per port tick).
/// </summary>
public interface IAudioSink
{
    void PlayNote(int note, int ticks);
    void Tick();
}

/// <summary>
/// Single-voice priority sound sequencer, ported from the R5 disasm
/// (notes §36.2): request $D3C7 (play only when the requested priority is
/// STRICTLY higher than the one currently sounding — the ROM's
/// <c>CMPA $56 / BCS</c>), vblank step $D3E0 ($57 = ticks left in the
/// current note repetition; $58 = repetitions left in the current entry;
/// at 0/0 the pointer advances 3 bytes to the next entry; dur 0 ends the
/// table and clears the priority), hardware $D3B6 (note written to PIA port
/// B, complemented — the note number selects the tone on the sound board).
/// Pure logic: the tone itself is the sink's business.
/// </summary>
public sealed class SoundEngine
{
    private readonly IAudioSink _sink;
    private SoundEntry[] _entries = [];
    private int _entryIndex = -1;
    private int _repetitionsLeft;   // ROM $58
    private int _ticksLeftInNote;   // ROM $57
    private int _priority;          // ROM $56

    public SoundEngine(IAudioSink sink) => _sink = sink;

    /// <summary>The priority currently holding the voice (0 = free).</summary>
    public int CurrentPriority => _priority;

    /// <summary>
    /// Request a sound. Preempts only when <paramref name="sequence.Priority"/>
    /// is strictly higher than the current one (ROM $D3CB-$D3D1). The first
    /// note sounds on the next <see cref="Tick"/> (ROM: the request primes
    /// $57=$58=1, so the first vblank advances straight to the first entry).
    /// </summary>
    public void Play(SoundSequence sequence)
    {
        if (sequence.Priority <= _priority)
        {
            return; // ROM $D3CD-DCFE: not strictly higher — ignore.
        }

        _priority = sequence.Priority;
        _entries = sequence.Entries;
        _entryIndex = -1;
        _repetitionsLeft = 1; // ROM $D3D9 LDD #$0101
        _ticksLeftInNote = 1;
    }

    /// <summary>
    /// One port tick (≈ one arcade vblank). Advances the sequencer per
    /// $D3E0: while the current note repetition runs, only the tick counter
    /// decrements; at 0 the repetition counter decrements and the note is
    /// re-sounded; at 0/0 the next entry loads (dur 0 = table end → voice
    /// freed, priority cleared).
    /// </summary>
    public void Tick()
    {
        _sink.Tick();

        if (_entries.Length == 0)
        {
            return;
        }

        if (_ticksLeftInNote > 0)
        {
            _ticksLeftInNote--;
            if (_ticksLeftInNote > 0)
            {
                return; // note still sounding (held on the sound board).
            }
        }

        // _ticksLeftInNote == 0 here (ROM falls through at $D3E6).
        _repetitionsLeft--;
        if (_repetitionsLeft > 0)
        {
            // ROM $D3EC BNE $D3FC: the repetition counter was JUST decremented
            // and is non-zero — re-sound the SAME note (len re-loaded from the
            // current entry at $D3FC, but $58 is NOT re-loaded).
            SoundEntry resound = _entries[_entryIndex];
            _ticksLeftInNote = resound.Len;
            _sink.PlayNote(resound.Note, resound.Len);
            return;
        }

        // Entry exhausted (ROM $D3EE-$D3FA): advance and load the next one
        // ($58 = the new entry's dur, then $D3FC sounds it).
        _entryIndex++;
        if (_entryIndex >= _entries.Length)
        {
            EndSequence();
            return;
        }

        SoundEntry entry = _entries[_entryIndex];
        _repetitionsLeft = entry.Dur;
        _ticksLeftInNote = entry.Len;
        _sink.PlayNote(entry.Note, entry.Len);
    }

    private void EndSequence()
    {
        // ROM $D3F6: the (0) priority is written back — the voice is free.
        _priority = 0;
        _entries = [];
        _entryIndex = -1;
    }
}
