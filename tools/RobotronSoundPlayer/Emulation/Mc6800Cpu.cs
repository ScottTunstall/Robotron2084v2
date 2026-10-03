namespace RobotronSoundPlayer.Emulation;

/// <summary>
/// The processor on the arcade's sound board. It runs the sound board's own program, which makes
/// every noise the game plays.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: none. The sound board's program is not in the original source; the main
/// board only sends it a sound number (<c>RRS22.ASM</c>, routine <c>SNDOUT</c>).</item>
/// <item>Disassembly: not in <c>asm/robomame.asm</c>, which covers the main board's processor only.</item>
/// </list>
/// The board's chip is a Motorola 6808: a 6800 with no memory of its own, so it has the 6800's
/// instructions and timings.
/// </remarks>
public sealed class Mc6800Cpu
{
    /// <summary>Where the address of the interrupt routine is kept.</summary>
    private const ushort InterruptVector = 0xFFF8;

    /// <summary>Where the address of the software-interrupt routine is kept.</summary>
    private const ushort SoftwareInterruptVector = 0xFFFA;

    /// <summary>Where the address of the start-up routine is kept.</summary>
    private const ushort ResetVector = 0xFFFE;

    /// <summary>Clock cycles it takes to answer an interrupt: save every register, then jump.</summary>
    private const int InterruptCycles = 12;

    /// <summary>Clock cycles that pass on each step while the processor waits for an interrupt.</summary>
    private const int WaitCycles = 1;

    /// <summary>The top two bits of the condition codes, which always read as ones.</summary>
    private const byte UnusedConditionCodeBits = 0xC0;

    /// <summary>Bits in a byte, used to split and join 16-bit words.</summary>
    private const int BitsPerByte = 8;

    private readonly IMc6800Bus _bus;
    private bool _isWaitingForInterrupt;

    /// <summary>Creates a processor wired to its memory and chips. Call <see cref="Reset"/> before running it.</summary>
    /// <param name="bus">The memory and chips the processor reads and writes.</param>
    public Mc6800Cpu(IMc6800Bus bus) => _bus = bus;

    /// <summary>Accumulator A.</summary>
    public byte A { get; internal set; }

    /// <summary>Accumulator B.</summary>
    public byte B { get; internal set; }

    /// <summary>The flags left by the last instruction.</summary>
    public Mc6800ConditionCodes ConditionCodes { get; internal set; }

    /// <summary>True while a chip is asking the processor to stop and answer it.</summary>
    public bool IsInterruptRequested { get; set; }

    /// <summary>The address of the next instruction.</summary>
    public ushort ProgramCounter { get; internal set; }

    /// <summary>The address of the next free byte on the stack.</summary>
    public ushort StackPointer { get; internal set; }

    /// <summary>The index register.</summary>
    public ushort X { get; internal set; }

    /// <summary>Starts the processor from the beginning, the way switching the machine on does.</summary>
    public void Reset()
    {
        ConditionCodes |= Mc6800ConditionCodes.InterruptMask;
        _isWaitingForInterrupt = false;
        ProgramCounter = ReadWord(ResetVector);
    }

    /// <summary>Runs one instruction, or answers an interrupt, and says how long that took.</summary>
    /// <returns>The clock cycles used.</returns>
    public int Step()
    {
        if (IsInterruptRequested && !HasFlag(Mc6800ConditionCodes.InterruptMask))
        {
            return AnswerInterrupt();
        }

        if (_isWaitingForInterrupt)
        {
            return WaitCycles;
        }

        Mc6800Instruction instruction = Mc6800InstructionSet.For(FetchByte());
        instruction.Execute(this);
        return instruction.Cycles;
    }

    /// <summary>Moves on by the offset in the next byte when the condition holds (the branch instructions).</summary>
    /// <param name="condition">Whether to take the branch.</param>
    internal void BranchIf(bool condition)
    {
        var offset = (sbyte)FetchByte();
        if (condition)
        {
            ProgramCounter = (ushort)(ProgramCounter + offset);
        }
    }

    /// <summary>Saves where to come back to, then moves on by the offset in the next byte (BSR).</summary>
    internal void BranchToSubroutine()
    {
        var offset = (sbyte)FetchByte();
        PushWord(ProgramCounter);
        ProgramCounter = (ushort)(ProgramCounter + offset);
    }

    /// <summary>The condition codes as the byte the processor stores for them (TPA, and when saving registers).</summary>
    /// <returns>The flags, with the two unused top bits set.</returns>
    internal byte ConditionCodesAsByte() => (byte)((byte)ConditionCodes | UnusedConditionCodeBits);

    /// <summary>The address in the next byte, which points into the first 256 bytes of memory.</summary>
    /// <returns>The address.</returns>
    internal ushort DirectAddress() => FetchByte();

    /// <summary>The address in the next two bytes.</summary>
    /// <returns>The address.</returns>
    internal ushort ExtendedAddress() => FetchWord();

    /// <summary>Reads the next byte of the program and moves past it.</summary>
    /// <returns>The byte.</returns>
    internal byte FetchByte() => _bus.ReadByte(ProgramCounter++);

    /// <summary>Reads the next two bytes of the program, high byte first, and moves past them.</summary>
    /// <returns>The 16-bit word.</returns>
    internal ushort FetchWord()
    {
        byte high = FetchByte();
        byte low = FetchByte();
        return Word(high, low);
    }

    /// <summary>The value in one of the two accumulators.</summary>
    /// <param name="accumulator">Which accumulator.</param>
    /// <returns>Its value.</returns>
    internal byte GetAccumulator(Mc6800Accumulator accumulator) => accumulator == Mc6800Accumulator.A ? A : B;

    /// <summary>Whether a flag is set.</summary>
    /// <param name="flag">The flag to test.</param>
    /// <returns>True when it is set.</returns>
    internal bool HasFlag(Mc6800ConditionCodes flag) => (ConditionCodes & flag) != 0;

    /// <summary>The index register plus the offset in the next byte.</summary>
    /// <returns>The address.</returns>
    internal ushort IndexedAddress() => (ushort)(X + FetchByte());

    /// <summary>True when the last comparison found the left number smaller, reading both as signed numbers.</summary>
    /// <returns>Whether the negative and overflow flags differ.</returns>
    internal bool IsSignedLess() => HasFlag(Mc6800ConditionCodes.Negative) != HasFlag(Mc6800ConditionCodes.Overflow);

    /// <summary>Saves where to come back to, then jumps to a routine (JSR).</summary>
    /// <param name="address">The routine's address.</param>
    internal void JumpToSubroutine(ushort address)
    {
        PushWord(ProgramCounter);
        ProgramCounter = address;
    }

    /// <summary>Sets the condition codes from a byte (TAP, and when restoring registers).</summary>
    /// <param name="value">The byte; its two unused top bits are ignored.</param>
    internal void LoadConditionCodes(byte value) =>
        ConditionCodes = (Mc6800ConditionCodes)(value & ~UnusedConditionCodeBits);

    /// <summary>Takes the last byte off the stack.</summary>
    /// <returns>The byte.</returns>
    internal byte Pull()
    {
        StackPointer++;
        return _bus.ReadByte(StackPointer);
    }

    /// <summary>Puts a byte on the stack.</summary>
    /// <param name="value">The byte.</param>
    internal void Push(byte value)
    {
        _bus.WriteByte(StackPointer, value);
        StackPointer--;
    }

    /// <summary>Reads the byte at an address.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The byte.</returns>
    internal byte ReadByte(ushort address) => _bus.ReadByte(address);

    /// <summary>Reads the 16-bit word at an address, high byte first.</summary>
    /// <param name="address">The address of the high byte.</param>
    /// <returns>The word.</returns>
    internal ushort ReadWord(ushort address)
    {
        byte high = _bus.ReadByte(address);
        byte low = _bus.ReadByte((ushort)(address + 1));
        return Word(high, low);
    }

    /// <summary>Puts back every register an interrupt saved, and carries on where it left off (RTI).</summary>
    internal void ReturnFromInterrupt()
    {
        LoadConditionCodes(Pull());
        B = Pull();
        A = Pull();
        X = PullWord();
        ProgramCounter = PullWord();
    }

    /// <summary>Goes back to where the last subroutine call came from (RTS).</summary>
    internal void ReturnFromSubroutine() => ProgramCounter = PullWord();

    /// <summary>Sets one of the two accumulators.</summary>
    /// <param name="accumulator">Which accumulator.</param>
    /// <param name="value">Its new value.</param>
    internal void SetAccumulator(Mc6800Accumulator accumulator, byte value)
    {
        if (accumulator == Mc6800Accumulator.A)
        {
            A = value;
            return;
        }

        B = value;
    }

    /// <summary>Sets or clears one flag.</summary>
    /// <param name="flag">The flag.</param>
    /// <param name="isSet">True to set it, false to clear it.</param>
    internal void SetFlag(Mc6800ConditionCodes flag, bool isSet)
    {
        if (isSet)
        {
            ConditionCodes |= flag;
            return;
        }

        ConditionCodes &= ~flag;
    }

    /// <summary>Saves every register and jumps to the software-interrupt routine (SWI).</summary>
    internal void SoftwareInterrupt()
    {
        SaveRegisters();
        SetFlag(Mc6800ConditionCodes.InterruptMask, true);
        ProgramCounter = ReadWord(SoftwareInterruptVector);
    }

    /// <summary>Saves every register, then sits still until an interrupt arrives (WAI).</summary>
    internal void WaitForInterrupt()
    {
        SaveRegisters();
        _isWaitingForInterrupt = true;
    }

    /// <summary>Writes a byte to an address.</summary>
    /// <param name="address">The address.</param>
    /// <param name="value">The byte.</param>
    internal void WriteByte(ushort address, byte value) => _bus.WriteByte(address, value);

    /// <summary>Writes a 16-bit word to an address, high byte first.</summary>
    /// <param name="address">The address for the high byte.</param>
    /// <param name="value">The word.</param>
    internal void WriteWord(ushort address, ushort value)
    {
        _bus.WriteByte(address, High(value));
        _bus.WriteByte((ushort)(address + 1), Low(value));
    }

    /// <summary>The high byte of a word.</summary>
    private static byte High(ushort value) => (byte)(value >> BitsPerByte);

    /// <summary>The low byte of a word.</summary>
    private static byte Low(ushort value) => (byte)value;

    /// <summary>Joins a high byte and a low byte into a word.</summary>
    private static ushort Word(byte high, byte low) => (ushort)((high << BitsPerByte) | low);

    /// <summary>Saves every register (unless a WAI already did) and jumps to the interrupt routine.</summary>
    /// <returns>The clock cycles used.</returns>
    private int AnswerInterrupt()
    {
        if (!_isWaitingForInterrupt)
        {
            SaveRegisters();
        }

        _isWaitingForInterrupt = false;
        SetFlag(Mc6800ConditionCodes.InterruptMask, true);
        ProgramCounter = ReadWord(InterruptVector);
        return InterruptCycles;
    }

    /// <summary>Takes a word off the stack, high byte first.</summary>
    private ushort PullWord()
    {
        byte high = Pull();
        byte low = Pull();
        return Word(high, low);
    }

    /// <summary>Puts a word on the stack so that it comes back off high byte first.</summary>
    private void PushWord(ushort value)
    {
        Push(Low(value));
        Push(High(value));
    }

    /// <summary>Puts every register on the stack, in the order an interrupt saves them.</summary>
    private void SaveRegisters()
    {
        PushWord(ProgramCounter);
        PushWord(X);
        Push(A);
        Push(B);
        Push(ConditionCodesAsByte());
    }
}
