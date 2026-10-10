using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The arcade's sound board, rebuilt from its program's source: it waits for a sound number, then plays
///     that sound by changing its output level at the same moments the real board does.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c> ("ROBOTRON SOUNDS VERSION 1.0 3-8-82"), routine <c>IRQ</c>
///             ("INTERRUPT PROCESSING") and the sound routines it calls.
///         </item>
///         <item>Disassembly: none in this repo; ROM <c>$FB11</c> (<c>IRQ</c>, the sound ROM's interrupt vector).</item>
///     </list>
///     The board runs each sound as a program timed by its own instructions. The port times each sound with
///     the same instruction costs (<see cref="InstructionCycles" />), so pitches and lengths match the arcade's.
///     Every sound number the game sends is built, and so are the eight coin sounds (<c>CNSND</c>); the notes (§130)
///     explain how they were checked.
/// </remarks>
public sealed class SoundBoard : ISoundBoard
{
    /// <summary>
    ///     The board's processor clock in cycles a second: its 3.579545 MHz crystal divided by four inside
    ///     the chip (MAME's Williams driver).
    /// </summary>
    public const int ClockHertz = 894_886;

    /// <summary>The six sound lines (<c>RRF.ASM</c>: "B0-B5 SOUND"): a sound number is 0 to 63.</summary>
    private const int SoundLines = 0x3F;

    /// <summary>
    ///     No sound number is waiting to be answered. It is the starting value of <see cref="_waitingSoundNumber" />,
    ///     which means that no sound is waiting.
    /// </summary>
    private const int NoSoundNumber = -1;

    /// <summary>The spinner sound, which keeps its send count (<c>SP1SND</c>).</summary>
    private const int SpinnerSoundNumber = 0x0E;

    /// <summary>The laser ball bonus, which keeps its place (<c>B2SND</c>).</summary>
    private const int LaserBallBonusSoundNumber = 0x12;

    /// <summary>What the handler takes off a low sound number to find its wave table settings (<c>DECA</c>).</summary>
    private const int LowWaveTableOffset = 0x01;

    /// <summary>
    ///     What the handler takes off a high sound number to find its wave table settings (<c>DECA</c>, <c>SUBA #$10</c>
    ///     ).
    /// </summary>
    private const int HighWaveTableOffset = 0x11;

    /// <summary>What the handler takes off a square wave sound number to find its settings (<c>DECA</c>, <c>SUBA #$1C</c>).</summary>
    private const int SquareWaveOffset = 0x1D;

    /// <summary>
    ///     <c>IRQ</c> up to the flag checks: <c>LDS</c>, <c>LDAA SOUND+2</c>, <c>LDX</c>, <c>STX XDECAY</c>, <c>LDX</c>,
    ///     <c>STX XPTR</c>, <c>LDAB</c>, <c>STAB AMP0</c>, <c>CLI</c>, <c>COMA</c>, <c>ANDA</c>, <c>LDAB ORGFLG</c>,
    ///     <c>BEQ</c>.
    /// </summary>
    private const int HandlerStartCycles =
        WordImmediate + Extended + WordImmediate + WordStoreDirect + WordImmediate + WordStoreDirect + Immediate +
        StoreDirect
        + Inherent + Inherent + Immediate + Direct + Branch;

    /// <summary><c>IRQ00</c>/<c>IRQ00A</c> checking both flags: <c>CLRB</c>, then <c>CMPA</c>, <c>BEQ</c> twice.</summary>
    private const int FlagCheckCycles = Inherent + 2 * (Immediate + Branch);

    /// <summary><c>IRQ000</c> sorting the number: <c>TSTA</c>, <c>BEQ</c>, <c>DECA</c>, <c>CMPA #$1F</c>, <c>BLT</c>.</summary>
    private const int SortCycles = Inherent + Branch + Inherent + Immediate + Branch;

    /// <summary><c>IRQ001</c>: <c>CMPA #$0C</c>, <c>BHI</c>.</summary>
    private const int LowRangeCycles = Immediate + Branch;

    /// <summary><c>IRQ10</c> or <c>IRQ20</c>: <c>CMPA #$1B</c>, <c>BHI</c>, <c>SUBA</c>.</summary>
    private const int MiddleRangeCycles = Immediate + Branch + Immediate;

    /// <summary>
    ///     <c>IRQ2</c> calling a routine through the jump table: <c>ASLA</c>, <c>LDX #JMPTBL</c>, <c>BSR ADDX</c>,
    ///     <c>LDX 0,X</c>, <c>JSR 0,X</c>.
    /// </summary>
    private const int JumpTableCycles =
        Inherent + WordImmediate + CallShort + (SubroutineCycles.CallAddToIndex - CallExtended) + WordIndexed +
        CallShort;

    /// <summary>
    ///     The high range: <c>CMPA #$3D</c>, <c>BGT</c>, <c>CMPA #$2A</c>, <c>BHI</c>, <c>SUBA #$10</c>,
    ///     <c>BRA IRQ002</c>.
    /// </summary>
    private const int HighRangeCycles = Immediate + Branch + Immediate + Branch + Immediate + Branch;

    /// <summary>
    ///     The first and last wave table sounds the handler reaches by the low range (<c>IRQ001</c>), and by the high
    ///     range (<c>IRQ00</c>).
    /// </summary>
    private const int FirstLowWaveTable = 0x01;

    private const int LastLowWaveTable = 0x0D;

    private const int FirstHighWaveTable = 0x20;

    private const int LastHighWaveTable = 0x2B;

    /// <summary>The place of <c>MOSQTO</c> in the square wave settings (<c>IRQ00C</c>: <c>SUBA #$39</c>).</summary>
    private const int MosquitoVector = 5;

    /// <summary>The handler sorting sound number 63: <c>CMPA #$3D</c>, <c>BGT</c>, <c>SUBA #$39</c>, <c>BRA</c>.</summary>
    private const int HighestRangeCycles = Immediate + Branch + Immediate + Branch;

    /// <summary>The second background sound's highest pitch (<c>BG2MAX</c>).</summary>
    private const int Background2MaxLevel = 29;

    /// <summary><c>IRQ3</c> checking the flags: <c>LDAA BG1FLG</c>, <c>ORAA BG2FLG</c>, <c>BEQ</c>.</summary>
    private const int BackgroundCheckCycles = Direct + Direct + Branch;

    /// <summary><c>IRQ3</c> starting a background: <c>CLRA</c>, <c>STAA B2FLG</c>, <c>LDAA BG1FLG</c>, <c>BEQ</c>, <c>JMP</c>.</summary>
    private const int BackgroundStartCycles = Inherent + StoreDirect + Direct + Branch + JumpExtended;

    /// <summary>
    ///     The handler seeing that an organ tune is next: <c>LDAB ORGFLG</c>, <c>BEQ</c>, <c>JSR ORGNT1</c>, then the
    ///     tune search, which the port does not play.
    /// </summary>
    private const int OrganTuneCycles = Direct + Branch + CallExtended + CallShort + ModifyExtended + Return;

    /// <summary><c>BGEND</c>: <c>CLRA</c>, two <c>STAA</c>, <c>RTS</c>.</summary>
    private const int BackgroundEndCycles = Inherent + StoreDirect + StoreDirect + Return;

    private readonly BoardMemory _memory = new();
    private readonly BoardOutput _output = new();
    private readonly IReadOnlyDictionary<int, Func<IEnumerable<OutputChange>>> _routines;
    private readonly WaveTableSound _waveTableSound;
    private int _cyclesLeftInChange;
    private byte _nextLevel;
    private int _nextWriteCycles;
    private IEnumerator<OutputChange>? _outputChanges;
    private int _waitingSoundNumber = NoSoundNumber;

    /// <summary>Switches the board on: silent, waiting for a sound number.</summary>
    public SoundBoard()
    {
        _waveTableSound = new WaveTableSound(_memory, _output);
        _routines = BuildRoutines();
    }

    /// <inheritdoc />
    public bool IsPlaying =>
        _waitingSoundNumber != NoSoundNumber || _outputChanges is not null || _cyclesLeftInChange > 0;

    /// <summary>The level the board is sending to the loudspeaker circuit right now, 0 to 255.</summary>
    public byte OutputLevel { get; private set; }

    /// <summary>Runs the board on, until the next change of level or for <paramref name="maxCycles" />, whichever comes first.</summary>
    /// <param name="maxCycles">The most cycles to run; at least 1.</param>
    /// <returns>How many cycles ran.</returns>
    public int Run(int maxCycles)
    {
        if (_cyclesLeftInChange == 0) AnswerWaitingSoundNumber();

        if (_cyclesLeftInChange == 0 && !TakeNextChange()) return maxCycles;

        var cycles = Math.Min(maxCycles, _cyclesLeftInChange);
        _cyclesLeftInChange -= cycles;
        if (_cyclesLeftInChange == 0) OutputLevel = _nextLevel;

        return cycles;
    }

    /// <summary>
    ///     Sends the board a sound number. The sound playing stops at once, except that a write to the output
    ///     port already under way finishes first, as the processor answers an interrupt only between
    ///     instructions. Like the arcade's input chip, the board holds the number until it next runs, so a
    ///     second number sent before then replaces the first.
    /// </summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 1 to 63; 0 does not reach the board.</param>
    /// <exception cref="ArgumentOutOfRangeException">The board has no routine for the number.</exception>
    public void SendSoundNumber(int soundNumber)
    {
        var number = soundNumber & SoundLines;
        if (number == 0) return;

        if (!CanPlay(number))
            throw new ArgumentOutOfRangeException(
                nameof(soundNumber),
                $"Sound ${number:X2} has no routine. Port it from VSNDRM3.SRC and add it to {nameof(BuildRoutines)}.");

        _waitingSoundNumber = number;
        if (!IsWriteUnderWay()) _cyclesLeftInChange = 0;
    }

    /// <summary>True when the board can play a sound number: one of the numbers the game sends.</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>True when its routine has been built.</returns>
    public bool CanPlay(int soundNumber)
    {
        return _routines.ContainsKey(soundNumber);
    }

    /// <summary>
    ///     Answers a waiting sound number as the board's interrupt handler does: stops the sound playing, clears
    ///     the repeat counts of the sounds that keep them, and starts the new sound (<c>IRQ</c>).
    /// </summary>
    private void AnswerWaitingSoundNumber()
    {
        if (_waitingSoundNumber == NoSoundNumber) return;

        var number = _waitingSoundNumber;
        _waitingSoundNumber = NoSoundNumber;
        _outputChanges?.Dispose();
        _output.Restart(OutputLevel);
        _output.Wait(Interrupt + HandlerStartCycles);
        IEnumerable<OutputChange> routine = [];
        if (_memory.IsOrganTuneNext)
        {
            _memory.IsOrganTuneNext = false;
            _output.Wait(OrganTuneCycles);
        }
        else
        {
            _output.Wait(FlagCheckCycles + SortCycles);
            ClearRepeatCounts(number);
            routine = _routines[number]();
        }

        _outputChanges = WithBackground(routine).GetEnumerator();
    }

    /// <summary>True when the instruction that writes the next level has started but not finished.</summary>
    private bool IsWriteUnderWay()
    {
        return _cyclesLeftInChange > 0 && _cyclesLeftInChange < _nextWriteCycles;
    }

    /// <summary>
    ///     Clears the spinner's send count unless the number is the spinner, and the laser ball bonus's repeat
    ///     unless it is the bonus (<c>IRQ00</c>: <c>STAB SP1FLG</c>; <c>IRQ00A</c>: <c>STAB B2FLG</c>).
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
            var change = _outputChanges.Current;
            _nextLevel = change.Level;
            _nextWriteCycles = change.WriteCycles;
            _cyclesLeftInChange = change.CyclesBefore;
            if (_cyclesLeftInChange > 0) return true;

            OutputLevel = change.Level;
        }

        _outputChanges = null;
        return false;
    }

    /// <summary>
    ///     Every sound number the board has, with the routine the board's handler sends it to. This is the one place
    ///     a sound number is matched to its routine; the names are the source's (<see cref="BoardSounds" />).
    /// </summary>
    /// <returns>The routines, by sound number.</returns>
    private Dictionary<int, Func<IEnumerable<OutputChange>>> BuildRoutines()
    {
        var routines = new Dictionary<int, Func<IEnumerable<OutputChange>>>();
        for (var number = FirstLowWaveTable; number <= LastLowWaveTable; number++)
            routines[number] = CreateLowWaveTableRoutine(number);

        for (var number = FirstHighWaveTable; number <= LastHighWaveTable; number++)
            routines[number] = CreateHighWaveTableRoutine(number);

        AddJumpTableRoutines(routines);
        AddHighJumpTableRoutines(routines);
        routines[0x1D] = CreateSquareWaveRoutine(0x1D - SquareWaveOffset, LowRangeCycles + MiddleRangeCycles); // SAW
        routines[0x1E] = CreateSquareWaveRoutine(0x1E - SquareWaveOffset, LowRangeCycles + MiddleRangeCycles); // FOSHIT
        routines[0x1F] = CreateSquareWaveRoutine(0x1F - SquareWaveOffset, LowRangeCycles + MiddleRangeCycles); // QUASAR
        routines[0x3F] = CreateSquareWaveRoutine(MosquitoVector, HighestRangeCycles); // MOSQTO
        return routines;
    }

    /// <summary>The sounds <c>JMPTBL</c> holds (<c>$0E</c> to <c>$1C</c>), which the handler reaches by the low range.</summary>
    /// <param name="routines">The routines to add them to.</param>
    private void AddJumpTableRoutines(Dictionary<int, Func<IEnumerable<OutputChange>>> routines)
    {
        routines[SpinnerSoundNumber] = CreateJumpTableRoutine(() => SpinnerSound.Play(_memory, _output)); // SP1
        routines[0x0F] = CreateJumpTableRoutine(StartBackground1); // BG1
        routines[0x10] = CreateJumpTableRoutine(IncrementBackground2); // BG2INC
        routines[0x11] = CreateJumpTableRoutine(() => LightningNoise.PlayLightning(_memory, _output)); // LITE
        routines[LaserBallBonusSoundNumber] = CreateJumpTableRoutine(_waveTableSound.PlayLaserBallBonus); // BON2
        routines[0x13] = CreateJumpTableRoutine(EndBackground); // BGEND
        routines[0x14] = CreateJumpTableRoutine(() => WhiteNoise.PlayTurbo(_memory, _output)); // TURBO
        routines[0x15] = CreateJumpTableRoutine(() => LightningNoise.PlayAppear(_memory, _output)); // APPEAR
        routines[0x16] = CreateJumpTableRoutine(() => FilteredNoise.PlayThrust(_memory, _output)); // THRUST
        routines[0x17] = CreateJumpTableRoutine(() => FilteredNoise.PlayCannon(_memory, _output)); // CANNON
        routines[0x18] = CreateJumpTableRoutine(() => RadioSound.Play(_memory, _output)); // RADIO
        routines[0x19] = CreateJumpTableRoutine(() => HyperSound.Play(_memory, _output)); // HYPER
        routines[0x1A] = CreateJumpTableRoutine(() => ScreamSound.Play(_memory, _output)); // SCREAM
        routines[0x1B] = CreateJumpTableRoutine(ArmOrganTune); // ORGANT
        routines[0x1C] = CreateJumpTableRoutine(PlayOrganNote); // ORGANN
    }

    /// <summary>The sounds <c>JMPTB1</c> holds (<c>$2C</c> to <c>$3E</c>), which the handler reaches by the high range.</summary>
    /// <param name="routines">The routines to add them to.</param>
    private void AddHighJumpTableRoutines(Dictionary<int, Func<IEnumerable<OutputChange>>> routines)
    {
        routines[0x2C] = CreateHighJumpTableRoutine(() => OscillatorSound.PlaySound2(_memory, _output)); // SND2
        routines[0x2D] = CreateHighJumpTableRoutine(() => OscillatorSound.PlaySound5(_memory, _output)); // SND5
        routines[0x2E] = CreateHighJumpTableRoutine(() => OscillatorSound.PlayThunder(_memory, _output)); // THNDR
        routines[0x2F] = CreateHighJumpTableRoutine(() => SingSound.PlayHstd(_memory, _output)); // HSTD
        routines[0x30] = CreateHighJumpTableRoutine(() => SingSound.PlayAtari(_memory, _output)); // ATARI
        routines[0x31] = CreateHighJumpTableRoutine(() => SingSound.PlaySiren(_memory, _output)); // SIREN
        routines[0x32] = CreateHighJumpTableRoutine(() => SingSound.PlayOrrrr(_memory, _output)); // ORRRR
        routines[0x33] = CreateHighJumpTableRoutine(() => SingSound.PlayPerkDollars(_memory, _output)); // PERK$$
        routines[0x34] = CreateHighJumpTableRoutine(() => SingSound.PlaySquirts(_memory, _output)); // SQRT
        routines[0x35] = CreateHighJumpTableRoutine(() => ElectricSound.PlayStart(_output)); // START
        routines[0x36] = CreateHighJumpTableRoutine(() => PlaneSound.Play(_output)); // PLANE
        routines[0x37] = CreateHighJumpTableRoutine(() => OscillatorSound.PlaySound16(_memory, _output)); // SND16
        routines[0x38] = CreateHighJumpTableRoutine(() => OscillatorSound.PlaySound17(_memory, _output)); // SND17
        routines[0x39] = CreateHighJumpTableRoutine(() => LightningNoise.PlayLaunch(_memory, _output)); // LAUNCH
        routines[0x3A] = CreateHighJumpTableRoutine(() => CrowdRoarSound.Play(_memory, _output)); // CDR
        routines[0x3B] = CreateHighJumpTableRoutine(() => KnockerSound.Play(_output)); // KNOCK
        routines[0x3C] = CreateHighJumpTableRoutine(() => SirenSound.Play(_output)); // ZIREN
        routines[0x3D] = CreateHighJumpTableRoutine(() => WhistleSound.Play(_output)); // WHIST
        routines[0x3E] = CreateHighJumpTableRoutine(() => FilteredNoise.PlayBomb(_memory, _output)); // HBOMB
    }

    /// <summary>A wave table sound numbered 1 to 13 (<c>IRQ001</c> to <c>IRQ002</c>).</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>The routine.</returns>
    private Func<IEnumerable<OutputChange>> CreateLowWaveTableRoutine(int soundNumber)
    {
        return () =>
        {
            _output.Wait(LowRangeCycles);
            return _waveTableSound.LoadAndPlay(soundNumber - LowWaveTableOffset);
        };
    }

    /// <summary>
    ///     A wave table sound numbered 32 to 43, which the handler moves down to follow the low ones (<c>IRQ00</c>'s high
    ///     range).
    /// </summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <returns>The routine.</returns>
    private Func<IEnumerable<OutputChange>> CreateHighWaveTableRoutine(int soundNumber)
    {
        return () =>
        {
            _output.Wait(HighRangeCycles);
            return _waveTableSound.LoadAndPlay(soundNumber - HighWaveTableOffset);
        };
    }

    /// <summary>A sound with its own routine, reached through the jump table <c>JMPTBL</c> (<c>IRQ10</c>, <c>IRQ2</c>).</summary>
    /// <param name="routine">The routine.</param>
    /// <returns>The routine, after the handler's time to reach it.</returns>
    private Func<IEnumerable<OutputChange>> CreateJumpTableRoutine(Func<IEnumerable<OutputChange>> routine)
    {
        return () =>
        {
            _output.Wait(LowRangeCycles + MiddleRangeCycles + JumpTableCycles);
            return routine();
        };
    }

    /// <summary>
    ///     A sound with its own routine, reached through the jump table by the handler's high range (<c>IRQ00B</c>,
    ///     <c>IRQ2</c>): the
    ///     sounds numbered 44 and up.
    /// </summary>
    /// <param name="routine">The routine.</param>
    /// <returns>The routine, after the handler's time to reach it.</returns>
    private Func<IEnumerable<OutputChange>> CreateHighJumpTableRoutine(Func<IEnumerable<OutputChange>> routine)
    {
        return () =>
        {
            _output.Wait(HighRangeCycles + JumpTableCycles);
            return routine();
        };
    }

    /// <summary>A square wave sound (<c>IRQ20</c>, <c>IRQ21</c>: <c>JSR VARILD</c>, <c>JSR VARI</c>).</summary>
    /// <param name="vectorIndex">The sound's place in the square wave settings table.</param>
    /// <param name="handlerCycles">The time the handler takes to sort the number into this range.</param>
    /// <returns>The routine.</returns>
    private Func<IEnumerable<OutputChange>> CreateSquareWaveRoutine(int vectorIndex, int handlerCycles)
    {
        return () =>
        {
            _output.Wait(handlerCycles + CallExtended);
            var square = new SquareWaveSound(_output);
            square.Load(vectorIndex);
            _output.Wait(CallExtended);
            return square.Play();
        };
    }

    /// <summary>
    ///     Turns the background sounds off (<c>BGEND</c>). The game only uses it to stop the sound playing ("BACKY OFFY",
    ///     notes §126).
    /// </summary>
    /// <returns>No output changes.</returns>
    private IEnumerable<OutputChange> EndBackground()
    {
        _output.Wait(BackgroundEndCycles);
        _memory.IsBackground1On = false;
        _memory.Background2Level = 0;
        return [];
    }

    /// <summary>
    ///     Starts the first background sound (<c>BG1</c>): it plays until another sound number arrives, and again after
    ///     every sound that follows.
    /// </summary>
    /// <returns>The sound's output changes.</returns>
    private IEnumerable<OutputChange> StartBackground1()
    {
        _memory.IsBackground1On = true;
        return FilteredNoise.PlayBackground1(_memory, _output);
    }

    /// <summary>
    ///     Moves the second background sound up one pitch and turns it on (<c>BG2INC</c>); it then plays after this
    ///     sound, and after every sound that follows.
    /// </summary>
    /// <returns>No output changes.</returns>
    private IEnumerable<OutputChange> IncrementBackground2()
    {
        _output.Wait(ModifyExtended + Direct + Immediate + Immediate + Branch + Inherent + StoreDirect + Return);
        _memory.IsBackground1On = false;
        var level = _memory.Background2Level;
        _memory.Background2Level = (byte)(level == Background2MaxLevel ? 1 : level + 1);
        return [];
    }

    /// <summary>The organ tune sound (<c>ORGANT</c>): it only arms the board, so that the next sound number is a tune number.</summary>
    /// <returns>No output changes.</returns>
    private IEnumerable<OutputChange> ArmOrganTune()
    {
        _output.Wait(ModifyExtended + Return);
        _memory.IsOrganTuneNext = true;
        return [];
    }

    /// <summary>The organ note sound (<c>ORGANN</c>): the source's routine is a bare <c>RTS</c>.</summary>
    /// <returns>No output changes.</returns>
    private IEnumerable<OutputChange> PlayOrganNote()
    {
        _output.Wait(Return);
        return [];
    }

    /// <summary>
    ///     Plays a sound's routine and then, as the handler does after every sound (<c>IRQ3</c>), whichever
    ///     background sound is on, which goes on until the next sound number arrives.
    /// </summary>
    /// <param name="routine">The sound's output changes.</param>
    /// <returns>The sound's output changes, and the background's.</returns>
    private IEnumerable<OutputChange> WithBackground(IEnumerable<OutputChange> routine)
    {
        foreach (var change in routine) yield return change;

        _output.Wait(BackgroundCheckCycles);
        if (!_memory.IsBackground1On && _memory.Background2Level == 0) yield break;

        _output.Wait(BackgroundStartCycles);
        _memory.IsLaserBallBonusRepeating = false;
        var background = _memory.IsBackground1On
            ? FilteredNoise.PlayBackground1(_memory, _output)
            : _waveTableSound.PlayBackground2(_memory.Background2Level);
        foreach (var change in background) yield return change;
    }
}
