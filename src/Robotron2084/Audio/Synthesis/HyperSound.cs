using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     A buzz whose two halves change balance: each cycle flips the level once part-way through and once at
///     the end, and the part-way flip moves later every cycle.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>HYPER</c>.</item>
///         <item>Disassembly: none in this repo; ROM <c>$F859</c> (from the jump table <c>JMPTBL</c>).</item>
///     </list>
/// </remarks>
internal static class HyperSound
{
    /// <summary>Counts in each cycle, and cycles in the sound: both counters stop when they reach the top bit (<c>BPL</c>).</summary>
    private const int CountsPerCycle = 0x80;

    /// <summary>The inner wait for each count (<c>LDAB #18</c>).</summary>
    private const int DelayCounts = 18;

    /// <summary><c>HYPER</c> starting: <c>CLRA</c>, then (after the store) <c>STAA TEMPA</c>.</summary>
    private const int StartCycles = Inherent;

    /// <summary><c>HYPER1</c> starting a cycle: <c>CLRA</c>.</summary>
    private const int StartCycleCycles = Inherent;

    /// <summary><c>HYPER2</c> checking for the part-way flip: <c>CMPA TEMPA</c>, <c>BNE</c>.</summary>
    private const int CheckFlipCycles = Direct + Branch;

    /// <summary>One count: <c>LDAB #18</c>, eighteen <c>DECB</c>/<c>BNE</c>, then <c>INCA</c>, <c>BPL</c>.</summary>
    private const int CountCycles = Immediate + DelayCounts * (Inherent + Branch) + Inherent + Branch;

    /// <summary>Ending a cycle, after the flip: <c>INC TEMPA</c>, <c>BPL</c>.</summary>
    private const int EndCycleCycles = ModifyExtended + Branch;

    /// <summary>Plays the hyper sound (sound <c>HYPER</c>).</summary>
    /// <param name="memory">The board's lasting variables, whose <c>TEMPA</c> holds where the part-way flip comes.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardMemory memory, BoardOutput output)
    {
        output.Wait(StartCycles);
        yield return output.Store(0);
        output.Wait(StoreDirect);
        memory.ScratchA = 0;
        do
        {
            output.Wait(StartCycleCycles);
            for (var count = 0; count < CountsPerCycle; count++)
            {
                output.Wait(CheckFlipCycles);
                if (count == memory.ScratchA) yield return output.Complement();

                output.Wait(CountCycles);
            }

            yield return output.Complement();
            output.Wait(EndCycleCycles);
            memory.ScratchA++;
        } while (memory.ScratchA < CountsPerCycle);
    }
}
