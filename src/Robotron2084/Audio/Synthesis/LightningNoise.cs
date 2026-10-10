using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// Crackling noise: the level flips at random moments, and the gap between chances to flip grows or
/// shrinks steadily until it comes round to nothing.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routines <c>LITE</c> ("LIGHTNING"), <c>APPEAR</c> and
/// <c>LITEN</c> ("LIGHTNING+APPEAR NOISE ROUTINE").</item>
/// <item>Disassembly: none in this repo; ROM <c>$F55A</c> (<c>LITE</c>) and <c>$F562</c> (<c>APPEAR</c>),
/// from the jump table <c>JMPTBL</c>.</item>
/// </list>
/// </remarks>
internal static class LightningNoise
{
    /// <summary><c>LITE</c>'s gap change: the gap grows by one each round (<c>LDAA #1</c>, <c>STAA DFREQ</c>).</summary>
    private const byte LightningGapChange = 1;

    /// <summary><c>LITE</c>'s first gap: the <c>1</c> still in accumulator A when it reaches <c>LITEN</c>.</summary>
    private const byte LightningFirstGap = 1;

    /// <summary><c>LITE</c>'s chances to flip at each gap (<c>LDAB #3</c>).</summary>
    private const byte LightningChancesPerGap = 3;

    /// <summary><c>APPEAR</c>'s gap change: the gap shrinks by two each round (<c>LDAA #$FE</c>).</summary>
    private const byte AppearGapChange = 0xFE;

    /// <summary><c>APPEAR</c>'s first gap (<c>LDAA #$C0</c>).</summary>
    private const byte AppearFirstGap = 0xC0;

    /// <summary><c>APPEAR</c>'s chances to flip at each gap (<c>LDAB #$10</c>).</summary>
    private const byte AppearChancesPerGap = 0x10;

    /// <summary><c>LITEN</c>'s first level: as loud as it goes (<c>LDAA #$FF</c>).</summary>
    private const byte StartLevel = 0xFF;

    /// <summary><c>LAUNCH</c>'s gap change (<c>LDAA #$FF</c>: one less each round), first gap (<c>LDAA #$60</c>) and chances (<c>LDAB #$FF</c>).</summary>
    private const byte LaunchGapChange = 0xFF;

    private const byte LaunchFirstGap = 0x60;

    private const byte LaunchChancesPerGap = 0xFF;

    /// <summary><c>LAUNCH</c> setting up: <c>LDAA</c>, <c>STAA</c>, <c>LDAA</c>, <c>LDAB</c>, <c>BRA</c>.</summary>
    private const int LaunchSetUpCycles = Immediate + StoreDirect + Immediate + Immediate + Branch;

    /// <summary><c>LITE</c> setting up: <c>LDAA</c>, <c>STAA</c>, <c>LDAB</c>, <c>BRA</c>.</summary>
    private const int LightningSetUpCycles = Immediate + StoreDirect + Immediate + Branch;

    /// <summary><c>APPEAR</c> setting up: <c>LDAA</c>, <c>STAA</c>, <c>LDAA</c>, <c>LDAB</c>, <c>BRA</c>.</summary>
    private const int AppearSetUpCycles = Immediate + StoreDirect + Immediate + Immediate + Branch;

    /// <summary>The rest of a chance, besides the gap itself: <c>LDAA LFREQ</c> before it, <c>DECB</c> and <c>BNE</c> after.</summary>
    private const int FinishChanceCycles = Direct + Inherent + Branch;

    /// <summary>One count of the gap (<c>LITE3</c>: <c>DECA</c>, <c>BNE</c>).</summary>
    private const int GapCountCycles = Inherent + Branch;

    /// <summary>Changing the gap at the end of a round: <c>LDAA</c>, <c>ADDA DFREQ</c>, <c>STAA</c>, <c>BNE</c>.</summary>
    private const int ChangeGapCycles = Direct + Direct + StoreDirect + Branch;

    /// <summary>Plays the lightning sound (sound <c>LITE</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayLightning(BoardMemory memory, BoardOutput output)
    {
        output.Wait(LightningSetUpCycles);
        return Play(memory, output, LightningFirstGap, LightningGapChange, LightningChancesPerGap);
    }

    /// <summary>Plays the appear sound (sound <c>APPEAR</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayAppear(BoardMemory memory, BoardOutput output)
    {
        output.Wait(AppearSetUpCycles);
        return Play(memory, output, AppearFirstGap, AppearGapChange, AppearChancesPerGap);
    }

    /// <summary>Plays the launch sound (sound <c>LAUNCH</c>): the gap shrinks by one each round, from $60, with 255 chances at each.</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> PlayLaunch(BoardMemory memory, BoardOutput output)
    {
        output.Wait(LaunchSetUpCycles);
        return Play(memory, output, LaunchFirstGap, LaunchGapChange, LaunchChancesPerGap);
    }

    /// <summary>The shared noise (<c>LITEN</c>).</summary>
    /// <param name="memory">The board's lasting variables, for the random numbers.</param>
    /// <param name="output">The board's output port.</param>
    /// <param name="gap">The first gap between chances, in counts (<c>LFREQ</c>).</param>
    /// <param name="gapChange">How much the gap changes each round (<c>DFREQ</c>).</param>
    /// <param name="chancesPerGap">How many chances to flip come at each gap (<c>CYCNT</c>).</param>
    /// <returns>The sound's output changes.</returns>
    private static IEnumerable<OutputChange> Play(BoardMemory memory, BoardOutput output, byte gap, byte gapChange, byte chancesPerGap)
    {
        output.Wait(StoreDirect + Immediate);
        yield return output.Store(StartLevel);
        output.Wait(StoreDirect);
        do
        {
            output.Wait(Direct);
            byte chancesLeft = chancesPerGap;
            do
            {
                foreach (OutputChange change in RandomStep.Run(memory, output, memory.RandomLow, RandomStep.MixLowCycles))
                {
                    yield return change;
                }

                output.Wait(Branch);
                if (memory.RandomBitOut)
                {
                    yield return output.Complement();
                }

                output.Wait(FinishChanceCycles + (CountdownLoop.Runs(gap) * GapCountCycles));
                chancesLeft--;
            }
            while (chancesLeft != 0);

            output.Wait(ChangeGapCycles);
            gap += gapChange;
        }
        while (gap != 0);
    }
}
