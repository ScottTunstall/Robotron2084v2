namespace RobotronSoundPlayer.Emulation;

/// <summary>
/// The 6800 processor's opcode bytes, named after what they do, with the Motorola mnemonic in each
/// summary (Motorola MC6800 data sheet).
/// </summary>
/// <remarks>
/// Most instructions come in families that differ only in where the value comes from. A family is
/// written as a base opcode plus the operation in its low four bits, and the addressing mode picks
/// the base: see <see cref="Mc6800InstructionSet"/>.
/// </remarks>
internal static class Mc6800Opcodes
{
    /// <summary>NOP: do nothing.</summary>
    internal const byte NoOperation = 0x01;

    /// <summary>TAP: copy accumulator A into the condition codes.</summary>
    internal const byte TransferAToConditionCodes = 0x06;

    /// <summary>TPA: copy the condition codes into accumulator A.</summary>
    internal const byte TransferConditionCodesToA = 0x07;

    /// <summary>INX: add one to the index register.</summary>
    internal const byte IncrementIndex = 0x08;

    /// <summary>DEX: take one from the index register.</summary>
    internal const byte DecrementIndex = 0x09;

    /// <summary>CLV: clear the overflow flag.</summary>
    internal const byte ClearOverflow = 0x0A;

    /// <summary>SEV: set the overflow flag.</summary>
    internal const byte SetOverflow = 0x0B;

    /// <summary>CLC: clear the carry flag.</summary>
    internal const byte ClearCarry = 0x0C;

    /// <summary>SEC: set the carry flag.</summary>
    internal const byte SetCarry = 0x0D;

    /// <summary>CLI: let interrupts in.</summary>
    internal const byte ClearInterruptMask = 0x0E;

    /// <summary>SEI: keep interrupts out.</summary>
    internal const byte SetInterruptMask = 0x0F;

    /// <summary>SBA: take accumulator B from accumulator A.</summary>
    internal const byte SubtractBFromA = 0x10;

    /// <summary>CBA: compare accumulator A with accumulator B.</summary>
    internal const byte CompareAWithB = 0x11;

    /// <summary>TAB: copy accumulator A into accumulator B.</summary>
    internal const byte TransferAToB = 0x16;

    /// <summary>TBA: copy accumulator B into accumulator A.</summary>
    internal const byte TransferBToA = 0x17;

    /// <summary>DAA: fix accumulator A up after a decimal sum.</summary>
    internal const byte DecimalAdjustA = 0x19;

    /// <summary>ABA: add accumulator B to accumulator A.</summary>
    internal const byte AddBToA = 0x1B;

    /// <summary>BRA: always branch.</summary>
    internal const byte BranchAlways = 0x20;

    /// <summary>BHI: branch if higher (unsigned).</summary>
    internal const byte BranchIfHigher = 0x22;

    /// <summary>BLS: branch if lower or the same (unsigned).</summary>
    internal const byte BranchIfLowerOrSame = 0x23;

    /// <summary>BCC: branch if carry is clear.</summary>
    internal const byte BranchIfCarryClear = 0x24;

    /// <summary>BCS: branch if carry is set.</summary>
    internal const byte BranchIfCarrySet = 0x25;

    /// <summary>BNE: branch if not equal (zero is clear).</summary>
    internal const byte BranchIfNotEqual = 0x26;

    /// <summary>BEQ: branch if equal (zero is set).</summary>
    internal const byte BranchIfEqual = 0x27;

    /// <summary>BVC: branch if overflow is clear.</summary>
    internal const byte BranchIfOverflowClear = 0x28;

    /// <summary>BVS: branch if overflow is set.</summary>
    internal const byte BranchIfOverflowSet = 0x29;

    /// <summary>BPL: branch if plus (negative is clear).</summary>
    internal const byte BranchIfPlus = 0x2A;

    /// <summary>BMI: branch if minus (negative is set).</summary>
    internal const byte BranchIfMinus = 0x2B;

    /// <summary>BGE: branch if greater or equal (signed).</summary>
    internal const byte BranchIfGreaterOrEqual = 0x2C;

    /// <summary>BLT: branch if less (signed).</summary>
    internal const byte BranchIfLess = 0x2D;

    /// <summary>BGT: branch if greater (signed).</summary>
    internal const byte BranchIfGreater = 0x2E;

    /// <summary>BLE: branch if less or equal (signed).</summary>
    internal const byte BranchIfLessOrEqual = 0x2F;

    /// <summary>TSX: copy the stack pointer, plus one, into the index register.</summary>
    internal const byte TransferStackToIndex = 0x30;

    /// <summary>INS: add one to the stack pointer.</summary>
    internal const byte IncrementStack = 0x31;

    /// <summary>PULA: take accumulator A off the stack.</summary>
    internal const byte PullA = 0x32;

    /// <summary>PULB: take accumulator B off the stack.</summary>
    internal const byte PullB = 0x33;

    /// <summary>DES: take one from the stack pointer.</summary>
    internal const byte DecrementStack = 0x34;

    /// <summary>TXS: copy the index register, minus one, into the stack pointer.</summary>
    internal const byte TransferIndexToStack = 0x35;

    /// <summary>PSHA: put accumulator A on the stack.</summary>
    internal const byte PushA = 0x36;

    /// <summary>PSHB: put accumulator B on the stack.</summary>
    internal const byte PushB = 0x37;

    /// <summary>RTS: return from a subroutine.</summary>
    internal const byte ReturnFromSubroutine = 0x39;

    /// <summary>RTI: return from an interrupt.</summary>
    internal const byte ReturnFromInterrupt = 0x3B;

    /// <summary>WAI: save the registers and wait for an interrupt.</summary>
    internal const byte WaitForInterrupt = 0x3E;

    /// <summary>SWI: software interrupt.</summary>
    internal const byte SoftwareInterrupt = 0x3F;

    /// <summary>JMP with an indexed address.</summary>
    internal const byte JumpIndexed = 0x6E;

    /// <summary>JMP with a full address.</summary>
    internal const byte JumpExtended = 0x7E;

    /// <summary>BSR: branch to a subroutine.</summary>
    internal const byte BranchToSubroutine = 0x8D;

    /// <summary>JSR with an indexed address.</summary>
    internal const byte JumpToSubroutineIndexed = 0xAD;

    /// <summary>JSR with a full address.</summary>
    internal const byte JumpToSubroutineExtended = 0xBD;

    /// <summary>Base of the one-value operations on accumulator A (NEGA … CLRA).</summary>
    internal const byte UnaryOnA = 0x40;

    /// <summary>Base of the one-value operations on accumulator B (NEGB … CLRB).</summary>
    internal const byte UnaryOnB = 0x50;

    /// <summary>Base of the one-value operations on memory at an indexed address.</summary>
    internal const byte UnaryIndexed = 0x60;

    /// <summary>Base of the one-value operations on memory at a full address.</summary>
    internal const byte UnaryExtended = 0x70;

    /// <summary>Base of the two-value operations on accumulator A with a value in the next byte.</summary>
    internal const byte AccumulatorAFamily = 0x80;

    /// <summary>Base of the two-value operations on accumulator B with a value in the next byte.</summary>
    internal const byte AccumulatorBFamily = 0xC0;

    /// <summary>Added to a family's base when the value is at a direct address.</summary>
    internal const byte DirectMode = 0x10;

    /// <summary>Added to a family's base when the value is at an indexed address.</summary>
    internal const byte IndexedMode = 0x20;

    /// <summary>Added to a family's base when the value is at a full address.</summary>
    internal const byte ExtendedMode = 0x30;

    /// <summary>NEG: make negative.</summary>
    internal const byte Negate = 0x0;

    /// <summary>COM: flip every bit.</summary>
    internal const byte Complement = 0x3;

    /// <summary>LSR: shift right, zero in at the top.</summary>
    internal const byte ShiftRightLogical = 0x4;

    /// <summary>ROR: rotate right through carry.</summary>
    internal const byte RotateRight = 0x6;

    /// <summary>ASR: shift right, keeping the sign.</summary>
    internal const byte ShiftRightArithmetic = 0x7;

    /// <summary>ASL: shift left.</summary>
    internal const byte ShiftLeft = 0x8;

    /// <summary>ROL: rotate left through carry.</summary>
    internal const byte RotateLeft = 0x9;

    /// <summary>DEC: take one away.</summary>
    internal const byte Decrement = 0xA;

    /// <summary>INC: add one.</summary>
    internal const byte Increment = 0xC;

    /// <summary>TST: set the flags from a value.</summary>
    internal const byte Test = 0xD;

    /// <summary>CLR: make zero.</summary>
    internal const byte Clear = 0xF;

    /// <summary>SUB: subtract.</summary>
    internal const byte Subtract = 0x0;

    /// <summary>CMP: compare.</summary>
    internal const byte Compare = 0x1;

    /// <summary>SBC: subtract with carry.</summary>
    internal const byte SubtractWithCarry = 0x2;

    /// <summary>AND: keep only the bits set in both.</summary>
    internal const byte And = 0x4;

    /// <summary>BIT: test the bits set in both, without changing the accumulator.</summary>
    internal const byte BitTest = 0x5;

    /// <summary>LDA/LDB: load.</summary>
    internal const byte Load = 0x6;

    /// <summary>STA/STB: store.</summary>
    internal const byte Store = 0x7;

    /// <summary>EOR: flip the bits set in the value.</summary>
    internal const byte ExclusiveOr = 0x8;

    /// <summary>ADC: add with carry.</summary>
    internal const byte AddWithCarry = 0x9;

    /// <summary>ORA/ORB: set the bits set in the value.</summary>
    internal const byte Or = 0xA;

    /// <summary>ADD: add.</summary>
    internal const byte Add = 0xB;

    /// <summary>CPX (in accumulator A's family): compare the index register.</summary>
    internal const byte CompareIndex = 0xC;

    /// <summary>LDS (in A's family) or LDX (in B's family): load a 16-bit register.</summary>
    internal const byte LoadWord = 0xE;

    /// <summary>STS (in A's family) or STX (in B's family): store a 16-bit register.</summary>
    internal const byte StoreWord = 0xF;
}
