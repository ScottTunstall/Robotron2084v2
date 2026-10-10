using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The board's single oscillator: a square wave whose pitch and size step on at set intervals, played once or
///     over and over, each time a little quieter.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c>, routines <c>MOVE</c> ("MOVE PARAMETERS"), <c>SING</c> ("DELTA F,
///             DELTA A ROUTINE") and <c>ECHO</c>, and the callers <c>ATARI</c>, <c>SIREN</c>, <c>ORRRR</c>, <c>HSTD</c>,
///             <c>PERK$$</c> and <c>SQRT</c> ("SINGLE OSCILLATOR SOUND CALLS" and "RANDOM SQUIRTS"), with the settings
///             <c>VEC02X</c> to <c>VEC09X</c>.
///         </item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
///     Robotron's game never asks for these; only the sound test page does.
/// </remarks>
internal sealed class SingSound
{
    /// <summary>The loudness every sound starts at (<c>IRQ</c>: <c>LDAB #$AF</c>, <c>STAB AMP0</c>).</summary>
    private const byte StartLoudness = 0xAF;

    /// <summary>The loudness <c>SIREN</c>, <c>HSTD</c> and <c>PERK$$</c> start at.</summary>
    private const byte FullLoudness = 0xFF;

    /// <summary>What <c>ECHO</c> takes off the loudness each time (<c>SUBA #$08</c>).</summary>
    private const byte EchoStep = 0x08;

    /// <summary>The bit that makes a byte negative to the processor.</summary>
    private const byte SignBit = 0x80;

    /// <summary>How much the square wave's pitch count is cut by to rejoin the delay loop (<c>SUBB #$05</c>).</summary>
    private const byte RejoinAdjustment = 5;

    /// <summary>How many times <c>SIREN</c> plays its two settings (<c>LDAB #$FF</c>).</summary>
    private const byte SirenRounds = 0xFF;

    /// <summary>How many squirts <c>SQRT</c> makes (<c>LDAB #$30</c>).</summary>
    private const byte SquirtCount = 0x30;

    /// <summary>
    ///     The squirt's random pitch: the random number is shifted twice and <c>$0C</c> is added (<c>LSRA</c> twice,
    ///     <c>ADDA #$0C</c>).
    /// </summary>
    private const int SquirtShift = 2;

    private const byte SquirtBase = 0x0C;

    /// <summary>What the random number generator adds (<c>ADDA #$0B</c>).</summary>
    private const byte RandomAdd = 0x0B;

    /// <summary><c>MOVE</c>: six <c>LDAA</c>/<c>STAA</c> pairs and <c>RTS</c>.</summary>
    private const int MoveCycles = 6 * (Indexed + StoreDirect) + Return;

    /// <summary><c>SING</c> starting: <c>LDAA AMP0</c>, <c>PSHB</c>, <c>LDAB</c>, <c>STAB</c>, <c>LDAB</c>, <c>STAB</c>.</summary>
    private const int SingStartCycles = Direct + Stack + Direct + StoreDirect + Direct + StoreDirect;

    /// <summary><c>SING1</c> before the first write: <c>COMA</c>, <c>LDAB FREQ$</c>; the write is <c>STAA SOUND</c>.</summary>
    private const int FirstHalfCycles = Inherent + Direct;

    /// <summary>One count of a delay loop (<c>SING2</c>, <c>SING3</c>: <c>DECB</c>, <c>BNE</c>).</summary>
    private const int DelayCountCycles = Inherent + Branch;

    /// <summary>
    ///     Before the second write: <c>COMA</c>, <c>LDAB FREQ$</c>, <c>BRA *+2</c> and two <c>INX</c>/<c>DEX</c> pairs
    ///     ("SYNC, 20 CYCLES").
    /// </summary>
    private const int SecondHalfCycles = Inherent + Direct + Branch + 4 * IndexStep;

    /// <summary>After the second delay: <c>DEC C$FRQ$</c>, <c>BEQ</c>.</summary>
    private const int CountPitchCycles = ModifyExtended + Branch;

    /// <summary>Then <c>DEC C$AMP$</c>, <c>BNE</c>.</summary>
    private const int CountLoudnessCycles = ModifyExtended + Branch;

    /// <summary>
    ///     The loudness steps on: before the write <c>COMA</c>, <c>LDAB C$AMP</c>; after it <c>STAB</c>, <c>LDAB</c>,
    ///     <c>ADDA</c>, <c>BMI</c>; then <c>NOP</c>, <c>BRA</c>.
    /// </summary>
    private const int LoudnessBeforeWriteCycles = Inherent + Direct;

    private const int LoudnessAfterWriteCycles = StoreDirect + Direct + Direct + Branch + Inherent + Branch;

    /// <summary>
    ///     The pitch steps on: <c>INX</c>, <c>DEX</c>, <c>NOP</c>, <c>COMA</c>, <c>LDAB C$FRQ</c> before the write;
    ///     <c>STAB</c>, <c>LDAB</c>, <c>SUBB</c>, <c>CMPB</c> twice, <c>BEQ</c> after.
    /// </summary>
    private const int PitchBeforeWriteCycles = IndexStep + IndexStep + Inherent + Inherent + Direct;

    private const int PitchAfterWriteCycles = StoreDirect + Direct + Direct + Direct + Direct + Branch;

    /// <summary><c>SING5</c>: <c>STAB FREQ$</c>, <c>SUBB #$05</c>, <c>BRA SING2</c>.</summary>
    private const int RejoinCycles = StoreDirect + Immediate + Branch;

    /// <summary><c>SING6</c>: <c>PULB</c>, <c>RTS</c>.</summary>
    private const int EndSingCycles = Stack + Return;

    /// <summary><c>ECHO</c>: <c>BSR</c>, <c>LDAA</c>, <c>SUBA</c>, <c>BPL</c>.</summary>
    private const int EchoCycles = CallShort + Direct + Immediate + Branch;

    /// <summary><c>ECHO</c> when the loudness is not yet spent: <c>STAA AMP0</c>, <c>RTS</c>; then <c>BRA PERK$1</c>.</summary>
    private const int EchoAgainCycles = StoreDirect + Return + Branch;

    /// <summary>
    ///     The settings, six bytes each (<c>VECnnX</c>): pitch, cycles at a pitch, pitch change, last pitch, cycles at a
    ///     loudness, loudness change.
    /// </summary>
    private static readonly byte[] Atari = [0x01, 0x03, 0xFF, 0x80, 0xFF, 0x00];

    private static readonly byte[] SirenLow = [0x20, 0x03, 0xFF, 0x50, 0xFF, 0x00];

    private static readonly byte[] SirenHigh = [0x50, 0x03, 0x01, 0x20, 0xFF, 0x00];

    private static readonly byte[] Orrrr = [0xFE, 0x04, 0x02, 0x04, 0xFF, 0x00];

    private static readonly byte[] PerkDollars = [0x48, 0x03, 0x01, 0x0C, 0xFF, 0x00];

    private static readonly byte[] Hstd = [0xE0, 0x01, 0x02, 0x10, 0xFF, 0x00];

    private static readonly byte[] Squirt = [0x50, 0xFF, 0x00, 0x00, 0x60, 0x80];

    private readonly BoardMemory _memory;
    private readonly BoardOutput _output;
    private byte _cyclesAtLoudness;
    private byte _cyclesAtPitch;
    private byte _delay;
    private bool _hasEnded;
    private byte _lastPitch;
    private byte _level;
    private byte _loudness = StartLoudness;
    private byte _loudnessChange;
    private byte _loudnessCyclesLeft;
    private byte _nextPitch;
    private byte _pitch;
    private byte _pitchChange;
    private byte _pitchCyclesLeft;

    /// <summary>Creates the sound on the board's memory and output port.</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    private SingSound(BoardMemory memory, BoardOutput output)
    {
        _memory = memory;
        _output = output;
    }

    /// <summary>Plays <c>ATARI</c>, once (<c>VEC02X</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayAtari(BoardMemory memory, BoardOutput output)
    {
        return new SingSound(memory, output).PlayOnceFrom(Atari);
    }

    /// <summary>Plays <c>ORRRR</c>, once (<c>VEC05X</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayOrrrr(BoardMemory memory, BoardOutput output)
    {
        return new SingSound(memory, output).PlayOnceFrom(Orrrr);
    }

    /// <summary>Plays <c>SIREN</c>: two settings, one after the other, 255 times (<c>VEC03X</c>, <c>VEC04X</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlaySiren(BoardMemory memory, BoardOutput output)
    {
        return new SingSound(memory, output).PlaySiren();
    }

    /// <summary>Plays <c>HSTD</c>, over and over, each time quieter (<c>VEC08X</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayHstd(BoardMemory memory, BoardOutput output)
    {
        return new SingSound(memory, output).PlayEchoing(Hstd, FullLoudness, CyclesBeforeEcho(true));
    }

    /// <summary>Plays <c>PERK$$</c>, over and over, each time quieter (<c>VEC06X</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayPerkDollars(BoardMemory memory, BoardOutput output)
    {
        return new SingSound(memory, output).PlayEchoing(PerkDollars, FullLoudness, CyclesBeforeEcho(true));
    }

    /// <summary>Plays <c>SQRT</c>: 48 short squirts at random pitches (<c>VEC09X</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlaySquirts(BoardMemory memory, BoardOutput output)
    {
        return new SingSound(memory, output).PlaySquirts();
    }

    /// <summary>
    ///     The set-up before the echoing loop: <c>LDAA</c>, <c>STAA AMP0</c> (<c>HSTD</c>, <c>PERK$$</c>), then
    ///     <c>LDX</c> and <c>BRA</c>.
    /// </summary>
    /// <param name="setsLoudness">True when the routine starts by loading the full loudness.</param>
    /// <returns>The cycles.</returns>
    private static int CyclesBeforeEcho(bool setsLoudness)
    {
        return (setsLoudness ? Immediate + StoreDirect : 0) + WordImmediate + Branch;
    }

    private IEnumerable<OutputChange> PlayOnceFrom(byte[] settings)
    {
        _output.Wait(WordImmediate + Branch + CallExtended);
        Move(settings);
        _output.Wait(CallExtended);
        return Sing();
    }

    private IEnumerable<OutputChange> PlayEchoing(byte[] settings, byte loudness, int setUpCycles)
    {
        _output.Wait(setUpCycles);
        _loudness = loudness;
        while (true)
        {
            _output.Wait(CallShort + CallExtended);
            Move(settings);
            _output.Wait(CallExtended);
            foreach (var change in Sing()) yield return change;

            _output.Wait(Return + EchoCycles);
            if ((byte)(_loudness - EchoStep) < SignBit)
            {
                _output.Wait(Stack + Stack + Return);
                yield break;
            }

            _output.Wait(EchoAgainCycles);
            _loudness -= EchoStep;
        }
    }

    private IEnumerable<OutputChange> PlaySiren()
    {
        _output.Wait(Immediate + StoreDirect);
        _loudness = FullLoudness;
        for (var round = 0; round < SirenRounds; round++)
        {
            foreach (var settings in new[] { SirenLow, SirenHigh })
            {
                _output.Wait(WordImmediate + CallShort + CallExtended);
                Move(settings);
                _output.Wait(CallExtended);
                foreach (var change in Sing()) yield return change;

                _output.Wait(Return);
            }

            _output.Wait(Inherent + Branch);
        }
    }

    private IEnumerable<OutputChange> PlaySquirts()
    {
        _output.Wait(Immediate + WordImmediate + CallShort);
        Move(Squirt);
        for (var squirt = 0; squirt < SquirtCount; squirt++)
        {
            var low = _memory.RandomLow;
            _output.Wait(Direct + Inherent + Direct + Immediate + StoreDirect + SquirtShift * Inherent + Immediate +
                         StoreDirect + CallShort);
            low = (byte)((low << 1) + low + RandomAdd);
            _memory.SetRandom(_memory.RandomHigh, low);
            _pitch = (byte)((low >> SquirtShift) + SquirtBase);
            foreach (var change in Sing()) yield return change;

            _output.Wait(Inherent + Branch);
        }

        _output.Wait(Return);
    }

    /// <summary>Copies a setting into the working variables (<c>MOVE</c>).</summary>
    /// <param name="settings">The six bytes.</param>
    private void Move(byte[] settings)
    {
        _output.Wait(MoveCycles);
        _pitch = settings[0];
        _cyclesAtPitch = settings[1];
        _pitchChange = settings[2];
        _lastPitch = settings[3];
        _cyclesAtLoudness = settings[4];
        _loudnessChange = settings[5];
    }


    /// <summary>
    ///     Plays the working settings: a square wave whose level flips every <see cref="_pitch" /> counts, the pitch
    ///     stepping on every <see cref="_cyclesAtPitch" /> cycles and the loudness every <see cref="_cyclesAtLoudness" />
    ///     (<c>SING</c>).
    /// </summary>
    /// <returns>The sound's output changes.</returns>
    private IEnumerable<OutputChange> Sing()
    {
        _output.Wait(SingStartCycles);
        _loudnessCyclesLeft = _cyclesAtLoudness;
        _hasEnded = false;
        _pitchCyclesLeft = _cyclesAtPitch;
        _level = _loudness;
        var isStartOfCycle = true;
        _delay = 0;
        while (true)
        {
            if (isStartOfCycle)
            {
                _level = (byte)~_level;
                _output.Wait(FirstHalfCycles);
                yield return _output.Store(_level);
                _delay = _pitch;
            }

            _output.Wait(CountdownLoop.Runs(_delay) * DelayCountCycles);
            _level = (byte)~_level;
            _output.Wait(SecondHalfCycles);
            yield return _output.Store(_level);
            _output.Wait(CountdownLoop.Runs(_pitch) * DelayCountCycles);

            _output.Wait(CountPitchCycles);
            _pitchCyclesLeft--;
            if (_pitchCyclesLeft == 0)
            {
                foreach (var change in StepPitch()) yield return change;
            }
            else
            {
                _output.Wait(CountLoudnessCycles);
                _loudnessCyclesLeft--;
                isStartOfCycle = _loudnessCyclesLeft != 0;
                if (isStartOfCycle) continue;

                foreach (var change in StepLoudness()) yield return change;
            }

            if (_hasEnded)
            {
                _output.Wait(EndSingCycles);
                yield break;
            }

            _output.Wait(RejoinCycles);
            _delay = (byte)(_nextPitch - RejoinAdjustment);
            _pitch = _nextPitch;
        }
    }

    /// <summary>
    ///     The loudness steps on (<c>SING</c>, after <c>DEC C$AMP$</c> reaches zero): the level flips and the loudness
    ///     changes.
    /// </summary>
    /// <returns>The write.</returns>
    private IEnumerable<OutputChange> StepLoudness()
    {
        _level = (byte)~_level;
        _output.Wait(LoudnessBeforeWriteCycles);
        yield return _output.Store(_level);
        _loudnessCyclesLeft = _cyclesAtLoudness;
        _nextPitch = _pitch;
        _output.Wait(LoudnessAfterWriteCycles);
        _level += _loudnessChange;
        _hasEnded = _level >= SignBit;
    }

    /// <summary>The pitch steps on (<c>SING4</c>): the level flips and the pitch moves on, or the sound ends.</summary>
    /// <returns>The write.</returns>
    private IEnumerable<OutputChange> StepPitch()
    {
        _level = (byte)~_level;
        _output.Wait(PitchBeforeWriteCycles);
        yield return _output.Store(_level);
        _pitchCyclesLeft = _cyclesAtPitch;
        _nextPitch = (byte)(_pitch - _pitchChange);
        _output.Wait(PitchAfterWriteCycles);
        _hasEnded = _nextPitch == _lastPitch;
    }
}
