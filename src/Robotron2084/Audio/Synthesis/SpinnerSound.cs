using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// The spinner sound: a square wave that plays forever, and starts a little higher each time it is sent
/// again before anything else is. The game sends it over and over as a wave ends.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>SP1</c> ("SPINNER #1 SOUND").</item>
/// <item>Disassembly: none in this repo; ROM <c>$F9A4</c> (from the jump table <c>JMPTBL</c>).</item>
/// </list>
/// </remarks>
internal static class SpinnerSound
{
    /// <summary>How many sends the pitch climbs through before it starts again (<c>SP1MAX</c>).</summary>
    private const int SendsPerClimb = 32;

    /// <summary>Above this, each step of the low time is the large one (<c>SP11</c>: <c>CMPA #20</c>).</summary>
    private const int LargeStepThreshold = 20;

    /// <summary>The large step of the low time (<c>ADDB #14</c>).</summary>
    private const int LargeLowWaitStep = 14;

    /// <summary>The small step of the low time (<c>ADDB #5</c>).</summary>
    private const int SmallLowWaitStep = 5;

    /// <summary>Loading the settings: <c>LDAA #</c>, <c>JSR VARILD</c>.</summary>
    private const int CallLoadCycles = Immediate + CallExtended;

    /// <summary>Checking the send count against its top: <c>LDAB SP1FLG</c>, <c>CMPB</c>, <c>BNE</c>.</summary>
    private const int CheckSendsCycles = Direct + Immediate + Branch;

    /// <summary>Counting the send and getting ready to work out the low time: <c>INCB</c>, <c>STAB</c>, <c>LDAA #</c>, <c>SBA</c>, <c>CLRB</c>.</summary>
    private const int CountSendCycles = Inherent + StoreDirect + Immediate + Inherent + Inherent;

    /// <summary><c>SP11</c>'s test: <c>CMPA #20</c>, <c>BLS</c>.</summary>
    private const int LargeStepTestCycles = Immediate + Branch;

    /// <summary><c>SP11</c>'s step: <c>ADDB #14</c>, <c>DECA</c>, <c>BRA</c>.</summary>
    private const int LargeStepCycles = Immediate + Inherent + Branch;

    /// <summary><c>SP12</c>'s step: <c>ADDB #5</c>, <c>DECA</c>, <c>BNE</c>.</summary>
    private const int SmallStepCycles = Immediate + Inherent + Branch;

    /// <summary>Plays the spinner sound and counts the send.</summary>
    /// <param name="memory">The board's lasting variables, which hold the send count.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes; they never end.</returns>
    public static IEnumerable<OutputChange> Play(BoardMemory memory, BoardOutput output)
    {
        var square = new SquareWaveSound(output);
        output.Wait(CallLoadCycles);
        square.Load(SquareWaveSound.CabinetShakeVector);
        square.LowWait = GetLowWaitForSend(CountSend(memory, output), output);
        while (true)
        {
            output.Wait(CallExtended);
            foreach (OutputChange change in square.Play())
            {
                yield return change;
            }

            output.Wait(Branch);
        }
    }

    /// <summary>Adds one to the send count, going back to 1 after the top (<c>SP1</c> to <c>SP1A</c>).</summary>
    /// <param name="memory">The board's lasting variables.</param>
    /// <param name="output">The board's output port, for the time it takes.</param>
    /// <returns>The new send count.</returns>
    private static byte CountSend(BoardMemory memory, BoardOutput output)
    {
        output.Wait(CheckSendsCycles);
        byte sends = memory.SpinnerSends;
        if (sends == SendsPerClimb - 1)
        {
            output.Wait(Inherent);
            sends = 0;
        }

        output.Wait(CountSendCycles);
        sends++;
        memory.SpinnerSends = sends;
        return sends;
    }

    /// <summary>
    /// Works out the low time: the more sends so far, the shorter it is, so the pitch rises
    /// (<c>SP11</c>, <c>SP12</c>, <c>STAB LOPER</c>).
    /// </summary>
    /// <param name="sends">The send count.</param>
    /// <param name="output">The board's output port, for the time it takes.</param>
    /// <returns>The low time, in counts.</returns>
    private static byte GetLowWaitForSend(byte sends, BoardOutput output)
    {
        var stepsLeft = (byte)(SendsPerClimb - sends);
        byte lowWait = 0;
        while (true)
        {
            output.Wait(LargeStepTestCycles);
            if (stepsLeft <= LargeStepThreshold)
            {
                break;
            }

            output.Wait(LargeStepCycles);
            lowWait += LargeLowWaitStep;
            stepsLeft--;
        }

        do
        {
            output.Wait(SmallStepCycles);
            lowWait += SmallLowWaitStep;
            stepsLeft--;
        }
        while (stepsLeft != 0);

        output.Wait(StoreDirect);
        return lowWait;
    }
}
