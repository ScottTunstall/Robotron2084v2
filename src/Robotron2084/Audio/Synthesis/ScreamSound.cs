using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// Four falling tones, each starting when the one before has fallen a little way, and each half as loud
/// as the one before: the tones' square waves are added together into the level.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>SCREAM</c> and its table <c>STABLE</c> ("SCREAM TABLE").</item>
/// <item>Disassembly: none in this repo; ROM <c>$F878</c> (from the jump table <c>JMPTBL</c>).</item>
/// </list>
/// The levels between each fall are counted in <c>TEMPB</c>, which the routine does not set first, so the
/// first fall's length depends on the sound before.
/// </remarks>
internal static class ScreamSound
{
    /// <summary>How many tones there are (<c>ECHOS</c>).</summary>
    private const int Tones = 4;

    /// <summary>The first tone's starting pitch (<c>LDAA #$40</c>).</summary>
    private const byte FirstPitch = 0x40;

    /// <summary>The pitch a tone has fallen to when it starts the next one (<c>CMPA #$37</c>).</summary>
    private const byte StartNextAtPitch = 0x37;

    /// <summary>The next tone's starting pitch (<c>LDAB #$41</c>).</summary>
    private const byte NextPitch = 0x41;

    /// <summary>The first tone's loudness; each tone after is half as loud (<c>LDAA #$80</c>, <c>LSR TEMPA</c>). It is the value stored in <see cref="BoardMemory.ScratchA"/> at the start of the sound.</summary>
    private const byte FirstLoudness = 0x80;

    /// <summary>The bit of a tone's timer that says its square wave is high (<c>BPL</c>).</summary>
    private const int HighBit = 0x80;

    /// <summary>
    /// <c>SCREAM</c> clearing the table: <c>LDX</c>, then for each byte <c>CLR ,X</c>, <c>INX</c>, <c>CPX</c>,
    /// <c>BNE</c>; then <c>LDAA</c>, <c>STAA</c> to start the first tone.
    /// </summary>
    private const int ClearTableCycles =
        WordImmediate + (2 * Tones * (ModifyIndexed + IndexStep + WordImmediate + Branch)) + Immediate + StoreDirect;

    /// <summary><c>SCREM2</c> starting a level: <c>LDX</c>, <c>LDAA</c>, <c>STAA TEMPA</c>, <c>CLRB</c>.</summary>
    private const int StartLevelCycles = WordImmediate + Immediate + StoreDirect + Inherent;

    /// <summary><c>SCREM3</c> moving one tone's timer on: <c>LDAA</c>, <c>ADDA</c>, <c>STAA</c>, <c>BPL</c>.</summary>
    private const int MoveTimerCycles = Indexed + Indexed + StoreIndexed + Branch;

    /// <summary><c>SCREM4</c> halving the loudness and moving to the next tone: <c>LSR TEMPA</c>, <c>INX</c>, <c>INX</c>, <c>CPX</c>, <c>BNE</c>.</summary>
    private const int NextToneCycles = ModifyExtended + IndexStep + IndexStep + WordImmediate + Branch;

    /// <summary>Counting a level, after the store: <c>INC TEMPB</c>, <c>BNE</c>.</summary>
    private const int CountLevelCycles = ModifyExtended + Branch;

    /// <summary><c>SCREM5</c> starting the fall: <c>LDX</c>, <c>CLRB</c>.</summary>
    private const int StartFallCycles = WordImmediate + Inherent;

    /// <summary>Checking one tone: <c>LDAA FREQ,X</c>, <c>BEQ</c>.</summary>
    private const int CheckToneCycles = Indexed + Branch;

    /// <summary>Checking whether a sounding tone starts the next: <c>CMPA #$37</c>, <c>BNE</c>.</summary>
    private const int CheckStartNextCycles = Immediate + Branch;

    /// <summary>Starting the next tone: <c>LDAB #$41</c>, <c>STAB FREQ+2,X</c>.</summary>
    private const int StartNextCycles = Immediate + StoreIndexed;

    /// <summary><c>SCREM6</c> lowering a tone's pitch: <c>DEC FREQ,X</c>, <c>INCB</c>.</summary>
    private const int FallCycles = ModifyIndexed + Inherent;

    /// <summary><c>SCREM7</c> moving to the next tone: <c>INX</c>, <c>INX</c>, <c>CPX</c>, <c>BNE</c>.</summary>
    private const int NextFallCycles = IndexStep + IndexStep + WordImmediate + Branch;

    /// <summary>Checking whether any tone is still sounding: <c>TSTB</c>, <c>BNE</c>.</summary>
    private const int CheckDoneCycles = Inherent + Branch;

    /// <summary>Plays the scream sound (sound <c>SCREAM</c>).</summary>
    /// <param name="memory">The board's lasting variables, whose <c>TEMPA</c> and <c>TEMPB</c> it uses.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardMemory memory, BoardOutput output)
    {
        var pitches = new byte[Tones];
        var timers = new byte[Tones];
        output.Wait(ClearTableCycles);
        pitches[0] = FirstPitch;
        do
        {
            do
            {
                output.Wait(StartLevelCycles);
                yield return output.Store(MixTones(memory, output, pitches, timers));
                output.Wait(CountLevelCycles);
                memory.ScratchB++;
            }
            while (memory.ScratchB != 0);
        }
        while (LowerPitches(output, pitches));
    }

    /// <summary>Moves each tone's timer on and adds up the loudness of the tones whose wave is high (<c>SCREM3</c>, <c>SCREM4</c>).</summary>
    /// <param name="memory">The board's lasting variables, whose <c>TEMPA</c> holds the loudness.</param>
    /// <param name="output">The board's output port, for the time it takes.</param>
    /// <param name="pitches">Each tone's pitch: what its timer moves by each level.</param>
    /// <param name="timers">Each tone's timer.</param>
    /// <returns>The level.</returns>
    private static byte MixTones(BoardMemory memory, BoardOutput output, byte[] pitches, byte[] timers)
    {
        memory.ScratchA = FirstLoudness;
        byte level = 0;
        for (int tone = 0; tone < Tones; tone++)
        {
            output.Wait(MoveTimerCycles);
            timers[tone] += pitches[tone];
            if (timers[tone] >= HighBit)
            {
                output.Wait(Direct);
                level += memory.ScratchA;
            }

            output.Wait(NextToneCycles);
            memory.ScratchA >>= 1;
        }

        return level;
    }

    /// <summary>
    /// Lowers the pitch of every tone still sounding, and starts the next tone when one falls far enough
    /// (<c>SCREM5</c> to <c>SCREM7</c>).
    /// </summary>
    /// <param name="output">The board's output port, for the time it takes.</param>
    /// <param name="pitches">Each tone's pitch.</param>
    /// <returns>True while any tone is still sounding.</returns>
    private static bool LowerPitches(BoardOutput output, byte[] pitches)
    {
        output.Wait(StartFallCycles);
        bool isSounding = false;
        for (int tone = 0; tone < Tones; tone++)
        {
            output.Wait(CheckToneCycles);
            if (pitches[tone] != 0)
            {
                output.Wait(CheckStartNextCycles);
                if (pitches[tone] == StartNextAtPitch)
                {
                    StartNextTone(output, pitches, tone);
                }

                output.Wait(FallCycles);
                pitches[tone]--;
                isSounding = true;
            }

            output.Wait(NextFallCycles);
        }

        output.Wait(CheckDoneCycles);
        return isSounding;
    }

    /// <summary>
    /// Starts the tone after this one. The last tone has none after it: the source's store then lands just
    /// past the table, on a byte no Robotron sound reads, so only its time is kept.
    /// </summary>
    /// <param name="output">The board's output port, for the time it takes.</param>
    /// <param name="pitches">Each tone's pitch.</param>
    /// <param name="tone">The tone that has fallen far enough.</param>
    private static void StartNextTone(BoardOutput output, byte[] pitches, int tone)
    {
        output.Wait(StartNextCycles);
        if (tone + 1 < Tones)
        {
            pitches[tone + 1] = NextPitch;
        }
    }
}
