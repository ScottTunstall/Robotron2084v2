using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// The board's wave table synthesiser: it copies a wave shape into memory and plays it over and over,
/// waiting a little between each level, with the wait taken from a pattern of pitches. Each pass through
/// the pattern can echo, quieter each time, and then move every pitch up or down and start again.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routines <c>GWLD</c> ("GWAVE LOADER"), <c>GWAVE</c>,
/// <c>WVTRAN</c> ("WAVE TRANSFER ROUTINE") and <c>WVDECA</c> ("WAVE DECAY ROUTINE").</item>
/// <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
/// </list>
/// The settings live in the board's memory between sounds, so the laser ball bonus can pick up where the
/// last one stopped (<c>BON2</c>'s <c>JMP GEND50</c>).
/// </remarks>
internal sealed class WaveTableSound
{
    /// <summary>
    /// <c>GWLD</c> up to the echo count: multiply the sound's place by 7 (<c>TAB</c>, <c>ASLB</c>, three
    /// <c>ABA</c>), find its settings (<c>LDX #SVTAB</c>, <c>ADDX</c>), then split byte 0 (<c>LDAA</c>,
    /// <c>TAB</c>, <c>ANDA</c>, <c>STAA</c>, four <c>LSRB</c>, <c>STAB</c>).
    /// </summary>
    private const int LoadSettingsCycles =
        (5 * Inherent) + WordImmediate + SubroutineCycles.CallAddToIndex + Indexed + Inherent + Immediate + StoreDirect + (4 * Inherent) + StoreDirect;

    /// <summary>
    /// <c>GWLD</c> splitting byte 1 (<c>LDAA 1,X</c>, <c>TAB</c>, four <c>LSRB</c>, <c>STAB</c>, <c>ANDA</c>,
    /// <c>STAA TEMPA</c>) and getting ready to find the wave (<c>STX TEMPX</c>, <c>LDX #GWVTAB</c>).
    /// </summary>
    private const int LoadWaveNumberCycles =
        Indexed + Inherent + (4 * Inherent) + StoreDirect + Immediate + StoreDirect + WordStoreDirect + WordImmediate;

    /// <summary><c>GWLD2</c> checking whether it has reached the wave yet (<c>DEC TEMPA</c>, <c>BMI</c>).</summary>
    private const int WaveSearchCheckCycles = ModifyExtended + Branch;

    /// <summary><c>GWLD2</c> stepping over one wave (<c>LDAA ,X</c>, <c>INCA</c>, <c>JSR ADDX</c>, <c>BRA</c>).</summary>
    private const int WaveSearchStepCycles = Indexed + Inherent + SubroutineCycles.CallAddToIndex + Branch;

    /// <summary><c>GWLD3</c> keeping the wave's place and calling the transfer (<c>STX GWFRM</c>, <c>JSR WVTRAN</c>).</summary>
    private const int CallTransferFromLoadCycles = WordStoreDirect + CallExtended;

    /// <summary><c>GWLD</c> fetching the first decay and calling the decay routine (<c>LDX</c>, <c>LDAA 2,X</c>, <c>STAA</c>, <c>JSR WVDECA</c>).</summary>
    private const int CallPreDecayFromLoadCycles = WordDirect + Indexed + StoreDirect + CallExtended;

    /// <summary>
    /// The rest of <c>GWLD</c>: the pitch settings (<c>LDX</c>, then <c>LDAA</c> and <c>STAA</c> twice),
    /// the pattern's start and end (<c>LDAA</c>, <c>TAB</c>, <c>LDAA</c>, <c>LDX #GFRTAB</c>, <c>ADDX</c>,
    /// <c>TBA</c>, <c>STX</c>, <c>CLR FOFSET</c>, <c>ADDX</c>, <c>STX</c>) and <c>RTS</c>.
    /// </summary>
    private const int LoadPatternCycles =
        WordDirect + (2 * (Indexed + StoreDirect)) + Indexed + Inherent + Indexed + WordImmediate + SubroutineCycles.CallAddToIndex
        + Inherent + WordStoreDirect + ModifyExtended + SubroutineCycles.CallAddToIndex + WordStoreDirect + Return;

    /// <summary>
    /// <c>WVTRAN</c> without the bytes it copies: <c>LDX</c>, <c>STX</c>, <c>LDX</c>, <c>LDAB ,X</c>, <c>INX</c>,
    /// <c>JSR TRANS</c> and <c>TRANS</c> itself, then <c>LDX</c>, <c>STX WVEND</c>, <c>RTS</c>.
    /// </summary>
    private const int TransferCycles =
        WordImmediate + WordStoreDirect + WordDirect + Indexed + IndexStep + CallExtended + SubroutineCycles.Transfer
        + WordDirect + WordStoreDirect + Return;

    /// <summary><c>WVDECA</c> when there is nothing to take off: <c>TSTA</c>, <c>BEQ</c>, <c>RTS</c>.</summary>
    private const int NoDecayCycles = Inherent + Branch + Return;

    /// <summary>
    /// <c>WVDECA</c> without its loop: <c>TSTA</c>, <c>BEQ</c>, <c>LDX</c>, <c>STX</c>, <c>LDX #GWTAB</c>,
    /// <c>STAA TEMPB</c>, and the <c>RTS</c> at <c>WVDCX</c>.
    /// </summary>
    private const int DecayCycles = Inherent + Branch + WordDirect + WordStoreDirect + WordImmediate + StoreDirect + Return;

    /// <summary>
    /// <c>WVDLP</c> for one level, besides the take-offs: <c>STX</c>, <c>LDX</c>, <c>LDAB</c>, <c>STAB</c>,
    /// <c>LDAB 1,X</c>, four <c>LSRB</c>, <c>INX</c>, <c>STX</c>, <c>LDX</c>, <c>LDAA ,X</c>, then <c>STAA ,X</c>,
    /// <c>INX</c>, <c>CPX</c>, <c>BNE</c>.
    /// </summary>
    private const int DecayPerLevelCycles =
        WordStoreDirect + WordDirect + Direct + StoreDirect + Indexed + (4 * Inherent) + IndexStep + WordStoreDirect + WordDirect + Indexed
        + StoreIndexed + IndexStep + WordDirect + Branch;

    /// <summary><c>WVDLP1</c> taking one sixteenth off a level: <c>SBA</c>, <c>DEC TEMPA</c>, <c>BNE</c>.</summary>
    private const int DecayStepCycles = Inherent + ModifyExtended + Branch;

    /// <summary>How far a wave's level is shifted to get the sixteenth that a decay step takes off it (four <c>LSRB</c>).</summary>
    private const int SixteenthShift = 4;

    /// <summary><c>GWAVE</c> starting the echo count: <c>LDAA GECHO</c>, <c>STAA GECNT</c>.</summary>
    private const int StartEchoesCycles = Direct + StoreDirect;

    /// <summary><c>GWT4</c> going back to the pattern's start: <c>LDX GWFRQ</c>, <c>STX XPLAY</c>.</summary>
    private const int StartPatternCycles = WordDirect + WordStoreDirect;

    /// <summary><c>GPLAY</c> reading the next pitch and checking for the pattern's end: <c>LDX</c>, <c>LDAA ,X</c>, <c>ADDA</c>, <c>STAA</c>, <c>CPX</c>, <c>BEQ</c>.</summary>
    private const int ReadPitchCycles = WordDirect + Indexed + Direct + StoreDirect + WordDirect + Branch;

    /// <summary><c>GPLAY</c> getting ready to play at that pitch: <c>LDAB GCCNT</c>, <c>INX</c>, <c>STX</c>.</summary>
    private const int StartPitchCycles = Direct + IndexStep + WordStoreDirect;

    /// <summary><c>GOUT</c> pointing at the wave's first level: <c>LDX #GWTAB</c>.</summary>
    private const int StartWaveCycles = WordImmediate;

    /// <summary>Getting one level of the wave ready, besides the wait: <c>LDAA GPER</c> and <c>LDAA ,X</c>.</summary>
    private const int ReadLevelCycles = Direct + Indexed;

    /// <summary><c>GPRLP</c> waiting one count: <c>DECA</c>, <c>BNE</c>.</summary>
    private const int WaitCountCycles = Inherent + Branch;

    /// <summary><c>GPR1</c> moving to the next level: <c>INX</c>, <c>CPX WVEND</c>, <c>BNE</c>.</summary>
    private const int NextLevelCycles = IndexStep + WordDirect + Branch;

    /// <summary>Counting one play of the wave: <c>DECB</c>, <c>BEQ</c>.</summary>
    private const int CountPlayCycles = Inherent + Branch;

    /// <summary>The source's padding before the wave plays again, so each play lasts the same ("SYNC 36"), and <c>BRA GOUT</c>.</summary>
    private const int ReplaySyncCycles = (4 * 2 * IndexStep) + (2 * Inherent) + Branch;

    /// <summary><c>GEND</c> calling the echo decay: <c>LDAA GECDEC</c>, <c>BSR WVDECA</c>.</summary>
    private const int CallEchoDecayCycles = Direct + CallShort;

    /// <summary><c>GEND40</c> counting an echo: <c>DEC GECNT</c>, <c>BNE</c>.</summary>
    private const int CountEchoCycles = ModifyExtended + Branch;

    /// <summary>Checking a flag and branching on it: <c>LDAA</c> and <c>BNE</c> or <c>BEQ</c>.</summary>
    private const int CheckFlagCycles = Direct + Branch;

    /// <summary><c>GEND50</c> counting a pitch step: <c>DEC GDCNT</c>, <c>BEQ</c>.</summary>
    private const int CountPitchStepCycles = ModifyExtended + Branch;

    /// <summary><c>GEND50</c>/<c>GEND60</c> moving the pitch: <c>ADDA FOFSET</c>, <c>STAA FOFSET</c>.</summary>
    private const int MovePitchCycles = Direct + StoreDirect;

    /// <summary><c>GEND61</c> getting ready to search the pattern: <c>LDX GWFRQ</c>, <c>CLRB</c>.</summary>
    private const int StartSearchCycles = WordDirect + Inherent;

    /// <summary><c>GW0</c> reading which way the pitch moves: <c>LDAA FOFSET</c>, <c>TST GDFINC</c>, <c>BMI</c>.</summary>
    private const int CheckDirectionCycles = Direct + ModifyExtended + Branch;

    /// <summary>Adding the move to one pitch and testing the sum: <c>ADDA ,X</c> and a branch.</summary>
    private const int TestPitchCycles = Indexed + Branch;

    /// <summary><c>GW2</c> or <c>GW2A</c> checking whether the start has been found: <c>TSTB</c> and a branch.</summary>
    private const int CheckFoundCycles = Inherent + Branch;

    /// <summary><c>GW2A</c> marking the start: <c>STX GWFRQ</c>, <c>INCB</c>.</summary>
    private const int MarkStartCycles = WordStoreDirect + Inherent;

    /// <summary><c>GW2B</c> moving to the next pitch: <c>INX</c>, <c>CPX FRQEND</c>, <c>BNE</c>.</summary>
    private const int NextPitchCycles = IndexStep + WordDirect + Branch;

    /// <summary><c>GW3</c> marking the end and checking whether to copy the wave again: <c>STX FRQEND</c>, <c>LDAA GECDEC</c>, <c>BEQ</c>.</summary>
    private const int MarkEndCycles = WordStoreDirect + Direct + Branch;

    /// <summary>The top bit: a pitch move above 127 lowers the waits instead of raising them. <see cref="BoardMemory.ScratchA"/> is compared with this to tell whether its top bit is set.</summary>
    private const int NegativeBit = 0x80;

    /// <summary>The bottom four bits of a settings byte. It picks the low half of the settings byte, which becomes <see cref="_playsPerPitch"/>.</summary>
    private const int LowNibble = 0x0F;

    /// <summary>How far the top four bits of a settings byte are shifted down. The settings byte is shifted down by this many bits to give its high half, which becomes <see cref="_echoes"/>.</summary>
    private const int HighNibbleShift = 4;

    private readonly BoardMemory _memory;
    private readonly BoardOutput _output;
    private readonly byte[] _waveSamples = new byte[WaveTableData.LongestWave];
    private byte _echoes;
    private byte _playsPerPitch;
    private byte _echoDecay;
    private byte _pitchStep;
    private byte _pitchStepsLeft;
    private byte _preDecay;
    private byte _pitchOffset;
    private int _patternStart;
    private int _patternEnd;
    private int _waveStart;
    private int _waveLength;

    /// <summary>Creates the synthesiser, sharing the board's memory and output port.</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    public WaveTableSound(BoardMemory memory, BoardOutput output)
    {
        _memory = memory;
        _output = output;
    }

    /// <summary>Loads a sound's settings and wave, then plays it (<c>JSR GWLD</c>, <c>JSR GWAVE</c>).</summary>
    /// <param name="vectorIndex">The sound's place in <see cref="WaveTableData.Vectors"/>.</param>
    /// <returns>The sound's output changes.</returns>
    public IEnumerable<OutputChange> LoadAndPlay(int vectorIndex)
    {
        _output.Wait(CallExtended);
        Load(vectorIndex);
        _output.Wait(CallExtended);
        return Play();
    }

    /// <summary>
    /// The laser ball bonus (<c>BON2</c>): the first send plays the bonus sound once; each send after it,
    /// while nothing else is sent, moves the pitch one step and plays it again.
    /// </summary>
    /// <returns>The sound's output changes.</returns>
    public IEnumerable<OutputChange> PlayLaserBallBonus()
    {
        _output.Wait(CheckFlagCycles);
        if (_memory.IsLaserBallBonusRepeating)
        {
            _output.Wait(JumpExtended);
            return MovePitchThenPlay();
        }

        _output.Wait(ModifyExtended + Immediate + CallShort);
        _memory.IsLaserBallBonusRepeating = true;
        Load(WaveTableData.LaserBallBonusVector);
        _output.Wait(Branch);
        return Play();
    }

    /// <summary>Loads a sound's settings, copies its wave into memory and makes it quieter if asked (<c>GWLD</c>).</summary>
    /// <param name="vectorIndex">The sound's place in <see cref="WaveTableData.Vectors"/>.</param>
    private void Load(int vectorIndex)
    {
        _output.Wait(LoadSettingsCycles + LoadWaveNumberCycles);
        WaveTableVector vector = WaveTableData.Vectors[vectorIndex];
        _playsPerPitch = (byte)(vector.EchoesAndPlays & LowNibble);
        _echoes = (byte)(vector.EchoesAndPlays >> HighNibbleShift);
        _echoDecay = (byte)(vector.EchoDecayAndWave >> HighNibbleShift);
        FindWave(vector.EchoDecayAndWave & LowNibble);
        _output.Wait(CallTransferFromLoadCycles);
        CopyWave();
        _output.Wait(CallPreDecayFromLoadCycles);
        _preDecay = vector.PreDecay;
        Decay(_preDecay);
        _output.Wait(LoadPatternCycles);
        _pitchStep = vector.PitchStep;
        _pitchStepsLeft = vector.PitchSteps;
        _patternStart = vector.PatternStart;
        _patternEnd = vector.PatternStart + vector.PatternLength;
        _pitchOffset = 0;
    }

    /// <summary>Steps over the waves before the one wanted, counting down in <c>TEMPA</c> (<c>GWLD2</c>).</summary>
    /// <param name="waveNumber">The wave's number, counting from 0.</param>
    private void FindWave(int waveNumber)
    {
        _waveStart = 0;
        _memory.ScratchA = (byte)waveNumber;
        while (true)
        {
            _output.Wait(WaveSearchCheckCycles);
            _memory.ScratchA--;
            if (_memory.ScratchA >= NegativeBit)
            {
                return;
            }

            _output.Wait(WaveSearchStepCycles);
            _waveStart += WaveTableData.Waves[_waveStart] + 1;
        }
    }

    /// <summary>Copies the wave from the table into memory, where the decays can change it (<c>WVTRAN</c>).</summary>
    private void CopyWave()
    {
        _waveLength = WaveTableData.Waves[_waveStart];
        _output.Wait(TransferCycles + (_waveLength * SubroutineCycles.TransferPerByte));
        for (int i = 0; i < _waveLength; i++)
        {
            _waveSamples[i] = WaveTableData.Waves[_waveStart + 1 + i];
        }
    }

    /// <summary>
    /// Makes the wave in memory quieter: each level loses a sixteenth of the table's level, as many times as
    /// asked (<c>WVDECA</c>).
    /// </summary>
    /// <param name="steps">How many sixteenths to take off; 0 leaves the wave alone.</param>
    private void Decay(byte steps)
    {
        if (steps == 0)
        {
            _output.Wait(NoDecayCycles);
            return;
        }

        _output.Wait(DecayCycles + (_waveLength * (DecayPerLevelCycles + (steps * DecayStepCycles))));
        _memory.ScratchB = steps;
        _memory.ScratchA = 0;
        for (int i = 0; i < _waveLength; i++)
        {
            int sixteenth = WaveTableData.Waves[_waveStart + 1 + i] >> SixteenthShift;
            _waveSamples[i] = (byte)(_waveSamples[i] - (steps * sixteenth));
        }
    }

    /// <summary>
    /// Plays the pattern with all its echoes, then moves the pitch and plays it again for as long as the
    /// settings ask (<c>GWAVE</c>). The laser ball bonus stops after the echoes.
    /// </summary>
    /// <returns>The sound's output changes.</returns>
    private IEnumerable<OutputChange> Play()
    {
        do
        {
            foreach (OutputChange change in PlayEchoes())
            {
                yield return change;
            }

            _output.Wait(CheckFlagCycles);
            if (_memory.IsLaserBallBonusRepeating)
            {
                yield break;
            }
        }
        while (MovePitch());
    }

    /// <summary>Moves the pitch, then plays on if the pattern can still be played (<c>GEND50</c> then <c>GWAVE</c>).</summary>
    /// <returns>The sound's output changes.</returns>
    private IEnumerable<OutputChange> MovePitchThenPlay() => MovePitch() ? Play() : [];

    /// <summary>Plays the pattern once for each echo, making the wave quieter after each (<c>GWT4</c> to <c>GEND40</c>).</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> PlayEchoes()
    {
        _output.Wait(StartEchoesCycles);
        byte echoesLeft = _echoes;
        do
        {
            _output.Wait(StartPatternCycles);
            foreach (OutputChange change in PlayPattern())
            {
                yield return change;
            }

            _output.Wait(CallEchoDecayCycles);
            Decay(_echoDecay);
            _output.Wait(CountEchoCycles);
            echoesLeft--;
        }
        while (echoesLeft != 0);
    }

    /// <summary>Plays the wave at each pitch of the pattern in turn (<c>GPLAY</c>).</summary>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> PlayPattern()
    {
        for (int place = _patternStart; ; place++)
        {
            _output.Wait(ReadPitchCycles);
            if (place == _patternEnd)
            {
                yield break;
            }

            var wait = (byte)(WaveTableData.Patterns[place] + _pitchOffset);
            _output.Wait(StartPitchCycles);
            foreach (OutputChange change in PlayAtPitch(wait))
            {
                yield return change;
            }
        }
    }

    /// <summary>Plays the whole wave as many times as the settings ask, at one pitch (<c>GOUT</c>).</summary>
    /// <param name="wait">The wait between levels, in counts of <see cref="WaitCountCycles"/>.</param>
    /// <returns>The output changes.</returns>
    private IEnumerable<OutputChange> PlayAtPitch(byte wait)
    {
        int cyclesBeforeEachLevel = ReadLevelCycles + (CountdownLoop.Runs(wait) * WaitCountCycles);
        byte playsLeft = _playsPerPitch;
        while (true)
        {
            _output.Wait(StartWaveCycles);
            for (int i = 0; i < _waveLength; i++)
            {
                _output.Wait(cyclesBeforeEachLevel);
                yield return _output.Store(_waveSamples[i]);
                _output.Wait(NextLevelCycles);
            }

            _output.Wait(CountPlayCycles);
            playsLeft--;
            if (playsLeft == 0)
            {
                yield break;
            }

            _output.Wait(ReplaySyncCycles);
        }
    }

    /// <summary>
    /// Moves every pitch by the step, if the settings move it and have steps left, and finds the part of
    /// the pattern that can still be played (<c>GEND50</c> to <c>GEND0</c>).
    /// </summary>
    /// <returns>True when there is something left to play.</returns>
    private bool MovePitch()
    {
        _output.Wait(CheckFlagCycles);
        if (_pitchStep == 0)
        {
            _output.Wait(Return);
            return false;
        }

        _output.Wait(CountPitchStepCycles);
        _pitchStepsLeft--;
        if (_pitchStepsLeft == 0)
        {
            _output.Wait(Return);
            return false;
        }

        _output.Wait(MovePitchCycles);
        _pitchOffset += _pitchStep;
        return FindPlayableRange();
    }

    /// <summary>
    /// Finds the run of pitches that the moved offset does not push past the top or the bottom, and makes it
    /// the pattern; then copies a fresh wave if echoes made it quieter (<c>GEND61</c> to <c>GEND0</c>).
    /// </summary>
    /// <returns>True when some pitch can still be played.</returns>
    private bool FindPlayableRange()
    {
        _output.Wait(StartSearchCycles);
        int? end = SearchPattern();
        if (end is null)
        {
            _output.Wait(Return);
            return false;
        }

        _output.Wait(MarkEndCycles);
        _patternEnd = end.Value;
        if (_echoDecay != 0)
        {
            _output.Wait(CallShort);
            CopyWave();
            _output.Wait(Direct + CallShort);
            Decay(_preDecay);
        }

        _output.Wait(JumpExtended);
        return true;
    }

    /// <summary>Walks the pattern, marking the first playable pitch as the start (<c>GW0</c> to <c>GW2B</c>).</summary>
    /// <returns>Where the playable run ends, or null when no pitch is playable.</returns>
    private int? SearchPattern()
    {
        bool hasStart = false;
        for (int place = _patternStart; ; place++)
        {
            _output.Wait(CheckDirectionCycles);
            bool isPlayable = IsPlayable(WaveTableData.Patterns[place]);
            _output.Wait(CheckFoundCycles);
            if (!isPlayable && hasStart)
            {
                _output.Wait(Branch);
                return place;
            }

            if (isPlayable && !hasStart)
            {
                _output.Wait(MarkStartCycles);
                _patternStart = place;
                hasStart = true;
            }

            _output.Wait(NextPitchCycles);
            if (place + 1 != _patternEnd)
            {
                continue;
            }

            _output.Wait(CheckFoundCycles);
            return hasStart ? place + 1 : null;
        }
    }

    /// <summary>
    /// True when a pitch, moved by the offset, still fits in a byte: rising, it must not carry past 255;
    /// falling, it must not reach 0 or go below it (<c>GW0</c>/<c>GW1</c>).
    /// </summary>
    /// <param name="pitch">The pattern's pitch.</param>
    private bool IsPlayable(byte pitch)
    {
        int sum = _pitchOffset + pitch;
        bool hasCarry = sum > byte.MaxValue;
        _output.Wait(TestPitchCycles);
        if (_pitchStep < NegativeBit)
        {
            if (!hasCarry)
            {
                _output.Wait(Branch);
            }

            return !hasCarry;
        }

        if ((byte)sum == 0)
        {
            return false;
        }

        _output.Wait(Branch);
        return hasCarry;
    }
}
