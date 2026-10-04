using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// The arcade's sound board, rebuilt from its program's source: it waits for a sound number, then plays
/// that sound by changing its output level at the same moments the real board does.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c> ("ROBOTRON SOUNDS VERSION 1.0 3-8-82"), routine <c>IRQ</c>
/// ("INTERRUPT PROCESSING") and the sound routines it calls.</item>
/// <item>Disassembly: none in this repo; ROM <c>$FB11</c> (<c>IRQ</c>, the sound ROM's interrupt vector).</item>
/// </list>
/// The board runs each sound as a program timed by its own instructions. The port times each sound with
/// the same instruction costs (<see cref="InstructionCycles"/>), so pitches and lengths match the arcade's.
/// Only the sound numbers the game sends are built; the notes (§130) explain how they were checked.
/// </remarks>
public sealed class SoundBoard : ISoundBoard
{
    /// <summary>
    /// The board's processor clock in cycles a second: its 3.579545 MHz crystal divided by four inside
    /// the chip (MAME's Williams driver).
    /// </summary>
    public const int ClockHertz = 894_886;

    /// <summary>The six sound lines (<c>RRF.ASM</c>: "B0-B5 SOUND"): a sound number is 0 to 63.</summary>
    private const int SoundLines = 0x3F;

    /// <summary>No sound number is waiting to be answered. It is the starting value of <see cref="_waitingSoundNumber"/>, which means that no sound is waiting.</summary>
    private const int NoSoundNumber = -1;

    /// <summary>The spinner sound, which keeps its send count (<c>SP1SND</c>).</summary>
    private const int SpinnerSoundNumber = 0x0E;

    /// <summary>The laser ball bonus, which keeps its place (<c>B2SND</c>).</summary>
    private const int LaserBallBonusSoundNumber = 0x12;

    /// <summary>What the handler takes off a low sound number to find its wave table settings (<c>DECA</c>).</summary>
    private const int LowWaveTableOffset = 0x01;

    /// <summary>What the handler takes off a high sound number to find its wave table settings (<c>DECA</c>, <c>SUBA #$10</c>).</summary>
    private const int HighWaveTableOffset = 0x11;

    /// <summary>What the handler takes off a square wave sound number to find its settings (<c>DECA</c>, <c>SUBA #$1C</c>).</summary>
    private const int SquareWaveOffset = 0x1D;

    /// <summary>
    /// <c>IRQ</c> up to the flag checks: <c>LDS</c>, <c>LDAA SOUND+2</c>, <c>LDX</c>, <c>STX XDECAY</c>, <c>LDX</c>,
    /// <c>STX XPTR</c>, <c>LDAB</c>, <c>STAB AMP0</c>, <c>CLI</c>, <c>COMA</c>, <c>ANDA</c>, <c>LDAB ORGFLG</c>, <c>BEQ</c>.
    /// </summary>
    private const int HandlerStartCycles =
        WordImmediate + Extended + WordImmediate + WordStoreDirect + WordImmediate + WordStoreDirect + Immediate + StoreDirect
        + Inherent + Inherent + Immediate + Direct + Branch;

    /// <summary><c>IRQ00</c>/<c>IRQ00A</c> checking both flags: <c>CLRB</c>, then <c>CMPA</c>, <c>BEQ</c> twice.</summary>
    private const int FlagCheckCycles = Inherent + (2 * (Immediate + Branch));

    /// <summary><c>IRQ000</c> sorting the number: <c>TSTA</c>, <c>BEQ</c>, <c>DECA</c>, <c>CMPA #$1F</c>, <c>BLT</c>.</summary>
    private const int SortCycles = Inherent + Branch + Inherent + Immediate + Branch;

    /// <summary><c>IRQ001</c>: <c>CMPA #$0C</c>, <c>BHI</c>.</summary>
    private const int LowRangeCycles = Immediate + Branch;

    /// <summary><c>IRQ10</c> or <c>IRQ20</c>: <c>CMPA #$1B</c>, <c>BHI</c>, <c>SUBA</c>.</summary>
    private const int MiddleRangeCycles = Immediate + Branch + Immediate;

    /// <summary>
    /// <c>IRQ2</c> calling a routine through the jump table: <c>ASLA</c>, <c>LDX #JMPTBL</c>, <c>BSR ADDX</c>,
    /// <c>LDX 0,X</c>, <c>JSR 0,X</c>.
    /// </summary>
    private const int JumpTableCycles =
        Inherent + WordImmediate + CallShort + (SubroutineCycles.CallAddToIndex - CallExtended) + WordIndexed + CallShort;

    /// <summary>The high range: <c>CMPA #$3D</c>, <c>BGT</c>, <c>CMPA #$2A</c>, <c>BHI</c>, <c>SUBA #$10</c>, <c>BRA IRQ002</c>.</summary>
    private const int HighRangeCycles = Immediate + Branch + Immediate + Branch + Immediate + Branch;

    /// <summary><c>BGEND</c>: <c>CLRA</c>, two <c>STAA</c>, <c>RTS</c>.</summary>
    private const int BackgroundEndCycles = Inherent + StoreDirect + StoreDirect + Return;

    private readonly BoardMemory _memory = new();
    private readonly BoardOutput _output = new();
    private readonly IReadOnlyDictionary<int, Func<IEnumerable<OutputChange>>> _routines;
    private readonly WaveTableSound _waveTableSound;
    private IEnumerator<OutputChange>? _outputChanges;
    private int _waitingSoundNumber = NoSoundNumber;
    private int _cyclesLeftInChange;
    private byte _nextLevel;
    private int _nextWriteCycles;

    /// <summary>Switches the board on: silent, waiting for a sound number.</summary>
    public SoundBoard()
    {
        _waveTableSound = new WaveTableSound(_memory, _output);
        _routines = BuildRoutines();
    }

    /// <summary>The level the board is sending to the loudspeaker circuit right now, 0 to 255.</summary>
    public byte OutputLevel { get; private set; }

    /// <summary>True when the board can play a sound number: one of the numbers the game sends.</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>True when its routine has been built.</returns>
    public bool CanPlay(int soundNumber) => _routines.ContainsKey(soundNumber);

    /// <summary>Runs the board on, until the next change of level or for <paramref name="maxCycles"/>, whichever comes first.</summary>
    /// <param name="maxCycles">The most cycles to run; at least 1.</param>
    /// <returns>How many cycles ran.</returns>
    public int Run(int maxCycles)
    {
        if (_cyclesLeftInChange == 0)
        {
            AnswerWaitingSoundNumber();
        }

        if (_cyclesLeftInChange == 0 && !TakeNextChange())
        {
            return maxCycles;
        }

        int cycles = Math.Min(maxCycles, _cyclesLeftInChange);
        _cyclesLeftInChange -= cycles;
        if (_cyclesLeftInChange == 0)
        {
            OutputLevel = _nextLevel;
        }

        return cycles;
    }

    /// <summary>
    /// Sends the board a sound number. The sound playing stops at once, except that a write to the output
    /// port already under way finishes first, as the processor answers an interrupt only between
    /// instructions. Like the arcade's input chip, the board holds the number until it next runs, so a
    /// second number sent before then replaces the first.
    /// </summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 1 to 63; 0 does not reach the board.</param>
    /// <exception cref="ArgumentOutOfRangeException">The board has no routine for the number.</exception>
    public void SendSoundNumber(int soundNumber)
    {
        int number = soundNumber & SoundLines;
        if (number == 0)
        {
            return;
        }

        if (!CanPlay(number))
        {
            throw new ArgumentOutOfRangeException(
                nameof(soundNumber), $"Sound ${number:X2} has no routine. Port it from VSNDRM3.SRC and add it to {nameof(BuildRoutines)}.");
        }

        _waitingSoundNumber = number;
        if (!IsWriteUnderWay())
        {
            _cyclesLeftInChange = 0;
        }
    }

    /// <summary>
    /// Answers a waiting sound number as the board's interrupt handler does: stops the sound playing, clears
    /// the repeat counts of the sounds that keep them, and starts the new sound (<c>IRQ</c>).
    /// </summary>
    private void AnswerWaitingSoundNumber()
    {
        if (_waitingSoundNumber == NoSoundNumber)
        {
            return;
        }

        int number = _waitingSoundNumber;
        _waitingSoundNumber = NoSoundNumber;
        _outputChanges?.Dispose();
        _output.Restart(OutputLevel);
        _output.Wait(Interrupt + HandlerStartCycles + FlagCheckCycles + SortCycles);
        ClearRepeatCounts(number);
        _outputChanges = _routines[number]().GetEnumerator();
    }

    /// <summary>True when the instruction that writes the next level has started but not finished.</summary>
    private bool IsWriteUnderWay() => _cyclesLeftInChange > 0 && _cyclesLeftInChange < _nextWriteCycles;

    /// <summary>
    /// Clears the spinner's send count unless the number is the spinner, and the laser ball bonus's repeat
    /// unless it is the bonus (<c>IRQ00</c>: <c>STAB SP1FLG</c>; <c>IRQ00A</c>: <c>STAB B2FLG</c>).
    /// </summary>
    /// <param name="number">The sound number being answered.</param>
    private void ClearRepeatCounts(int number)
    {
        if (number != SpinnerSoundNumber)
        {
            _output.Wait(StoreDirect);
            _memory.SpinnerSends = 0;
        }

        if (number != LaserBallBonusSoundNumber)
        {
            _output.Wait(StoreDirect);
            _memory.IsLaserBallBonusRepeating = false;
        }
    }

    /// <summary>Moves on to the sound's next change of level, or goes quiet when the sound has ended.</summary>
    /// <returns>True when there is a change to run towards.</returns>
    private bool TakeNextChange()
    {
        while (_outputChanges is not null && _outputChanges.MoveNext())
        {
            OutputChange change = _outputChanges.Current;
            _nextLevel = change.Level;
            _nextWriteCycles = change.WriteCycles;
            _cyclesLeftInChange = change.CyclesBefore;
            if (_cyclesLeftInChange > 0)
            {
                return true;
            }

            OutputLevel = change.Level;
        }

        _outputChanges = null;
        return false;
    }

    /// <summary>
    /// Every sound number the game sends, with the routine the board's handler sends it to. This is the
    /// one place a sound number is matched to its routine.
    /// </summary>
    /// <returns>The routines, by sound number.</returns>
    private Dictionary<int, Func<IEnumerable<OutputChange>>> BuildRoutines() => new()
    {
        [0x01] = CreateLowWaveTableRoutine(0x01), // HBDV "HEARTBEAT DISTORTO"
        [0x04] = CreateLowWaveTableRoutine(0x04), // XBV
        [0x06] = CreateLowWaveTableRoutine(0x06), // HBEV "HEARTBEAT ECHO"
        [0x08] = CreateLowWaveTableRoutine(0x08), // SPNRV
        [0x0D] = CreateLowWaveTableRoutine(0x0D), // ED17
        [SpinnerSoundNumber] = CreateJumpTableRoutine(() => SpinnerSound.Play(_memory, _output)), // SP1
        [0x11] = CreateJumpTableRoutine(() => LightningNoise.PlayLightning(_memory, _output)), // LITE
        [LaserBallBonusSoundNumber] = CreateJumpTableRoutine(_waveTableSound.PlayLaserBallBonus), // BON2
        [0x13] = CreateJumpTableRoutine(EndBackground), // BGEND
        [0x14] = CreateJumpTableRoutine(() => WhiteNoise.PlayTurbo(_memory, _output)), // TURBO
        [0x15] = CreateJumpTableRoutine(() => LightningNoise.PlayAppear(_memory, _output)), // APPEAR
        [0x17] = CreateJumpTableRoutine(() => FilteredNoise.PlayCannon(_memory, _output)), // CANNON
        [0x18] = CreateJumpTableRoutine(() => RadioSound.Play(_memory, _output)), // RADIO
        [0x19] = CreateJumpTableRoutine(() => HyperSound.Play(_memory, _output)), // HYPER
        [0x1A] = CreateJumpTableRoutine(() => ScreamSound.Play(_memory, _output)), // SCREAM
        [0x1D] = CreateSquareWaveRoutine(0x1D), // SAW
        [0x1E] = CreateSquareWaveRoutine(0x1E), // FOSHIT
        [0x25] = CreateHighWaveTableRoutine(0x25), // SSPV
        [0x28] = CreateHighWaveTableRoutine(0x28), // GDYUKV
    };

    /// <summary>A wave table sound numbered 1 to 13 (<c>IRQ001</c> to <c>IRQ002</c>).</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>The routine.</returns>
    private Func<IEnumerable<OutputChange>> CreateLowWaveTableRoutine(int soundNumber) => () =>
    {
        _output.Wait(LowRangeCycles);
        return _waveTableSound.LoadAndPlay(soundNumber - LowWaveTableOffset);
    };

    /// <summary>A wave table sound numbered 32 to 43, which the handler moves down to follow the low ones (<c>IRQ00</c>'s high range).</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>The routine.</returns>
    private Func<IEnumerable<OutputChange>> CreateHighWaveTableRoutine(int soundNumber) => () =>
    {
        _output.Wait(HighRangeCycles);
        return _waveTableSound.LoadAndPlay(soundNumber - HighWaveTableOffset);
    };

    /// <summary>A sound with its own routine, reached through the jump table <c>JMPTBL</c> (<c>IRQ10</c>, <c>IRQ2</c>).</summary>
    /// <param name="routine">The routine.</param>
    /// <returns>The routine, after the handler's time to reach it.</returns>
    private Func<IEnumerable<OutputChange>> CreateJumpTableRoutine(Func<IEnumerable<OutputChange>> routine) => () =>
    {
        _output.Wait(LowRangeCycles + MiddleRangeCycles + JumpTableCycles);
        return routine();
    };

    /// <summary>A square wave sound (<c>IRQ20</c>, <c>IRQ21</c>: <c>JSR VARILD</c>, <c>JSR VARI</c>).</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>The routine.</returns>
    private Func<IEnumerable<OutputChange>> CreateSquareWaveRoutine(int soundNumber) => () =>
    {
        _output.Wait(LowRangeCycles + MiddleRangeCycles + CallExtended);
        var square = new SquareWaveSound(_output);
        square.Load(soundNumber - SquareWaveOffset);
        _output.Wait(CallExtended);
        return square.Play();
    };

    /// <summary>
    /// Turns the background sounds off (<c>BGEND</c>). Robotron never turns them on, so this only stops the
    /// sound playing, which is how the game uses it ("BACKY OFFY", notes §126).
    /// </summary>
    /// <returns>No output changes.</returns>
    private IEnumerable<OutputChange> EndBackground()
    {
        _output.Wait(BackgroundEndCycles);
        return [];
    }
}
