using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     White noise that fades out and slows down: each level is silence or the current loudness at random,
///     the loudness drops a step each round, and the gap between levels grows.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>VSNDRM3.SRC</c>, routines <c>TURBO</c> and <c>MOISE</c> ("WHITE NOISE ROUTINE").</item>
///         <item>Disassembly: none in this repo; ROM <c>$F59B</c> (<c>TURBO</c>, from the jump table <c>JMPTBL</c>).</item>
///     </list>
/// </remarks>
internal static class WhiteNoise
{
    /// <summary><c>TURBO</c>'s levels per round (<c>LDAA #$20</c>, <c>STAA CYCNT</c>).</summary>
    private const byte TurboLevelsPerRound = 0x20;

    /// <summary><c>TURBO</c>'s loudness drop each round (<c>LDAA #$1</c>, <c>STAA DECAY</c>).</summary>
    private const byte TurboFade = 1;

    /// <summary><c>TURBO</c>'s first gap between levels, in counts (<c>LDX #1</c>).</summary>
    private const ushort TurboFirstGap = 1;

    /// <summary><c>TURBO</c>'s first loudness (<c>LDAB #$FF</c>).</summary>
    private const byte TurboFirstLoudness = 0xFF;

    /// <summary>
    ///     <c>TURBO</c> setting up: <c>LDAA</c>, <c>STAA CYCNT</c>, <c>STAA NFFLG</c>, <c>LDAA</c>, <c>LDX</c>, <c>LDAB</c>,
    ///     <c>BRA</c>. The <c>NFFLG</c> it sets is not zero, so the gap grows every round.
    /// </summary>
    private const int TurboSetUpCycles =
        Immediate + StoreDirect + StoreDirect + Immediate + WordImmediate + Immediate + Branch;

    /// <summary><c>MOISE</c> keeping the fade (<c>STAA DECAY</c>).</summary>
    private const int StoreFadeCycles = StoreDirect;

    /// <summary><c>MOISE0</c> keeping the gap (<c>STX NFRQ1</c>).</summary>
    private const int StoreGapCycles = WordStoreDirect;

    /// <summary><c>MOIS00</c> keeping the loudness and starting the round: <c>STAB NAMP</c>, <c>LDAB CYCNT</c>.</summary>
    private const int StartRoundCycles = StoreDirect + Direct;

    /// <summary>Choosing a level after the random step: <c>LDAA #0</c> and <c>BCC</c>.</summary>
    private const int ChooseLevelCycles = Immediate + Branch;

    /// <summary>The waits after a level: <c>LDX NFRQ1</c> before the gap, <c>DECB</c> and <c>BNE</c> after.</summary>
    private const int FinishLevelCycles = WordDirect + Inherent + Branch;

    /// <summary>One count of the gap (<c>MOISE3</c>: <c>DEX</c>, <c>BNE</c>).</summary>
    private const int GapCountCycles = IndexStep + Branch;

    /// <summary>Fading at the end of a round: <c>LDAB NAMP</c>, <c>SUBB DECAY</c>, <c>BEQ</c>.</summary>
    private const int FadeCycles = Direct + Direct + Branch;

    /// <summary>Growing the gap: <c>LDX NFRQ1</c>, <c>INX</c>, <c>LDAA NFFLG</c>, <c>BEQ</c>.</summary>
    private const int GrowGapCycles = WordDirect + IndexStep + Direct + Branch;

    /// <summary>Plays the turbo sound (sound <c>TURBO</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayTurbo(BoardMemory memory, BoardOutput output)
    {
        output.Wait(TurboSetUpCycles + StoreFadeCycles);
        var gap = TurboFirstGap;
        var loudness = TurboFirstLoudness;
        while (true)
        {
            output.Wait(StoreGapCycles + StartRoundCycles);
            foreach (var change in PlayRound(memory, output, gap, loudness)) yield return change;

            output.Wait(FadeCycles);
            loudness -= TurboFade;
            if (loudness == 0) yield break;

            output.Wait(GrowGapCycles + Branch);
            gap++;
        }
    }

    /// <summary>Plays one round of random levels at one loudness and gap (<c>MOISE1</c> to <c>MOISE3</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <param name="gap">The gap after each level, in counts (<c>NFRQ1</c>).</param>
    /// <param name="loudness">The level a random 1 gives (<c>NAMP</c>).</param>
    /// <returns>The output changes.</returns>
    private static IEnumerable<OutputChange> PlayRound(BoardMemory memory, BoardOutput output, ushort gap,
        byte loudness)
    {
        var levelsLeft = TurboLevelsPerRound;
        do
        {
            foreach (var change in RandomStep.Run(memory, output, memory.RandomLow, RandomStep.MixLowCycles))
                yield return change;

            output.Wait(ChooseLevelCycles);
            byte level = 0;
            if (memory.RandomBitOut)
            {
                output.Wait(Direct);
                level = loudness;
            }

            yield return output.Store(level);
            output.Wait(FinishLevelCycles + CountdownLoop.Runs(gap) * GapCountCycles);
            levelsLeft--;
        } while (levelsLeft != 0);
    }
}
