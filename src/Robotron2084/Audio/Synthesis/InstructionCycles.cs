namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     How many clock cycles each kind of 6800 instruction takes (Motorola MC6800 data sheet). The sound
///     board's program times every sound by counting instructions, so the port times its sounds with these.
/// </summary>
/// <remarks>
///     The board's variables sit in the first 128 bytes of memory, so the source's loads, stores, adds and
///     compares of them are "direct" instructions. The one-value operations (<c>CLR</c>, <c>COM</c>,
///     <c>DEC</c>, <c>INC</c>, <c>ROR</c>, <c>LSR</c>, <c>TST</c>) have no direct form and use a full
///     address, as does every access to the output port <c>SOUND</c>.
/// </remarks>
internal static class InstructionCycles
{
    /// <summary>
    ///     An instruction on registers only: <c>TAB</c>, <c>ABA</c>, <c>LSRA</c>, <c>DECB</c>, <c>COMA</c>, <c>NOP</c>
    ///     and the like.
    /// </summary>
    public const int Inherent = 2;

    /// <summary><c>INX</c> or <c>DEX</c>.</summary>
    public const int IndexStep = 4;

    /// <summary><c>PSHA</c> or <c>PULA</c>.</summary>
    public const int Stack = 4;

    /// <summary>Any branch, taken or not.</summary>
    public const int Branch = 4;

    /// <summary><c>RTS</c>.</summary>
    public const int Return = 5;

    /// <summary><c>JMP</c> to a full address.</summary>
    public const int JumpExtended = 3;

    /// <summary><c>BSR</c>, or <c>JSR</c> through the index register.</summary>
    public const int CallShort = 8;

    /// <summary><c>JSR</c> to a full address.</summary>
    public const int CallExtended = 9;

    /// <summary>
    ///     A one-value operation on memory at a full address: <c>CLR</c>, <c>COM</c>, <c>DEC</c>, <c>INC</c>, <c>ROR</c>,
    ///     <c>TST</c>.
    /// </summary>
    public const int ModifyExtended = 6;

    /// <summary>A one-value operation on memory through the index register.</summary>
    public const int ModifyIndexed = 7;

    /// <summary>An 8-bit load or sum with the value in the instruction.</summary>
    public const int Immediate = 2;

    /// <summary>An 8-bit load or sum of a variable.</summary>
    public const int Direct = 3;

    /// <summary>An 8-bit load or sum through the index register.</summary>
    public const int Indexed = 5;

    /// <summary>An 8-bit load from a full address, such as reading the output port.</summary>
    public const int Extended = 4;

    /// <summary>An 8-bit store to a variable.</summary>
    public const int StoreDirect = 4;

    /// <summary>An 8-bit store through the index register.</summary>
    public const int StoreIndexed = 6;

    /// <summary>An 8-bit store to a full address, such as writing the output port.</summary>
    public const int StoreExtended = 5;

    /// <summary>A 16-bit load or compare with the value in the instruction (<c>LDX #</c>, <c>CPX #</c>, <c>LDS #</c>).</summary>
    public const int WordImmediate = 3;

    /// <summary>A 16-bit load or compare of a variable.</summary>
    public const int WordDirect = 4;

    /// <summary>A 16-bit load through the index register.</summary>
    public const int WordIndexed = 6;

    /// <summary>A 16-bit store to a variable.</summary>
    public const int WordStoreDirect = 5;

    /// <summary>Answering an interrupt: saving the registers and fetching the handler's address.</summary>
    public const int Interrupt = 12;
}
