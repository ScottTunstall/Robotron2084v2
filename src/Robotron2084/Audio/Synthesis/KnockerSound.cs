using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// A knocker: bursts of a square wave, each burst a different length and loudness than the last, from fast
/// and quiet to slow and loud.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>KNOCK</c> ("KNOCKER ROUTINE") and its pattern <c>KNKTAB</c> ("KNOCKER PATTERN").</item>
/// <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
/// </list>
/// The routine also writes the second register of the output chip (<c>CLR SOUND+2</c>, "FULL BLAST", and
/// <c>STAA SOUND+2</c> at the end, "OVERRIDE OFF"), which sets the volume on the arcade's sound board; the
/// port's output is not volume-controlled, so those writes take their time and do nothing else.
/// </remarks>
internal static class KnockerSound
{
    /// <summary>The loud half of a burst: the top four bits of a pattern's second byte; the bottom four are how many cycles the burst has.</summary>
    private const int LevelMask = 0xF0;

    private const int CountMask = 0x0F;

    /// <summary>The wait unit: <c>LDX #5</c>, five <c>DEX</c>/<c>BNE</c>, then <c>DECA</c> and <c>BNE</c> (the period counts these).</summary>
    private const int PeriodUnitCycles = WordImmediate + (5 * (IndexStep + Branch)) + Inherent + Branch;

    /// <summary><c>KNOCK</c> setting up: <c>CLR SOUND+2</c>, <c>LDX</c>, <c>STX SNDTMP</c>.</summary>
    private const int SetUpCycles = ModifyExtended + WordImmediate + WordStoreDirect;

    /// <summary><c>SQLP</c> reading a burst: <c>LDX</c>, <c>LDAA</c>, <c>BEQ</c>, <c>LDAB</c>, <c>ANDB</c>, <c>STAB</c>, <c>LDAB</c>, two <c>INX</c>, <c>STX</c>, <c>STAA</c>, <c>ANDB</c>.</summary>
    private const int ReadBurstCycles =
        WordDirect + Indexed + Branch + Indexed + Immediate + StoreDirect + Indexed + (2 * IndexStep) + WordStoreDirect + StoreDirect + Immediate;

    /// <summary><c>LP0</c> before the loud write: <c>LDAA AMP</c>; after it <c>LDAA PERIOD</c>.</summary>
    private const int LoudStartCycles = Direct;

    private const int WaitStartCycles = Direct;

    /// <summary><c>LP2</c> after the quiet write and its wait, then <c>DECB</c>, <c>BNE</c>.</summary>
    private const int EndCycleCycles = Inherent + Branch;

    /// <summary>The end of the pattern: <c>BRA SQLP</c> between bursts; <c>LDAA #$80</c>, <c>STAA SOUND+2</c> at the end.</summary>
    private const int NextBurstCycles = Branch;

    private const int EndCycles = Immediate + StoreExtended + Return;

    /// <summary><c>KNKTAB</c>: each burst's wait (the period) and its loudness and cycle count; a period of 0 ends the pattern.</summary>
    private static readonly ushort[] Pattern =
    [
        0x01FC, 0x02FC, 0x03F8, 0x04F8, 0x06F8, 0x08F4, 0x0CF4,
        0x10F4, 0x20F2, 0x40F1, 0x60F1, 0x80F1, 0xA0F1, 0xC0F1,
    ];

    /// <summary>Plays the knocker (sound <c>KNOCK</c>).</summary>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardOutput output)
    {
        output.Wait(SetUpCycles);
        foreach (ushort burst in Pattern)
        {
            var period = (byte)(burst >> 8);
            var loud = (byte)((burst & LevelMask));
            byte cycles = (byte)(burst & CountMask);
            output.Wait(ReadBurstCycles);
            do
            {
                output.Wait(LoudStartCycles);
                yield return output.Store(loud);
                output.Wait(WaitStartCycles + (CountdownLoop.Runs(period) * PeriodUnitCycles));
                output.Wait(ModifyExtended - StoreExtended);
                yield return output.Store(0);
                output.Wait(WaitStartCycles + (CountdownLoop.Runs(period) * PeriodUnitCycles));
                output.Wait(EndCycleCycles);
                cycles--;
            }
            while (cycles != 0);

            output.Wait(NextBurstCycles);
        }

        output.Wait(WordDirect + Indexed + Branch + EndCycles);
    }
}
