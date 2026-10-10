using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     One step of the board's random number generator, timed instruction by instruction: each half of the
///     step happens when its instruction starts, so a sound number that cuts in part-way leaves the generator
///     exactly where the real board would.
/// </summary>
/// <remarks>
///     Original source: <c>VSNDRM3.SRC</c>, the step the noise routines share (<c>LITE1</c>, <c>MOISE1</c>,
///     <c>FNOIS1</c>): mix a byte with <c>LO</c>, then <c>ROR HI</c> and <c>ROR LO</c>. The bit that falls out
///     is left in <see cref="BoardMemory.RandomBitOut" />.
/// </remarks>
internal static class RandomStep
{
    /// <summary>Mixing <c>LO</c> into the new bit: <c>LDAA LO</c>, three <c>LSRA</c>, <c>EORA LO</c>, <c>LSRA</c>.</summary>
    public const int MixLowCycles = Direct + 3 * Inherent + Direct + Inherent;

    /// <summary>Mixing the output level into the new bit: <c>TAB</c>, three <c>LSRB</c>, <c>EORB LO</c>, <c>LSRB</c>.</summary>
    public const int MixLevelCycles = Inherent + 3 * Inherent + Direct + Inherent;

    /// <summary>Runs one step.</summary>
    /// <param name="memory">The board's lasting variables, which hold the generator.</param>
    /// <param name="output">The board's output port, for the time it takes.</param>
    /// <param name="mixedWith">The byte mixed into the new bit.</param>
    /// <param name="mixCycles">How long the mixing takes: <see cref="MixLowCycles" /> or <see cref="MixLevelCycles" />.</param>
    /// <returns>Two passes of time, one before each half of the step.</returns>
    public static IEnumerable<OutputChange> Run(BoardMemory memory, BoardOutput output, byte mixedWith, int mixCycles)
    {
        output.Wait(mixCycles);
        yield return output.Pass();
        memory.RotateRandomHigh(mixedWith);
        output.Wait(ModifyExtended);
        yield return output.Pass();
        memory.RotateRandomLow();
        output.Wait(ModifyExtended);
    }
}
