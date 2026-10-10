using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     A falling bomb: a sine wave read through a table, faster and faster, so the pitch rises until the speed
///     has come down to its end.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>VSNDRM3.SRC</c>, routine <c>WHIST</c> ("THE BOMB OOOOOH NOOOOO!") and its table
///             <c>SINTBL</c> ("SINE TABLE").
///         </item>
///         <item>Disassembly: none in this repo (the sound ROM is not disassembled).</item>
///     </list>
///     The wave's phase (<c>TIME</c>) is not set before the sound, so it starts as whatever the board's memory held;
///     the port starts it at 0, as the board's memory is when the board is first switched on. The phase's top five
///     bits pick the level, so only the table's first 32 levels are ever read.
/// </remarks>
internal static class WhistleSound
{
    /// <summary>The speed the phase advances by at the start (<c>LDA #$80</c>, <c>STAA FREQZ</c>).</summary>
    private const byte FirstSpeed = 0x80;

    /// <summary>The speed at which the sound ends (<c>CMPA #$20</c>).</summary>
    private const byte LastSpeed = 0x20;

    /// <summary>Samples played at each speed (<c>LDAA #$80</c>, <c>STAA TEMPA</c>).</summary>
    private const int SamplesPerSpeed = 0x80;

    /// <summary>The wait before each sample, in counts (<c>LDAA #18</c>).</summary>
    private const int WaitCounts = 18;

    /// <summary>How far the phase is shifted to give the table's place (<c>LSRA</c> three times).</summary>
    private const int PlaceShift = 3;

    /// <summary><c>WHIST</c> setting up: <c>LDAA</c>, <c>STAA FREQZ</c>, <c>LDAA</c>, <c>STAA TABLE</c>.</summary>
    private const int SetUpCycles = (Immediate + StoreDirect) * 2;

    /// <summary><c>WHIST0</c> starting a speed: <c>LDAA #$80</c>, <c>STAA TEMPA</c>.</summary>
    private const int SpeedStartCycles = Immediate + StoreDirect;

    /// <summary>
    ///     <c>WHIST1</c> to the write: <c>LDAA #18</c>, the wait (<c>DECA</c>, <c>BNE</c>), then <c>LDAA TIME</c>,
    ///     <c>ADDA FREQZ</c>, <c>STAA TIME</c>, three <c>LSRA</c>, <c>ADDA #</c>, <c>STAA TABLE+1</c>, <c>LDX TABLE</c>,
    ///     <c>LDAA ,X</c>.
    /// </summary>
    private const int BeforeWriteCycles =
        Immediate + WaitCounts * (Inherent + Branch) + Direct + Direct + StoreDirect + PlaceShift * Inherent +
        Immediate + StoreDirect + WordDirect + Indexed;

    /// <summary><c>WHIST1</c> after the write: <c>DEC TEMPA</c>, <c>BNE</c>.</summary>
    private const int AfterWriteCycles = ModifyExtended + Branch;

    /// <summary><c>WHIST</c> finishing a speed: <c>DEC FREQZ</c>, <c>LDAA FREQZ</c>, <c>CMPA #$20</c>, <c>BNE</c>.</summary>
    private const int SpeedEndCycles = ModifyExtended + Direct + Immediate + Branch;

    /// <summary><c>SINTBL</c>: the sine wave, 64 levels.</summary>
    private static readonly byte[] SineTable =
    [
        0x80, 0x8C, 0x98, 0xA5, 0xB0, 0xBC, 0xC6, 0xD0, 0xDA, 0xE2, 0xEA, 0xF0, 0xF5, 0xFA, 0xFD, 0xFE,
        0xFF, 0xFE, 0xFD, 0xFA, 0xF5, 0xF0, 0xEA, 0xE2, 0xDA, 0xD0, 0xC6, 0xBC, 0xB0, 0xA5, 0x98, 0x8C,
        0x80, 0x73, 0x67, 0x5A, 0x4F, 0x43, 0x39, 0x2F, 0x25, 0x1D, 0x15, 0x0F, 0x0A, 0x05, 0x02, 0x01,
        0x00, 0x01, 0x02, 0x05, 0x0A, 0x0F, 0x15, 0x1D, 0x25, 0x2F, 0x39, 0x43, 0x4F, 0x5A, 0x67, 0x73
    ];

    /// <summary>Plays the falling bomb (sound <c>WHIST</c>).</summary>
    /// <param name="output">The board's output port.</param>
    /// <returns>The sound's output changes.</returns>
    public static IEnumerable<OutputChange> Play(BoardOutput output)
    {
        output.Wait(SetUpCycles);
        var speed = FirstSpeed;
        byte time = 0;
        do
        {
            output.Wait(SpeedStartCycles);
            for (var sample = 0; sample < SamplesPerSpeed; sample++)
            {
                output.Wait(BeforeWriteCycles);
                time += speed;
                yield return output.Store(SineTable[time >> PlaceShift]);
                output.Wait(AfterWriteCycles);
            }

            speed--;
            output.Wait(SpeedEndCycles);
        } while (speed != LastSpeed);

        output.Wait(Return);
    }
}
