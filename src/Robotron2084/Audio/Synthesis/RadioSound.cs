using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
/// A short wave read faster and faster: a timer adds the speed to itself, the top of the timer picks the
/// level, and every time the timer wraps the speed goes up by one, until it wraps too.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>VSNDRM3.SRC</c>, routine <c>RADIO</c> and its wave <c>RADSND</c> ("RADIO SOUND WAVEFORM", ROM <c>$FC47</c>).</item>
/// <item>Disassembly: none in this repo; ROM <c>$F82B</c> (from the jump table <c>JMPTBL</c>).</item>
/// </list>
/// The timer's top byte is <c>TEMPA</c>, which the routine does not set first, so where the wave starts
/// depends on the sound before. Its low byte starts at 0, from the interrupt handler's <c>CLRB</c>.
/// </remarks>
internal static class RadioSound
{
    /// <summary>The first speed (<c>LDX #100</c>).</summary>
    private const ushort FirstSpeed = 100;

    /// <summary>The bits of the timer's top byte that pick a level (<c>ANDA #$F</c>).</summary>
    private const int WavePlaceMask = 0x0F;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary><c>RADIO</c> setting up: <c>LDAA #</c>, <c>STAA XPTR</c>, <c>LDX #100</c>, <c>STX TEMPX</c>.</summary>
    private const int SetUpCycles = Immediate + StoreDirect + WordImmediate + WordStoreDirect;

    /// <summary>
    /// <c>RADIO1</c> adding the speed to the timer: <c>ADDB</c>, <c>LDAA</c>, <c>ADCA</c>, <c>STAA</c>,
    /// <c>LDX</c>, <c>BCS</c>, then two more instructions either way (<c>INX</c>/<c>BEQ</c>, or the source's
    /// "EQUALIZE TIME" <c>BRA</c>/<c>BRA</c>).
    /// </summary>
    private const int AddSpeedCycles = Direct + Direct + Direct + StoreDirect + WordDirect + Branch + IndexStep + Branch;

    /// <summary>
    /// <c>RADIO3</c> picking the level: <c>STX</c>, <c>ANDA</c>, <c>ADDA</c>, <c>STAA</c>, <c>LDX</c>,
    /// <c>LDAA ,X</c>, then (after the store) <c>BRA RADIO1</c>.
    /// </summary>
    private const int PickLevelCycles = WordStoreDirect + Immediate + Immediate + StoreDirect + WordDirect + Indexed;

    /// <summary><c>RADSND</c>: the sixteen levels of the wave.</summary>
    private static readonly byte[] Wave =
    [
        0x8C, 0x5B, 0xB6, 0x40, 0xBF, 0x49, 0xA4, 0x73,
        0x73, 0xA4, 0x49, 0xBF, 0x40, 0xB6, 0x5B, 0x8C,
    ];

    /// <summary>Plays the radio sound (sound <c>RADIO</c>).</summary>
    /// <param name="memory">The board's lasting variables, whose <c>TEMPA</c> is the timer's top byte.</param>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardMemory memory, BoardOutput output)
    {
        output.Wait(SetUpCycles);
        ushort speed = FirstSpeed;
        byte timerLow = 0;
        while (true)
        {
            output.Wait(AddSpeedCycles);
            int timer = ((memory.ScratchA << ByteBits) | timerLow) + speed;
            memory.ScratchA = (byte)(timer >> ByteBits);
            timerLow = (byte)timer;
            if (timer > ushort.MaxValue)
            {
                speed++;
                if (speed == 0)
                {
                    yield break;
                }
            }

            output.Wait(PickLevelCycles);
            yield return output.Store(Wave[memory.ScratchA & WavePlaceMask]);
            output.Wait(Branch);
        }
    }
}
