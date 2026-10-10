using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     A diving plane: the output goes low for a time that grows every cycle and high for a fixed time, so the
///     pitch falls for as long as the sound plays.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>PLANE</c> ("DIVING PLANE SOUND").</item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
///     The sound never ends by itself: the low time grows by one count a cycle, and the sound has gone on until
///     another sound number arrives long before the count could wrap.
/// </remarks>
internal static class PlaneSound
{
    /// <summary>The low time's first value (<c>LDX #$0001</c>).</summary>
    private const int FirstLowCount = 1;

    /// <summary>The high time (<c>LDX #$0380</c>).</summary>
    private const int HighCount = 0x0380;

    /// <summary>The largest 16-bit count; going past it wraps to 0.</summary>
    private const int WordWrap = 0x10000;

    /// <summary><c>PLANE</c> setting up: <c>LDX</c>, <c>STX FREQ1</c>, <c>LDX</c>, <c>STX FREQ3</c>.</summary>
    private const int SetUpCycles = (WordImmediate + WordStoreDirect) * 2;

    /// <summary><c>PLANE1</c> after the clear: <c>LDX FREQ1</c>, <c>INX</c>, <c>STX FREQ1</c>.</summary>
    private const int GrowLowCycles = WordDirect + IndexStep + WordStoreDirect;

    /// <summary>One count of a wait (<c>DEX</c>, <c>BNE</c>).</summary>
    private const int CountCycles = IndexStep + Branch;

    /// <summary>After the complement: <c>LDX FREQ3</c>; and at the end <c>BRA PLANE1</c>.</summary>
    private const int LoadHighCycles = WordDirect;

    /// <summary>Plays the diving plane (sound <c>PLANE</c>).</summary>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardOutput output)
    {
        output.Wait(SetUpCycles);
        var lowCount = FirstLowCount;
        while (true)
        {
            output.Wait(ModifyExtended - StoreExtended);
            yield return output.Store(0);
            output.Wait(GrowLowCycles);
            lowCount = (lowCount + 1) % WordWrap;
            output.Wait(CountCycles * CountdownLoop.Runs((ushort)lowCount));
            yield return output.Complement();
            output.Wait(LoadHighCycles + CountCycles * HighCount + Branch);
        }
    }
}
