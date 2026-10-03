namespace RobotronSoundPlayer.Emulation;

/// <summary>The six flags a 6800 processor keeps about the result of its last instruction.</summary>
[Flags]
public enum Mc6800ConditionCodes : byte
{
    /// <summary>No flags are set.</summary>
    None = 0,

    /// <summary>The last sum carried out of the top bit, or the last subtraction had to borrow.</summary>
    Carry = 0x01,

    /// <summary>The last signed sum or difference was too big or too small to fit.</summary>
    Overflow = 0x02,

    /// <summary>The last result was zero.</summary>
    Zero = 0x04,

    /// <summary>The last result had its top bit set, so it is negative when read as a signed number.</summary>
    Negative = 0x08,

    /// <summary>While this is set the processor ignores requests to stop and answer a device.</summary>
    InterruptMask = 0x10,

    /// <summary>The last sum carried out of its low four bits (used when adding decimal digits).</summary>
    HalfCarry = 0x20,
}
