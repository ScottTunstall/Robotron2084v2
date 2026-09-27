namespace Robotron2084.Audio.Hardware;

/// <summary>
/// The 6800 processor's arithmetic: the sums, shifts and tests its instructions do, and the flags
/// each one leaves behind (Motorola MC6800 data sheet).
/// </summary>
internal static class Mc6800Alu
{
    /// <summary>The top bit of a byte: set when the byte is negative.</summary>
    private const byte SignBit = 0x80;

    /// <summary>The top bit of a 16-bit word: set when the word is negative.</summary>
    private const ushort WordSignBit = 0x8000;

    /// <summary>The largest positive signed byte; one more overflows.</summary>
    private const byte LargestPositive = 0x7F;

    /// <summary>The bottom bit of a byte.</summary>
    private const byte LowBit = 0x01;

    /// <summary>The bit a sum's low four bits carry into.</summary>
    private const int HalfCarryBit = 0x10;

    /// <summary>The smallest sum that no longer fits in a byte.</summary>
    private const int ByteCarry = 0x100;

    /// <summary>The low four bits of a byte: one decimal digit.</summary>
    private const byte LowDigitMask = 0x0F;

    /// <summary>Bits in one decimal digit of a packed decimal byte.</summary>
    private const int BitsPerDigit = 4;

    /// <summary>The largest value a decimal digit may hold.</summary>
    private const int LargestDigit = 9;

    /// <summary>What decimal adjust adds to fix the low digit.</summary>
    private const int LowDigitCorrection = 0x06;

    /// <summary>What decimal adjust adds to fix the high digit.</summary>
    private const int HighDigitCorrection = 0x60;

    /// <summary>Adds two bytes, and the carry flag too when asked (ADD, ADC, ABA).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <param name="withCarry">True to add the carry flag as well.</param>
    /// <returns>The sum.</returns>
    internal static byte Add(Mc6800Cpu cpu, byte left, byte right, bool withCarry)
    {
        int carryIn = withCarry && cpu.HasFlag(Mc6800ConditionCodes.Carry) ? 1 : 0;
        int sum = left + right + carryIn;
        var result = (byte)sum;
        cpu.SetFlag(Mc6800ConditionCodes.HalfCarry, ((left ^ right ^ sum) & HalfCarryBit) != 0);
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, ((left ^ result) & (right ^ result) & SignBit) != 0);
        cpu.SetFlag(Mc6800ConditionCodes.Carry, sum >= ByteCarry);
        SetNegativeAndZero(cpu, result);
        return result;
    }

    /// <summary>Makes a byte zero (CLR).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <returns>Zero.</returns>
    internal static byte Clear(Mc6800Cpu cpu)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Carry, false);
        return Load(cpu, 0);
    }

    /// <summary>Compares the index register with a word, keeping only the flags (CPX).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="operand">The word to compare with.</param>
    internal static void CompareIndex(Mc6800Cpu cpu, ushort operand)
    {
        ushort index = cpu.X;
        var result = (ushort)(index - operand);
        cpu.SetFlag(Mc6800ConditionCodes.Zero, result == 0);
        cpu.SetFlag(Mc6800ConditionCodes.Negative, (result & WordSignBit) != 0);
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, ((index ^ operand) & (index ^ result) & WordSignBit) != 0);
    }

    /// <summary>Flips every bit of a byte (COM).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The flipped byte.</returns>
    internal static byte Complement(Mc6800Cpu cpu, byte value)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Carry, true);
        return Load(cpu, (byte)~value);
    }

    /// <summary>Fixes up accumulator A after adding two decimal numbers, so each half holds one digit again (DAA).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte to fix.</param>
    /// <returns>The fixed byte.</returns>
    internal static byte DecimalAdjust(Mc6800Cpu cpu, byte value)
    {
        int lowDigit = value & LowDigitMask;
        int highDigit = value >> BitsPerDigit;
        bool carry = cpu.HasFlag(Mc6800ConditionCodes.Carry);
        int correction = 0;
        if (lowDigit > LargestDigit || cpu.HasFlag(Mc6800ConditionCodes.HalfCarry))
        {
            correction |= LowDigitCorrection;
        }

        if (carry || highDigit > LargestDigit || (highDigit >= LargestDigit && lowDigit > LargestDigit))
        {
            correction |= HighDigitCorrection;
            carry = true;
        }

        var result = (byte)(value + correction);
        cpu.SetFlag(Mc6800ConditionCodes.Carry, carry);
        SetNegativeAndZero(cpu, result);
        return result;
    }

    /// <summary>Takes one away from a byte (DEC).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>One less.</returns>
    internal static byte Decrement(Mc6800Cpu cpu, byte value)
    {
        var result = (byte)(value - 1);
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, value == SignBit);
        SetNegativeAndZero(cpu, result);
        return result;
    }

    /// <summary>Adds one to a byte (INC).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>One more.</returns>
    internal static byte Increment(Mc6800Cpu cpu, byte value)
    {
        var result = (byte)(value + 1);
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, value == LargestPositive);
        SetNegativeAndZero(cpu, result);
        return result;
    }

    /// <summary>Sets the flags for a byte that has been loaded, stored or combined bit by bit (LDA, STA, AND, ORA, EOR, BIT, TAB, TBA).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The same byte.</returns>
    internal static byte Load(Mc6800Cpu cpu, byte value)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, false);
        SetNegativeAndZero(cpu, value);
        return value;
    }

    /// <summary>Sets the flags for a 16-bit word that has been loaded or stored (LDX, LDS, STX, STS).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The word.</param>
    /// <returns>The same word.</returns>
    internal static ushort LoadWord(Mc6800Cpu cpu, ushort value)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, false);
        cpu.SetFlag(Mc6800ConditionCodes.Negative, (value & WordSignBit) != 0);
        cpu.SetFlag(Mc6800ConditionCodes.Zero, value == 0);
        return value;
    }

    /// <summary>Turns a byte into its negative (NEG).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>Zero minus the byte.</returns>
    internal static byte Negate(Mc6800Cpu cpu, byte value)
    {
        var result = (byte)-value;
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, result == SignBit);
        cpu.SetFlag(Mc6800ConditionCodes.Carry, result != 0);
        SetNegativeAndZero(cpu, result);
        return result;
    }

    /// <summary>Moves every bit one place left, the carry flag coming in at the bottom (ROL).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The rotated byte.</returns>
    internal static byte RotateLeft(Mc6800Cpu cpu, byte value)
    {
        int carryIn = cpu.HasFlag(Mc6800ConditionCodes.Carry) ? LowBit : 0;
        var result = (byte)((value << 1) | carryIn);
        return Shifted(cpu, result, (value & SignBit) != 0);
    }

    /// <summary>Moves every bit one place right, the carry flag coming in at the top (ROR).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The rotated byte.</returns>
    internal static byte RotateRight(Mc6800Cpu cpu, byte value)
    {
        int carryIn = cpu.HasFlag(Mc6800ConditionCodes.Carry) ? SignBit : 0;
        var result = (byte)((value >> 1) | carryIn);
        return Shifted(cpu, result, (value & LowBit) != 0);
    }

    /// <summary>Moves every bit one place left, a zero coming in at the bottom (ASL).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The shifted byte.</returns>
    internal static byte ShiftLeft(Mc6800Cpu cpu, byte value)
    {
        var result = (byte)(value << 1);
        return Shifted(cpu, result, (value & SignBit) != 0);
    }

    /// <summary>Moves every bit one place right, keeping the top bit so the sign stays (ASR).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The shifted byte.</returns>
    internal static byte ShiftRightArithmetic(Mc6800Cpu cpu, byte value)
    {
        var result = (byte)((value >> 1) | (value & SignBit));
        return Shifted(cpu, result, (value & LowBit) != 0);
    }

    /// <summary>Moves every bit one place right, a zero coming in at the top (LSR).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The shifted byte.</returns>
    internal static byte ShiftRightLogical(Mc6800Cpu cpu, byte value)
    {
        var result = (byte)(value >> 1);
        return Shifted(cpu, result, (value & LowBit) != 0);
    }

    /// <summary>Takes one byte from another, and the carry flag too when asked (SUB, SBC, CMP, SBA, CBA).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="left">The number to take from.</param>
    /// <param name="right">The number to take away.</param>
    /// <param name="withCarry">True to take the carry flag away as well.</param>
    /// <returns>The difference.</returns>
    internal static byte Subtract(Mc6800Cpu cpu, byte left, byte right, bool withCarry)
    {
        int borrowIn = withCarry && cpu.HasFlag(Mc6800ConditionCodes.Carry) ? 1 : 0;
        int difference = left - right - borrowIn;
        var result = (byte)difference;
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, ((left ^ right) & (left ^ result) & SignBit) != 0);
        cpu.SetFlag(Mc6800ConditionCodes.Carry, difference < 0);
        SetNegativeAndZero(cpu, result);
        return result;
    }

    /// <summary>Sets the flags for a byte without changing it (TST).</summary>
    /// <param name="cpu">The processor whose flags are set.</param>
    /// <param name="value">The byte.</param>
    /// <returns>The same byte.</returns>
    internal static byte Test(Mc6800Cpu cpu, byte value)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Carry, false);
        return Load(cpu, value);
    }

    /// <summary>Sets the negative and zero flags from a result.</summary>
    private static void SetNegativeAndZero(Mc6800Cpu cpu, byte result)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Negative, (result & SignBit) != 0);
        cpu.SetFlag(Mc6800ConditionCodes.Zero, result == 0);
    }

    /// <summary>Sets the flags a shift or rotate leaves: the bit pushed out goes to carry, and overflow is negative-differs-from-carry.</summary>
    private static byte Shifted(Mc6800Cpu cpu, byte result, bool carryOut)
    {
        cpu.SetFlag(Mc6800ConditionCodes.Carry, carryOut);
        SetNegativeAndZero(cpu, result);
        cpu.SetFlag(Mc6800ConditionCodes.Overflow, cpu.HasFlag(Mc6800ConditionCodes.Negative) != carryOut);
        return result;
    }
}
