using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     A crowd roaring: white noise whose loudness swells and then fades, with whistles (triangle waves that
///     glide in pitch) laid over it, each starting when the noise reaches a set loudness.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c>, routine <c>CDR</c> ("CROWD ROAR") and the routines it runs:
///             <c>WISLD</c>, <c>NOISLD</c>, <c>NINIT</c>/<c>NINIT2</c> and the loop <c>WIN</c>, with the settings in
///             <c>WS1</c> (the whistles), <c>CR1</c> (the swell) and <c>CR2</c> (the fade).
///         </item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
///     Robotron uses it for one sound: the seventh of the eight coin sounds (sound number <c>$3A</c>, through
///     <c>JMPTB1</c>). The loop calls the triangle wave's output (<c>TRIDR</c>) between every one of its
///     steps, so the output is written a dozen times a round and every call has its own timing here.
///     One approximation: <c>ADDX</c>, which steps the whistle table on, takes 6 cycles longer when the
///     sum carries into the high byte of the table's address, which depends on where the ROM puts the table;
///     the port times it as if it never does, as it does for every other use of <c>ADDX</c>.
/// </remarks>
internal sealed class CrowdRoarSound
{
    /// <summary>
    ///     The bytes in each whistle's settings (<c>WS1</c>): loudness to start at, first pitch, pitch change, cycles per
    ///     pitch, lowest pitch.
    /// </summary>
    private const int WhistleBytes = 5;

    /// <summary>
    ///     The noise settings' first bytes in the order <c>NOISLD</c> reads them: cycles per loudness change, loudness,
    ///     loudness change, pitch flag, pitch.
    /// </summary>
    private const int NoiseBytes = 5;

    /// <summary>
    ///     What <c>CDR</c> puts in <c>HI</c> and <c>LO</c> to start the random numbers (<c>LDX #$A500</c>, <c>STX HI</c>
    ///     ).
    /// </summary>
    private const byte SeedHigh = 0xA5;

    private const byte SeedLow = 0x00;

    /// <summary>
    ///     The whistle's pass counter at the start (<c>NINIT</c>: <c>LDAA #$E</c>, <c>STAA WCNT</c>): "cycle offset for
    ///     whistle".
    /// </summary>
    private const byte FirstWhistleCount = 0x0E;

    /// <summary>The top bit, which makes a whistle's running value negative (<c>BPL GO</c>).</summary>
    private const byte SignBit = 0x80;

    /// <summary>
    ///     <c>CDR</c> up to the first noise settings: <c>LDX</c>, <c>STX PTRHI</c>, <c>JSR WISLD</c> (counted with it),
    ///     <c>LDX #$A500</c>, <c>STX HI</c>, <c>LDX #CR1</c>.
    /// </summary>
    private const int StartCycles = WordImmediate + WordStoreDirect + CallExtended + WordImmediate + WordStoreDirect +
                                    WordImmediate;

    /// <summary>
    ///     <c>WISLD</c> reading a whistle's settings: five <c>LDAA</c>/<c>STAA</c> pairs, <c>LDAA #5</c>, <c>JSR ADDX</c>
    ///     , <c>STX PTRHI</c>, <c>RTS</c>.
    /// </summary>
    private const int LoadWhistleCycles =
        5 * (Indexed + StoreDirect) + Branch + Immediate + SubroutineCycles.CallAddToIndex + WordStoreDirect + Return;

    /// <summary><c>WISLD</c> finding the list's end: <c>LDAA ,X</c>, <c>STAA WHIS</c>, <c>BEQ</c>, <c>RTS</c>.</summary>
    private const int EndOfWhistlesCycles = Indexed + StoreDirect + Branch + Return;

    /// <summary><c>NOISLD</c>: five <c>LDAA</c>/<c>STAA</c> pairs and <c>RTS</c>; and the <c>JSR</c> to it.</summary>
    private const int LoadNoiseCycles = CallExtended + NoiseBytes * (Indexed + StoreDirect) + Return;

    /// <summary>
    ///     <c>NINIT</c> before <c>NINIT2</c>: <c>JSR</c>, <c>CLR WFRQ</c>, <c>CLR DFRQ</c>, <c>LDAA</c>, <c>STAA WCNT</c>
    ///     , <c>CLR CURVAL</c>.
    /// </summary>
    private const int StartWhistleCycles =
        CallExtended + ModifyExtended + ModifyExtended + Immediate + StoreDirect + ModifyExtended;

    /// <summary>
    ///     <c>NINIT2</c> calling <c>NSUB</c>: <c>BSR</c>, <c>CLR CYCNT</c>, <c>LDAA</c>, <c>STAA</c>, <c>CLR NNOIS</c>,
    ///     <c>RTS</c>; and <c>JMP NINIT2</c> from <c>CDR</c>.
    /// </summary>
    private const int StartNoiseCycles = CallShort + ModifyExtended + Direct + StoreDirect + ModifyExtended + Return;

    private const int JumpToNoiseCycles = JumpExtended;

    /// <summary>
    ///     <c>NOISE1</c>: <c>BSR</c>, <c>LDAA LO</c>, three <c>LSRA</c>, <c>EORA LO</c>, <c>STAA ATP</c>, <c>INX</c>,
    ///     <c>ANDA #$7</c>, <c>RTS</c>.
    /// </summary>
    private const int Noise1Cycles =
        CallShort + Direct + 3 * Inherent + Direct + StoreDirect + IndexStep + Immediate + Return;

    /// <summary>
    ///     <c>NOISE2</c> up to the random step: <c>BSR</c>, <c>LDAA ATP</c>, <c>LSRA</c>; then <c>ROR HI</c> and
    ///     <c>ROR LO</c>.
    /// </summary>
    private const int Noise2StartCycles = CallShort + Direct + Inherent;

    /// <summary>
    ///     <c>NOISE2</c> after the step: <c>LDAA #0</c>, <c>BCC</c>, (<c>LDAA NAMP</c> when the bit is set),
    ///     <c>STAA NNOIS</c>, <c>RTS</c>.
    /// </summary>
    private const int Noise2EndCycles = Immediate + Branch + StoreDirect + Return;

    /// <summary>
    ///     <c>TRIDR</c> adding to the whistle's running value: <c>LDAA CURVAL</c>, <c>ADDA WFRQ</c>, <c>STAA CURVAL</c>,
    ///     <c>BPL</c>.
    /// </summary>
    private const int WhistleStepCycles = Direct + Direct + StoreDirect + Branch;

    /// <summary>
    ///     <c>TRIDR</c> after the optional <c>COMA</c>: <c>ABA</c>, then <c>STAA SOUND</c> and <c>RTS</c> (the write is
    ///     counted by <see cref="BoardOutput.Store" />).
    /// </summary>
    private const int WhistleAddNoiseCycles = Inherent;

    /// <summary>The call to <c>TRIDR</c>: <c>JSR</c> after <c>NOISE1</c> and <c>NOISE2</c>, <c>BSR</c> after the rest.</summary>
    private const int CallWhistleFromJsrCycles = CallExtended;

    private const int CallWhistleFromBsrCycles = CallShort;

    /// <summary><c>RNT</c>: <c>BSR</c>, <c>LDAA NFRQ2</c>, <c>DEC NFRQ</c>, <c>BEQ</c>.</summary>
    private const int NoiseTimerCycles = CallShort + Direct + ModifyExtended + Branch;

    /// <summary><c>RNT</c> when the timer has not run out: <c>INX</c>, <c>DEX</c>, <c>BRA</c>, <c>RTS</c>.</summary>
    private const int NoiseTimerWaitingCycles = IndexStep + IndexStep + Branch + Return;

    /// <summary><c>RNT</c> when it has: <c>STAA NFRQ</c>, <c>LDAB NNOIS</c>, <c>LSRB</c>, <c>INC CYCNT</c>, <c>RTS</c>.</summary>
    private const int NoiseTimerReloadCycles = StoreDirect + Direct + Inherent + ModifyExtended + Return;

    /// <summary><c>RNA</c>: <c>BSR</c>, <c>LDAA CY2</c>, <c>CMPA CYCNT</c>, <c>BEQ</c>.</summary>
    private const int NoiseLoudnessCycles = CallShort + Direct + Direct + Branch;

    /// <summary><c>RNA</c> when it is not yet time: <c>INX</c>, <c>DEX</c>, <c>BRA</c>, <c>RTS</c>.</summary>
    private const int NoiseLoudnessWaitingCycles = IndexStep + IndexStep + Branch + Return;

    /// <summary><c>RNA</c> when it is: <c>CLR CYCNT</c>, <c>LDAA NAMP</c>, <c>SUBA DECAY</c>, <c>STAA NAMP</c>, <c>RTS</c>.</summary>
    private const int NoiseLoudnessChangeCycles = ModifyExtended + Direct + Direct + StoreDirect + Return;

    /// <summary><c>TRICNT</c>: <c>BSR</c>, <c>LDAA WCNT2</c>, <c>DEC WCNT</c>, <c>BEQ</c>.</summary>
    private const int WhistleCountCycles = CallShort + Direct + ModifyExtended + Branch;

    /// <summary>
    ///     <c>TRICNT</c> when the count has not run out: <c>LDAA NAMP</c> (a full address, from the raw <c>FCB $B6</c>),
    ///     <c>BNE</c>, then <c>RTS</c>, or <c>BRA NSEND</c> and its <c>RTS</c>.
    /// </summary>
    private const int WhistleCountWaitingCycles = Extended + Branch;

    private const int WhistleCountSilentCycles = Branch;

    /// <summary><c>TRICNT</c> when it has: <c>STAA WCNT</c>, <c>LDAA WFRQ</c>, <c>ADDA DFRQ</c>, <c>STAA WFRQ</c>.</summary>
    private const int WhistleGlideCycles = StoreDirect + Direct + Direct + StoreDirect;

    /// <summary><c>TRIFRQ</c>: <c>BSR</c>, <c>LDAA WFRQ</c>, <c>CMPA MINWIS</c>, <c>BEQ</c>.</summary>
    private const int WhistleEndTestCycles = CallShort + Direct + Direct + Branch;

    /// <summary><c>TRIFRQ</c> when the whistle has not ended: <c>INX</c>, <c>LDAA NAMP</c>, <c>BNE</c>.</summary>
    private const int WhistleGoingCycles = IndexStep + Direct + Branch;

    /// <summary><c>TRIFRQ</c> turning the whistle off: <c>CLR WFRQ</c>, <c>CLR DFRQ</c>, <c>CLR CURVAL</c>, <c>LDX PTRHI</c>.</summary>
    private const int WhistleOffCycles = ModifyExtended + ModifyExtended + ModifyExtended + WordDirect;

    /// <summary><c>NNW</c>: <c>BSR</c>, <c>LDAA WHIS</c>, <c>BEQ</c>.</summary>
    private const int WhistleStartTestCycles = CallShort + Direct + Branch;

    /// <summary><c>NNW</c> with no whistle waiting: <c>INX</c>, <c>DEX</c>, <c>RTS</c>.</summary>
    private const int NoWhistleWaitingCycles = IndexStep + IndexStep + Return;

    /// <summary><c>NNW</c> with one waiting but not yet due: <c>CMPA NAMP</c>, <c>BNE</c>, <c>RTS</c>.</summary>
    private const int WhistleNotDueCycles = Direct + Branch + Return;

    /// <summary>
    ///     <c>NNW</c> starting the whistle: <c>CMPA NAMP</c>, <c>BNE</c> (not taken), <c>BRA WINIT</c>, then
    ///     <c>CLR WHIS</c>, <c>LDAA</c>, <c>STAA</c>, <c>LDAA</c>, <c>STAA</c>, <c>RTS</c>.
    /// </summary>
    private const int WhistleStartCycles =
        Direct + Branch + Branch + ModifyExtended + Direct + StoreDirect + Direct + StoreDirect + Return;

    /// <summary>The loop's <c>BRA WIN</c>.</summary>
    private const int LoopBackCycles = Branch;

    /// <summary><c>WS1</c>: the whistles, in the order they are started; a zero loudness ends the list.</summary>
    private static readonly byte[] Whistles =
    [
        0x90, 0x10, 0x02, 0x14, 0x40,
        0xB4, 0x40, 0xFF, 0x14, 0x30,
        0xD0, 0x32, 0x02, 0x10, 0x60,
        0xEE, 0x20, 0x02, 0x08, 0x54,
        0xE9, 0x54, 0xFF, 0x20, 0x28,
        0xC0, 0x30, 0x02, 0x14, 0x58,
        0xAC, 0x20, 0x02, 0x08, 0x58,
        0xA6, 0x58, 0xFF, 0x18, 0x22,
        0x00
    ];

    /// <summary><c>CR1</c>: the swell (a loudness that rises by 4 each time round, since <c>SUBA</c> takes $FC off).</summary>
    private static readonly byte[] Swell = [0x30, 0x10, 0xFC, 0x00, 0x01];

    /// <summary><c>CR2</c>: the fade.</summary>
    private static readonly byte[] Fade = [0x30, 0xFC, 0x01, 0x00, 0x01];

    private readonly BoardMemory _memory;
    private readonly BoardOutput _output;
    private byte _noiseCycles;
    private byte _noiseCyclesPerChange;
    private byte _noiseLoudness;
    private byte _noiseLoudnessChange;
    private byte _noiseNext;
    private byte _noiseOut;
    private byte _noisePitchLeft;
    private byte _noisePitchReload;
    private byte _whistleCount;
    private byte _whistleCountReload;
    private byte _whistleGlide;
    private int _whistleIndex;
    private byte _whistleLoudness;
    private byte _whistleLowestPitch;
    private byte _whistlePitch;
    private byte _whistleStartGlide;
    private byte _whistleStartPitch;
    private byte _whistleValue;

    /// <summary>Creates the sound on the board's memory and output port.</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    private CrowdRoarSound(BoardMemory memory, BoardOutput output)
    {
        _memory = memory;
        _output = output;
    }

    /// <summary>Plays the crowd roar (sound <c>CDR</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardMemory memory, BoardOutput output)
    {
        return new CrowdRoarSound(memory, output).Play();
    }

    /// <summary>The whole sound: the first whistle, the swell, then the fade.</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> Play()
    {
        _output.Wait(StartCycles);
        LoadWhistle();
        _memory.SetRandom(SeedHigh, SeedLow);

        LoadNoise(Swell);
        _output.Wait(StartWhistleCycles);
        _whistlePitch = 0;
        _whistleGlide = 0;
        _whistleCount = FirstWhistleCount;
        _whistleValue = 0;
        foreach (var change in RunNoise()) yield return change;

        LoadNoise(Fade);
        _output.Wait(JumpToNoiseCycles + WordImmediate);
        foreach (var change in RunNoise()) yield return change;
    }

    /// <summary>Reads the next whistle's settings, or notes that there are no more (<c>WISLD</c>).</summary>
    private void LoadWhistle()
    {
        var at = _whistleIndex * WhistleBytes;
        _whistleLoudness = Whistles[at];
        if (_whistleLoudness == 0)
        {
            _output.Wait(EndOfWhistlesCycles);
            return;
        }

        _whistleStartPitch = Whistles[at + 1];
        _whistleStartGlide = Whistles[at + 2];
        _whistleCountReload = Whistles[at + 3];
        _whistleLowestPitch = Whistles[at + 4];
        _whistleIndex++;
        _output.Wait(LoadWhistleCycles);
    }

    /// <summary>Loads a noise's settings (<c>NOISLD</c>).</summary>
    /// <param name="settings">The five bytes: <c>CY2</c>, <c>NAMP</c>, <c>DECAY</c>, <c>NFFLG</c> and <c>NFRQ2</c>.</param>
    private void LoadNoise(byte[] settings)
    {
        _output.Wait(LoadNoiseCycles);
        _noiseCyclesPerChange = settings[0];
        _noiseLoudness = settings[1];
        _noiseLoudnessChange = settings[2];
        _noisePitchReload = settings[4];
    }

    /// <summary>
    ///     <summary>
    ///         Plays one noise until it ends: <c>NINIT2</c> then the loop <c>WIN</c>, which writes the whistle's
    ///         output after every step of the noise.
    ///     </summary>
    ///     <returns>The output changes.</returns>
    private IEnumerable<OutputChange> RunNoise()
    {
        _output.Wait(StartNoiseCycles);
        _noiseCycles = 0;
        _noisePitchLeft = _noisePitchReload;
        _noiseNext = 0;

        while (true)
        {
            // NOISE1, a write, NOISE2 (the random step), a write.
            _output.Wait(Noise1Cycles);
            yield return WriteWhistle(CallWhistleFromJsrCycles);
            _output.Wait(Return);
            _output.Wait(Noise2StartCycles);
            yield return _output.Pass();
            _memory.RotateRandomHigh(_memory.RandomLow);
            _output.Wait(ModifyExtended);
            yield return _output.Pass();
            _memory.RotateRandomLow();
            _output.Wait(ModifyExtended + Noise2EndCycles + (_memory.RandomBitOut ? Direct : 0));
            _noiseNext = _memory.RandomBitOut ? _noiseLoudness : (byte)0;
            yield return WriteWhistle(CallWhistleFromJsrCycles);
            _output.Wait(Return);

            // RNT, a write, RNA, a write, TRICNT, a write.
            RunNoiseTimer();
            yield return WriteWhistle(CallWhistleFromBsrCycles);
            _output.Wait(Return);
            ChangeNoiseLoudness();
            yield return WriteWhistle(CallWhistleFromBsrCycles);
            _output.Wait(Return);
            GlideWhistle();
            yield return WriteWhistle(CallWhistleFromBsrCycles);
            _output.Wait(Return);

            // TRIFRQ: the whistle's end, which may end the whole noise, and a write.
            if (!TestWhistleEnd()) yield break;

            yield return WriteWhistle(CallWhistleFromBsrCycles);
            _output.Wait(Return);

            // NNW: a whistle waiting for its loudness is started, and the loop goes round.
            StartWhistle();
            _output.Wait(LoopBackCycles);
        }
    }

    /// <summary>The noise's timer: when it runs out, the next noise level is taken (<c>RNT</c>).</summary>
    private void RunNoiseTimer()
    {
        _output.Wait(NoiseTimerCycles);
        _noisePitchLeft--;
        if (_noisePitchLeft != 0)
        {
            _output.Wait(NoiseTimerWaitingCycles);
            return;
        }

        _output.Wait(NoiseTimerReloadCycles);
        _noisePitchLeft = _noisePitchReload;
        _noiseOut = (byte)(_noiseNext >> 1);
        _noiseCycles++;
    }

    /// <summary>After enough cycles the noise's loudness steps on (<c>RNA</c>).</summary>
    private void ChangeNoiseLoudness()
    {
        _output.Wait(NoiseLoudnessCycles);
        if (_noiseCyclesPerChange != _noiseCycles)
        {
            _output.Wait(NoiseLoudnessWaitingCycles);
            return;
        }

        _output.Wait(NoiseLoudnessChangeCycles);
        _noiseCycles = 0;
        _noiseLoudness = (byte)(_noiseLoudness - _noiseLoudnessChange);
    }

    /// <summary>After enough passes the whistle's pitch glides on (<c>TRICNT</c>).</summary>
    private void GlideWhistle()
    {
        _output.Wait(WhistleCountCycles);
        _whistleCount--;
        if (_whistleCount != 0)
        {
            _output.Wait(WhistleCountWaitingCycles +
                         (_noiseLoudness != 0 ? Return : WhistleCountSilentCycles + Return));
            return;
        }

        _output.Wait(WhistleGlideCycles + Return);
        _whistleCount = _whistleCountReload;
        _whistlePitch = (byte)(_whistlePitch + _whistleGlide);
    }

    /// <summary>
    ///     A whistle that has glided down to its lowest pitch ends and the next is loaded; otherwise the noise
    ///     ends when it has faded to nothing (<c>TRIFRQ</c>).
    /// </summary>
    /// <returns>False when the noise is over.</returns>
    private bool TestWhistleEnd()
    {
        _output.Wait(WhistleEndTestCycles);
        if (_whistlePitch == _whistleLowestPitch)
        {
            _output.Wait(WhistleOffCycles);
            _whistlePitch = 0;
            _whistleGlide = 0;
            _whistleValue = 0;
            LoadWhistle();
            return true;
        }

        _output.Wait(WhistleGoingCycles);
        if (_noiseLoudness == 0) return false;

        _output.Wait(Return);
        return true;
    }

    /// <summary>A whistle waiting for the noise to reach its loudness starts (<c>NNW</c>, <c>WINIT</c>).</summary>
    private void StartWhistle()
    {
        _output.Wait(WhistleStartTestCycles);
        if (_whistleLoudness == 0)
        {
            _output.Wait(NoWhistleWaitingCycles);
        }
        else if (_whistleLoudness != _noiseLoudness)
        {
            _output.Wait(WhistleNotDueCycles);
        }
        else
        {
            _output.Wait(WhistleStartCycles);
            _whistleLoudness = 0;
            _whistlePitch = _whistleStartPitch;
            _whistleGlide = _whistleStartGlide;
        }
    }

    /// <summary>
    ///     Writes the whistle's level plus the noise (<c>TRIDR</c>): the whistle's running value grows by its
    ///     pitch, folds back on itself when it passes the top bit (a triangle wave), and the noise is added.
    /// </summary>
    /// <param name="callCycles">The call: <c>JSR</c> or <c>BSR</c>.</param>
    /// <returns>The output change.</returns>
    private OutputChange WriteWhistle(int callCycles)
    {
        _output.Wait(callCycles + WhistleStepCycles);
        _whistleValue = (byte)(_whistleValue + _whistlePitch);
        var level = _whistleValue;
        if ((level & SignBit) != 0)
        {
            _output.Wait(Inherent);
            level = (byte)~level;
        }

        _output.Wait(WhistleAddNoiseCycles);
        return _output.Store((byte)(level + _noiseOut));
    }
}
