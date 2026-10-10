using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The board's three-oscillator generator with a noise source: three square waves, each with its own
///     pitch and size, are added together with random noise, and a decay stage shifts the whole level down
///     a step at a time until the sound dies.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c>, routines <c>PLAY</c> ("THREE OSCILLATOR SOUND GENERATOR"),
///             <c>RDECAY</c>/<c>DECAYZ</c> ("ECHO AND DECAY ROUTINE") and <c>TRANS</c>, reached through <c>SND5</c>
///             and the settings <c>VEC05</c>.
///         </item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
///     Robotron uses this generator for one sound, the second of the eight coin sounds (sound number <c>$2D</c>,
///     through <c>JMPTB1</c>). The source keeps each oscillator's settings in 28 bytes copied from the vector
///     into the board's memory, in seven groups of four (one byte for each of the three oscillators and the
///     noise); the port keeps them as seven groups the same way.
/// </remarks>
internal sealed class OscillatorSound
{
    /// <summary>The oscillators, and the noise as the fourth (the four bytes of each group).</summary>
    private const int Voices = 4;

    /// <summary>The noise voice's place in a group (<c>FREQ4</c>, <c>DELTA4</c>, ...).</summary>
    private const int NoiseVoice = 3;

    /// <summary>The bytes copied from the vector (<c>LDAB #28</c>).</summary>
    private const int VectorBytes = 28;

    /// <summary>
    ///     Where each group starts in the vector: <c>FREQ1</c>, <c>DELTA1</c>, <c>FREQ1$</c>, <c>CYCLE1</c>,
    ///     <c>DFREQ1</c>, <c>EFREQ1</c>, <c>CYCL1$</c>.
    /// </summary>
    private const int FrequencyGroup = 0;

    private const int AmplitudeGroup = Voices;

    private const int CounterGroup = 2 * Voices;

    private const int CyclesGroup = 3 * Voices;

    private const int FrequencyStepGroup = 4 * Voices;

    private const int EndFrequencyGroup = 5 * Voices;

    private const int CycleCounterGroup = 6 * Voices;

    /// <summary>The level of silence the sum starts from (<c>LDAA #$80</c>): the middle of the output's range.</summary>
    private const byte SilentLevel = 0x80;

    /// <summary>The sign bit, which marks the noise voice as "white" when its pitch byte has it set.</summary>
    private const byte SignBit = 0x80;

    /// <summary>The mask that strips it (<c>ANDB #$7F</c>).</summary>
    private const byte NotSignBit = 0x7F;

    /// <summary>
    ///     The shift at which the decay ends: <c>RDECAY</c> is eight shifts above <c>DECAYZ</c>, and <c>CPX #RDECAY+1</c>
    ///     stops the sound when the pointer reaches seven.
    /// </summary>
    private const int MostShifts = 7;

    /// <summary>What the next random number adds to three times the last (<c>ADDB #$0B</c>).</summary>
    private const byte RandomAdd = 0x0B;

    /// <summary>
    ///     <c>SND5</c> up to the start of <c>PLAY</c>: <c>LDX</c>, <c>BRA</c>, <c>LDAB</c>, <c>JSR TRANS</c> and
    ///     <c>TRANS</c> itself, <c>JSR PLAY</c>.
    /// </summary>
    private const int CopyAndCallCycles =
        WordImmediate + Branch + Immediate + CallExtended + SubroutineCycles.Transfer +
        VectorBytes * SubroutineCycles.TransferPerByte + CallExtended;

    /// <summary><c>PLAY</c> starting: <c>STX XPLAY</c>, <c>LDX #DECAYZ</c>, <c>STX XDECAY</c>, <c>LDAA #$80</c>.</summary>
    private const int StartCycles = WordStoreDirect + WordImmediate + WordStoreDirect + Immediate;

    /// <summary><c>PLAY1</c> reading the noise voice's pitch and branching: <c>LDAB FREQ4</c>, <c>BPL</c>.</summary>
    private const int CheckNoiseModeCycles = Direct + Branch;

    /// <summary>
    ///     The white mode's extra wait: <c>LDAB RANDOM</c>, three <c>LSRB</c>, <c>INCB</c>; then each count is
    ///     <c>DECB</c>, <c>BNE</c>.
    /// </summary>
    private const int WhiteWaitSetUpCycles = Direct + 3 * Inherent + Inherent;

    private const int WhiteWaitCountCycles = Inherent + Branch;

    /// <summary>Counting one voice down: <c>DEC FREQn$</c>, <c>BEQ</c>.</summary>
    private const int CountVoiceCycles = ModifyExtended + Branch;

    /// <summary>The noise voice running out and its pitch being read: <c>LDAB FREQ4</c>, <c>BEQ</c>.</summary>
    private const int CheckNoisePitchCycles = Direct + Branch;

    /// <summary>Reloading the noise voice: <c>ANDB #$7F</c>, <c>STAB FREQ4$</c>.</summary>
    private const int ReloadNoiseCycles = Immediate + StoreDirect;

    /// <summary>The next random number: <c>LDAB</c>, <c>ASLB</c>, <c>ADDB</c>, <c>ADDB #</c>, <c>STAB</c>.</summary>
    private const int NextRandomCycles = Direct + Inherent + Direct + Immediate + StoreDirect;

    /// <summary>Counting the noise's decay: <c>DEC CYCL4$</c>, <c>BNE</c>.</summary>
    private const int CountDecayCycles = ModifyExtended + Branch;

    /// <summary>
    ///     The decay stepping: <c>LDAB CYCLE4</c>, <c>STAB CYCL4$</c>, <c>LDX XDECAY</c>, <c>DEX</c>, <c>CPX #</c>,
    ///     <c>BEQ</c>.
    /// </summary>
    private const int StepDecayCycles = Direct + StoreDirect + WordDirect + IndexStep + WordImmediate + Branch;

    /// <summary>Saving the new decay pointer: <c>STX XDECAY</c>.</summary>
    private const int SaveDecayCycles = WordStoreDirect;

    /// <summary>
    ///     <c>PLAY6</c> picking a noise amplitude: <c>LDAB RANDOM</c>, <c>BMI</c>, <c>ANDB DELTA4</c>, <c>ANDB #$7F</c>,
    ///     then either <c>BRA</c> or <c>NEGB</c>.
    /// </summary>
    private const int NoiseAmplitudeCycles = Direct + Branch + Direct + Immediate;

    /// <summary>The positive case's <c>BRA PLAY6B</c>, and the negative case's <c>NEGB</c>.</summary>
    private const int PositiveBranchCycles = Branch;

    private const int NegateCycles = Inherent;

    /// <summary><c>PLAY6B</c> adding the noise to the level: <c>PSHA</c>, <c>ABA</c>, <c>TAB</c>, <c>PULA</c>.</summary>
    private const int AddNoiseCycles = Stack + Inherent + Inherent + Stack;

    /// <summary>
    ///     Calling the decay: <c>LDX XDECAY</c>, <c>JSR 0,X</c>; and each shift, then the store and <c>RTS</c> (
    ///     <c>DECAYZ</c>).
    /// </summary>
    private const int CallDecayCycles = WordDirect + CallShort;

    /// <summary>After the decay returns: <c>BRA PLAY1</c> or <c>JMP PLAY1</c>.</summary>
    private const int NoiseLoopBackCycles = Branch;

    private const int OscillatorLoopBackCycles = JumpExtended;

    /// <summary>
    ///     An oscillator running out: <c>LDX #FREQn</c> (and <c>BRA PLAY10</c> for the first two), <c>TST 24,X</c>,
    ///     <c>BEQ</c>.
    /// </summary>
    private const int SelectVoiceCycles = WordImmediate;

    private const int SelectVoiceBranchCycles = Branch;

    private const int CheckCyclesCycles = ModifyIndexed + Branch;

    /// <summary><c>PLAY10</c> counting the pitch's cycles down: <c>DEC 24,X</c>, <c>BNE</c>.</summary>
    private const int CountCyclesCycles = ModifyIndexed + Branch;

    /// <summary>
    ///     <c>PLAY10</c> moving the pitch: <c>LDAB 12,X</c>, <c>STAB 24,X</c>, <c>LDAB 0,X</c>, <c>ADDB 16,X</c>,
    ///     <c>CMPB 20,X</c>, <c>BEQ</c>, <c>STAB 0,X</c>.
    /// </summary>
    private const int MovePitchCycles = Indexed + StoreIndexed + Indexed + Indexed + Indexed + Branch + StoreIndexed;

    /// <summary>
    ///     <c>PLAY11</c> making the next level: <c>LDAB 0,X</c>, <c>STAB 8,X</c>, <c>ADDA 4,X</c>, <c>NEG 4,X</c>,
    ///     <c>TAB</c>.
    /// </summary>
    private const int NextLevelCycles = Indexed + StoreIndexed + Indexed + ModifyIndexed + Inherent;

    /// <summary><c>PLAY12</c> finishing: <c>LDX XPLAY</c>, <c>RTS</c>.</summary>
    private const int EndCycles = WordDirect + Return;

    /// <summary>Bits the random number is shifted to give the white mode's wait (<c>LSRB</c> three times).</summary>
    private const int RandomShift = 3;

    /// <summary><c>VEC01</c> (<c>THNDR</c>), <c>VEC02</c>, <c>VEC016</c> and <c>VEC017</c>, as the source writes them.</summary>
    private static readonly byte[] Vec01 = FromWords(0xFFFF, 0xFF90, 0xFFFF, 0xFFFF, 0xFFFF, 0xFF90, 0xFFFF, 0xFFFF,
        0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000);

    private static readonly byte[] Vec02 = FromWords(0x4801, 0x0000, 0x3F3F, 0x0000, 0x4801, 0x0000, 0x0108, 0x0000,
        0x8101, 0x0000, 0x01FF, 0x0000, 0x0108, 0x0000);

    private static readonly byte[] Vec016 = FromWords(0x0104, 0x0000, 0x3F7F, 0x0000, 0x0104, 0x0000, 0x05FF, 0x0000,
        0x0100, 0x0000, 0x4800, 0x0000, 0x05FF, 0x0000);

    private static readonly byte[] Vec017 = FromWords(0x0280, 0x0030, 0x0A7F, 0x007F, 0x0280, 0x0030, 0xC080, 0x0020,
        0x0110, 0x0015, 0xC010, 0x0000, 0xC080, 0x0000);

    /// <summary><c>SND5</c>'s settings (<c>VEC05</c>): a low buzz that is mostly noise, and a noise that fades.</summary>
    private static readonly byte[] Vec05 =
    [
        0x04, 0x00, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x7F, 0x04, 0x00,
        0x00, 0x04, 0xFF, 0x00, 0x00, 0xA0, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0xA0
    ];

    private readonly BoardMemory _memory;
    private readonly BoardOutput _output;
    private readonly byte[] _ram;
    private byte _level = SilentLevel;
    private int _shifts; // XDECAY: how far the decay's pointer has moved up from the bare store

    /// <summary>Creates the sound on the board's memory and output port, with its own copy of the settings.</summary>
    /// <param name="memory">The board's lasting variables, whose <c>LO</c> is the noise's random number.</param>
    /// <param name="output">The board's output port.</param>
    /// <param name="vector">The 28 bytes of settings.</param>
    private OscillatorSound(BoardMemory memory, BoardOutput output, byte[] vector)
    {
        _memory = memory;
        _output = output;
        _ram = (byte[])vector.Clone();
    }

    /// <summary>Turns the source's <c>FDB</c> words into the bytes they put in the ROM.</summary>
    /// <param name="words">The words.</param>
    /// <returns>The bytes, high byte first.</returns>
    private static byte[] FromWords(params ushort[] words)
    {
        return [.. words.SelectMany(word => new[] { (byte)(word >> 8), (byte)word })];
    }

    /// <summary>Plays the second coin sound (sound <c>SND5</c>, settings <c>VEC05</c>).</summary>
    /// <param name="memory">The board's lasting variables, whose <c>LO</c> is the noise's random number.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlaySound5(BoardMemory memory, BoardOutput output)
    {
        return Start(memory, output, Vec05, true);
    }

    /// <summary>Plays <c>SND2</c> (settings <c>VEC02</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlaySound2(BoardMemory memory, BoardOutput output)
    {
        return Start(memory, output, Vec02, true);
    }

    /// <summary>
    ///     Plays <c>THNDR</c>, "THUNDER SOUND" (settings <c>VEC01</c>); the only one of the group that is not reached by
    ///     a <c>BRA</c>.
    /// </summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayThunder(BoardMemory memory, BoardOutput output)
    {
        return Start(memory, output, Vec01, false);
    }

    /// <summary>Plays <c>SND16</c> (settings <c>VEC016</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlaySound16(BoardMemory memory, BoardOutput output)
    {
        return Start(memory, output, Vec016, true);
    }

    /// <summary>Plays <c>SND17</c> (settings <c>VEC017</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlaySound17(BoardMemory memory, BoardOutput output)
    {
        return Start(memory, output, Vec017, true);
    }

    private static IEnumerable<OutputChange> Start(BoardMemory memory, BoardOutput output, byte[] vector,
        bool isReachedByBranch)
    {
        output.Wait(isReachedByBranch ? CopyAndCallCycles : CopyAndCallCycles - Branch);
        return new OscillatorSound(memory, output, vector).Play();
    }

    /// <summary>Plays the settings round and round until an oscillator or the decay ends the sound (<c>PLAY1</c>).</summary>
    /// <returns>The sound's output changes.</returns>
    private IEnumerable<OutputChange> Play()
    {
        _output.Wait(StartCycles);
        while (true)
        {
            WaitForWhiteNoise();
            var due = CountVoices();
            if (due >= 0)
            {
                if (!ChangeOscillator(due)) yield break;

                yield return WriteLevel(_level, OscillatorLoopBackCycles);
                continue;
            }

            if (!ChangeNoise(out var noise)) yield break;

            if (noise is { } amount) yield return WriteLevel((byte)(_level + amount), NoiseLoopBackCycles);
        }
    }

    /// <summary>The white mode's extra wait, when the noise voice's pitch byte asks for it (<c>PLAY1</c> to <c>PLAY2</c>).</summary>
    private void WaitForWhiteNoise()
    {
        _output.Wait(CheckNoiseModeCycles);
        if ((_ram[FrequencyGroup + NoiseVoice] & SignBit) != 0)
        {
            var wait = (byte)((_memory.RandomLow >> RandomShift) + 1);
            _output.Wait(WhiteWaitSetUpCycles + CountdownLoop.Runs(wait) * WhiteWaitCountCycles);
        }
    }

    /// <summary>Counts the three oscillators down in turn (<c>PLAY3</c>) until one runs out.</summary>
    /// <returns>The oscillator that ran out, or -1 when none did.</returns>
    private int CountVoices()
    {
        for (var voice = 0; voice < NoiseVoice; voice++)
        {
            _output.Wait(CountVoiceCycles);
            if (--_ram[CounterGroup + voice] == 0) return voice;
        }

        return -1;
    }

    /// <summary>
    ///     An oscillator has run out: it may move on to its next pitch, and it adds its size to the level
    ///     and turns it over for next time (<c>PLAY7</c> to <c>PLAY11</c>).
    /// </summary>
    /// <param name="voice">The oscillator.</param>
    /// <returns>False when the oscillator reached its last pitch and the sound is over.</returns>
    private bool ChangeOscillator(int voice)
    {
        _output.Wait(SelectVoiceCycles + (voice < 2 ? SelectVoiceBranchCycles : 0) + CheckCyclesCycles);
        if (_ram[CycleCounterGroup + voice] != 0)
        {
            _output.Wait(CountCyclesCycles);
            if (--_ram[CycleCounterGroup + voice] == 0 && !MovePitch(voice)) return false;
        }

        _output.Wait(NextLevelCycles);
        _ram[CounterGroup + voice] = _ram[FrequencyGroup + voice];
        _level = (byte)(_level + _ram[AmplitudeGroup + voice]);
        _ram[AmplitudeGroup + voice] = (byte)-_ram[AmplitudeGroup + voice];
        return true;
    }

    /// <summary>An oscillator's pitch changes by its step (<c>PLAY10</c>).</summary>
    /// <param name="voice">The oscillator.</param>
    /// <returns>False when the new pitch is the oscillator's last, which ends the sound.</returns>
    private bool MovePitch(int voice)
    {
        _output.Wait(MovePitchCycles - StoreIndexed);
        _ram[CycleCounterGroup + voice] = _ram[CyclesGroup + voice];
        var pitch = (byte)(_ram[FrequencyGroup + voice] + _ram[FrequencyStepGroup + voice]);
        if (pitch == _ram[EndFrequencyGroup + voice])
        {
            _output.Wait(EndCycles);
            return false;
        }

        _output.Wait(StoreIndexed);
        _ram[FrequencyGroup + voice] = pitch;
        return true;
    }

    /// <summary>
    ///     The noise voice's turn (<c>PLAY3</c> onwards): it counts down, and when it runs out it makes the next
    ///     random number, steps the decay now and then, and picks a noise amount to add to the level.
    /// </summary>
    /// <param name="noise">The amount to add to the level, or null when the voice has not run out and nothing is written.</param>
    /// <returns>False when the decay has run to its end and the sound is over.</returns>
    private bool ChangeNoise(out int? noise)
    {
        noise = null;
        _output.Wait(CountVoiceCycles);
        if (--_ram[CounterGroup + NoiseVoice] != 0) return true;

        _output.Wait(CheckNoisePitchCycles);
        if (_ram[FrequencyGroup + NoiseVoice] == 0) return true;

        _output.Wait(ReloadNoiseCycles + NextRandomCycles + CountDecayCycles);
        _ram[CounterGroup + NoiseVoice] = (byte)(_ram[FrequencyGroup + NoiseVoice] & NotSignBit);
        var low = _memory.RandomLow;
        _memory.SetRandom(_memory.RandomHigh, (byte)((low << 1) + low + RandomAdd));
        if (--_ram[CycleCounterGroup + NoiseVoice] == 0 && !StepDecay()) return false;

        _output.Wait(NoiseAmplitudeCycles + AddNoiseCycles);
        var amount = _memory.RandomLow & _ram[AmplitudeGroup + NoiseVoice] & NotSignBit;
        if (_memory.RandomLow >= SignBit)
        {
            _output.Wait(NegateCycles);
            amount = -amount;
        }
        else
        {
            _output.Wait(PositiveBranchCycles);
        }

        noise = amount;
        return true;
    }

    /// <summary>The decay moves one step on: the whole level will be shifted down one more place.</summary>
    /// <returns>False when the step is the last, which ends the sound.</returns>
    private bool StepDecay()
    {
        _output.Wait(StepDecayCycles);
        _ram[CycleCounterGroup + NoiseVoice] = _ram[CyclesGroup + NoiseVoice];
        if (_shifts + 1 == MostShifts)
        {
            _output.Wait(EndCycles);
            return false;
        }

        _output.Wait(SaveDecayCycles);
        _shifts++;
        return true;
    }

    /// <summary>Writes a level through the decay (<c>JSR 0,X</c> into the shifts, then <c>STAB SOUND</c> and <c>RTS</c>).</summary>
    /// <param name="level">The level before the decay.</param>
    /// <param name="loopBackCycles">The branch back to the start.</param>
    /// <returns>The output change.</returns>
    private OutputChange WriteLevel(byte level, int loopBackCycles)
    {
        _output.Wait(CallDecayCycles + _shifts * Inherent);
        var change = _output.Store((byte)(level >> _shifts));
        _output.Wait(Return + loopBackCycles);
        return change;
    }
}
