using static Robotron2084.Audio.Synthesis.InstructionCycles;

namespace Robotron2084.Audio.Synthesis;

/// <summary>What the sound board's two shared helper routines cost, in clock cycles.</summary>
/// <remarks>
///     Original source: <c>VSNDRM3.SRC</c>, routines <c>ADDX</c> ("ADD A TO INDEX REGISTER") and <c>TRANS</c>
///     ("PARAMETER TRANSFER").
/// </remarks>
internal static class SubroutineCycles
{
    /// <summary>
    ///     Calling <c>ADDX</c> and running it when the sum stays in the same 256-byte page, as every use in
    ///     the port does: <c>JSR</c>, then <c>STX</c>, <c>ADDA</c>, <c>STAA</c>, <c>BCC</c> taken, <c>LDX</c>, <c>RTS</c>.
    /// </summary>
    public const int CallAddToIndex =
        CallExtended + WordStoreDirect + Direct + StoreDirect + Branch + WordDirect + Return;

    /// <summary><c>TRANS</c> without the bytes it copies: <c>PSHA</c>, <c>PULA</c>, <c>RTS</c>.</summary>
    public const int Transfer = Stack + Stack + Return;

    /// <summary>
    ///     <c>TRANS1</c> copying one byte: <c>LDAA</c>, <c>STX</c>, <c>LDX</c>, <c>STAA</c>, <c>INX</c>, <c>STX</c>,
    ///     <c>LDX</c>, <c>INX</c>, <c>DECB</c>, <c>BNE</c>.
    /// </summary>
    public const int TransferPerByte =
        Indexed + WordStoreDirect + WordDirect + StoreIndexed + IndexStep + WordStoreDirect + WordDirect + IndexStep +
        Inherent + Branch;
}
