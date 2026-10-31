using static RobotronSoundPlayer.Emulation.Mc6800Opcodes;

namespace RobotronSoundPlayer.Emulation;

/// <summary>
///     The 6800 processor's instruction set: for each of the 256 possible opcode bytes, what the
///     instruction does and how many clock cycles it takes (Motorola MC6800 data sheet).
/// </summary>
/// <remarks>A byte that is not a 6800 instruction does nothing for two cycles.</remarks>
internal static class Mc6800InstructionSet
{
    /// <summary>Cycles for an instruction that works on registers only.</summary>
    private const int InherentCycles = 2;

    /// <summary>Cycles for INX, DEX and the stack instructions.</summary>
    private const int RegisterWordCycles = 4;

    /// <summary>Cycles for any branch, taken or not.</summary>
    private const int BranchCycles = 4;

    /// <summary>Cycles for RTS.</summary>
    private const int ReturnFromSubroutineCycles = 5;

    /// <summary>Cycles for RTI.</summary>
    private const int ReturnFromInterruptCycles = 10;

    /// <summary>Cycles for WAI.</summary>
    private const int WaitCycles = 9;

    /// <summary>Cycles for SWI.</summary>
    private const int SoftwareInterruptCycles = 12;

    /// <summary>Cycles for JMP with an indexed address.</summary>
    private const int JumpIndexedCycles = 4;

    /// <summary>Cycles for JMP with a full address.</summary>
    private const int JumpExtendedCycles = 3;

    /// <summary>Cycles for BSR and JSR with an indexed address.</summary>
    private const int CallCycles = 8;

    /// <summary>Cycles for JSR with a full address.</summary>
    private const int CallExtendedCycles = 9;

    /// <summary>Cycles for a one-value operation on memory at an indexed address.</summary>
    private const int UnaryIndexedCycles = 7;

    /// <summary>Cycles for a one-value operation on memory at a full address.</summary>
    private const int UnaryExtendedCycles = 6;

    /// <summary>Cycles for an 8-bit operation with its value in the next byte.</summary>
    private const int ImmediateByteCycles = 2;

    /// <summary>Cycles for an 8-bit operation with its value at a direct address.</summary>
    private const int DirectByteCycles = 3;

    /// <summary>Cycles for an 8-bit operation with its value at an indexed address.</summary>
    private const int IndexedByteCycles = 5;

    /// <summary>Cycles for an 8-bit operation with its value at a full address.</summary>
    private const int ExtendedByteCycles = 4;

    /// <summary>Cycles for STA/STB at a direct address.</summary>
    private const int DirectStoreCycles = 4;

    /// <summary>Cycles for STA/STB at an indexed address.</summary>
    private const int IndexedStoreCycles = 6;

    /// <summary>Cycles for STA/STB at a full address.</summary>
    private const int ExtendedStoreCycles = 5;

    /// <summary>Cycles for a 16-bit load or compare with its value in the next two bytes.</summary>
    private const int ImmediateWordCycles = 3;

    /// <summary>Cycles for a 16-bit load or compare with its value at a direct address.</summary>
    private const int DirectWordCycles = 4;

    /// <summary>Cycles for a 16-bit load or compare with its value at an indexed address.</summary>
    private const int IndexedWordCycles = 6;

    /// <summary>Cycles for a 16-bit load or compare with its value at a full address.</summary>
    private const int ExtendedWordCycles = 5;

    /// <summary>Cycles for a 16-bit store at a direct address.</summary>
    private const int DirectWordStoreCycles = 5;

    /// <summary>Cycles for a 16-bit store at an indexed address.</summary>
    private const int IndexedWordStoreCycles = 7;

    /// <summary>Cycles for a 16-bit store at a full address.</summary>
    private const int ExtendedWordStoreCycles = 6;

    /// <summary>Every opcode's instruction, indexed by the opcode byte.</summary>
    private static readonly Mc6800Instruction[] Instructions = Build();

    /// <summary>The instruction an opcode byte stands for.</summary>
    /// <param name="opcode">The opcode byte.</param>
    /// <returns>Its instruction.</returns>
    internal static Mc6800Instruction GetInstruction(byte opcode)
    {
        return Instructions[opcode];
    }

    /// <summary>Fills in every opcode.</summary>
    private static Mc6800Instruction[] Build()
    {
        var set = new Mc6800Instruction[byte.MaxValue + 1];
        Array.Fill(set, new Mc6800Instruction(_ => { }, InherentCycles));
        AddFlagInstructions(set);
        AddRegisterInstructions(set);
        AddBranches(set);
        AddStackInstructions(set);
        AddUnaryOperations(set);
        AddAccumulatorOperations(set);
        AddStores(set);
        AddWordOperations(set);
        AddJumps(set);
        return set;
    }

    /// <summary>Puts one instruction in the table.</summary>
    /// <param name="set">The table.</param>
    /// <param name="opcode">The opcode byte.</param>
    /// <param name="execute">What the instruction does.</param>
    /// <param name="cycles">How many clock cycles it takes.</param>
    private static void Define(Mc6800Instruction[] set, int opcode, Action<Mc6800Cpu> execute,
        int cycles = InherentCycles)
    {
        set[opcode] = new Mc6800Instruction(execute, cycles);
    }

    /// <summary>The instructions that set and clear single flags, or copy the flags to and from accumulator A.</summary>
    private static void AddFlagInstructions(Mc6800Instruction[] set)
    {
        Define(set, NoOperation, _ => { });
        Define(set, TransferAToConditionCodes, mc6800Cpu => mc6800Cpu.LoadConditionCodes(mc6800Cpu.A));
        Define(set, TransferConditionCodesToA, mc6800Cpu => mc6800Cpu.A = mc6800Cpu.ConditionCodesAsByte());
        Define(set, ClearOverflow, mc6800Cpu => mc6800Cpu.SetFlag(Mc6800ConditionCodes.Overflow, false));
        Define(set, SetOverflow, mc6800Cpu => mc6800Cpu.SetFlag(Mc6800ConditionCodes.Overflow, true));
        Define(set, ClearCarry, mc6800Cpu => mc6800Cpu.SetFlag(Mc6800ConditionCodes.Carry, false));
        Define(set, SetCarry, mc6800Cpu => mc6800Cpu.SetFlag(Mc6800ConditionCodes.Carry, true));
        Define(set, ClearInterruptMask, mc6800Cpu => mc6800Cpu.SetFlag(Mc6800ConditionCodes.InterruptMask, false));
        Define(set, SetInterruptMask, mc6800Cpu => mc6800Cpu.SetFlag(Mc6800ConditionCodes.InterruptMask, true));
    }

    /// <summary>The instructions that work between the two accumulators, and on the index register.</summary>
    private static void AddRegisterInstructions(Mc6800Instruction[] set)
    {
        Define(set, SubtractBFromA, mc6800Cpu => mc6800Cpu.A = Mc6800Alu.Subtract(mc6800Cpu, mc6800Cpu.A, mc6800Cpu.B, false));
        Define(set, CompareAWithB, mc6800Cpu => Mc6800Alu.Subtract(mc6800Cpu, mc6800Cpu.A, mc6800Cpu.B, false));
        Define(set, TransferAToB, mc6800Cpu => mc6800Cpu.B = Mc6800Alu.Load(mc6800Cpu, mc6800Cpu.A));
        Define(set, TransferBToA, mc6800Cpu => mc6800Cpu.A = Mc6800Alu.Load(mc6800Cpu, mc6800Cpu.B));
        Define(set, DecimalAdjustA, mc6800Cpu => mc6800Cpu.A = Mc6800Alu.DecimalAdjust(mc6800Cpu, mc6800Cpu.A));
        Define(set, AddBToA, mc6800Cpu => mc6800Cpu.A = Mc6800Alu.Add(mc6800Cpu, mc6800Cpu.A, mc6800Cpu.B, false));
        Define(set, IncrementIndex, mc6800Cpu => StepIndex(mc6800Cpu, 1), RegisterWordCycles);
        Define(set, DecrementIndex, mc6800Cpu => StepIndex(mc6800Cpu, -1), RegisterWordCycles);
    }

    /// <summary>Moves the index register by one and sets the zero flag from it (INX, DEX).</summary>
    private static void StepIndex(Mc6800Cpu mc6800Cpu, int step)
    {
        mc6800Cpu.X = (ushort)(mc6800Cpu.X + step);
        mc6800Cpu.SetFlag(Mc6800ConditionCodes.Zero, mc6800Cpu.X == 0);
    }

    /// <summary>The conditional and unconditional branches.</summary>
    private static void AddBranches(Mc6800Instruction[] set)
    {
        AddBranch(set, BranchAlways, _ => true);
        AddBranch(set, BranchIfHigher,
            mc6800Cpu => !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Carry) && !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Zero));
        AddBranch(set, BranchIfLowerOrSame,
            mc6800Cpu => mc6800Cpu.HasFlag(Mc6800ConditionCodes.Carry) || mc6800Cpu.HasFlag(Mc6800ConditionCodes.Zero));
        AddBranch(set, BranchIfCarryClear, mc6800Cpu => !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Carry));
        AddBranch(set, BranchIfCarrySet, mc6800Cpu => mc6800Cpu.HasFlag(Mc6800ConditionCodes.Carry));
        AddBranch(set, BranchIfNotEqual, mc6800Cpu => !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Zero));
        AddBranch(set, BranchIfEqual, mc6800Cpu => mc6800Cpu.HasFlag(Mc6800ConditionCodes.Zero));
        AddBranch(set, BranchIfOverflowClear, mc6800Cpu => !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Overflow));
        AddBranch(set, BranchIfOverflowSet, mc6800Cpu => mc6800Cpu.HasFlag(Mc6800ConditionCodes.Overflow));
        AddBranch(set, BranchIfPlus, mc6800Cpu => !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Negative));
        AddBranch(set, BranchIfMinus, mc6800Cpu => mc6800Cpu.HasFlag(Mc6800ConditionCodes.Negative));
        AddBranch(set, BranchIfGreaterOrEqual, mc6800Cpu => !mc6800Cpu.IsSignedLess());
        AddBranch(set, BranchIfLess, mc6800Cpu => mc6800Cpu.IsSignedLess());
        AddBranch(set, BranchIfGreater, mc6800Cpu => !mc6800Cpu.HasFlag(Mc6800ConditionCodes.Zero) && !mc6800Cpu.IsSignedLess());
        AddBranch(set, BranchIfLessOrEqual, mc6800Cpu => mc6800Cpu.HasFlag(Mc6800ConditionCodes.Zero) || mc6800Cpu.IsSignedLess());
        Define(set, BranchToSubroutine, mc6800Cpu => mc6800Cpu.BranchToSubroutine(), CallCycles);
    }

    /// <summary>Puts one branch in the table.</summary>
    private static void AddBranch(Mc6800Instruction[] set, int opcode, Func<Mc6800Cpu, bool> condition)
    {
        Define(set, opcode, mc6800Cpu => mc6800Cpu.BranchIf(condition(mc6800Cpu)), BranchCycles);
    }

    /// <summary>The stack instructions, returns and interrupts.</summary>
    private static void AddStackInstructions(Mc6800Instruction[] set)
    {
        Define(set, TransferStackToIndex, mc6800Cpu => mc6800Cpu.X = (ushort)(mc6800Cpu.StackPointer + 1), RegisterWordCycles);
        Define(set, IncrementStack, mc6800Cpu => mc6800Cpu.StackPointer++, RegisterWordCycles);
        Define(set, PullA, mc6800Cpu => mc6800Cpu.A = mc6800Cpu.Pull(), RegisterWordCycles);
        Define(set, PullB, mc6800Cpu => mc6800Cpu.B = mc6800Cpu.Pull(), RegisterWordCycles);
        Define(set, DecrementStack, mc6800Cpu => mc6800Cpu.StackPointer--, RegisterWordCycles);
        Define(set, TransferIndexToStack, mc6800Cpu => mc6800Cpu.StackPointer = (ushort)(mc6800Cpu.X - 1), RegisterWordCycles);
        Define(set, PushA, mc6800Cpu => mc6800Cpu.Push(mc6800Cpu.A), RegisterWordCycles);
        Define(set, PushB, mc6800Cpu => mc6800Cpu.Push(mc6800Cpu.B), RegisterWordCycles);
        Define(set, ReturnFromSubroutine, mc6800Cpu => mc6800Cpu.ReturnFromSubroutine(), ReturnFromSubroutineCycles);
        Define(set, ReturnFromInterrupt, mc6800Cpu => mc6800Cpu.ReturnFromInterrupt(), ReturnFromInterruptCycles);
        Define(set, WaitForInterrupt, mc6800Cpu => mc6800Cpu.WaitForInterrupt(), WaitCycles);
        Define(set, SoftwareInterrupt, mc6800Cpu => mc6800Cpu.SoftwareInterrupt(), SoftwareInterruptCycles);
    }

    /// <summary>The one-value operations, each on accumulator A, accumulator B and memory.</summary>
    private static void AddUnaryOperations(Mc6800Instruction[] set)
    {
        AddUnary(set, Negate, Mc6800Alu.Negate, true);
        AddUnary(set, Complement, Mc6800Alu.Complement, true);
        AddUnary(set, ShiftRightLogical, Mc6800Alu.ShiftRightLogical, true);
        AddUnary(set, RotateRight, Mc6800Alu.RotateRight, true);
        AddUnary(set, ShiftRightArithmetic, Mc6800Alu.ShiftRightArithmetic, true);
        AddUnary(set, ShiftLeft, Mc6800Alu.ShiftLeft, true);
        AddUnary(set, RotateLeft, Mc6800Alu.RotateLeft, true);
        AddUnary(set, Decrement, Mc6800Alu.Decrement, true);
        AddUnary(set, Increment, Mc6800Alu.Increment, true);
        AddUnary(set, Test, Mc6800Alu.Test, false);
        AddUnary(set, Clear, (mc6800Cpu, _) => Mc6800Alu.Clear(mc6800Cpu), true);
    }

    /// <summary>Puts one one-value operation in the table in all four of its forms.</summary>
    /// <param name="set">The table.</param>
    /// <param name="operation">The operation's low four bits.</param>
    /// <param name="compute">Works out the new value and sets the flags.</param>
    /// <param name="writesBack">False for TST, which must not write to memory: a write to a chip can change it.</param>
    private static void AddUnary(Mc6800Instruction[] set, byte operation, Func<Mc6800Cpu, byte, byte> compute,
        bool writesBack)
    {
        Define(set, UnaryOnA | operation, mc6800Cpu => mc6800Cpu.A = compute(mc6800Cpu, mc6800Cpu.A));
        Define(set, UnaryOnB | operation, mc6800Cpu => mc6800Cpu.B = compute(mc6800Cpu, mc6800Cpu.B));
        Define(set, UnaryIndexed | operation, mc6800Cpu => ModifyMemory(mc6800Cpu, mc6800Cpu.IndexedAddress(), compute, writesBack),
            UnaryIndexedCycles);
        Define(set, UnaryExtended | operation, mc6800Cpu => ModifyMemory(mc6800Cpu, mc6800Cpu.ExtendedAddress(), compute, writesBack),
            UnaryExtendedCycles);
    }

    /// <summary>Reads a byte, works out its new value, and writes it back when the operation does.</summary>
    private static void ModifyMemory(Mc6800Cpu mc6800Cpu, ushort address, Func<Mc6800Cpu, byte, byte> compute,
        bool writesBack)
    {
        var result = compute(mc6800Cpu, mc6800Cpu.ReadByte(address));
        if (writesBack) mc6800Cpu.WriteByte(address, result);
    }

    /// <summary>The two-value operations between an accumulator and a value.</summary>
    private static void AddAccumulatorOperations(Mc6800Instruction[] set)
    {
        AddForBothAccumulators(set, Subtract, (mc6800Cpu, value, operand) => Mc6800Alu.Subtract(mc6800Cpu, value, operand, false));
        AddForBothAccumulators(set, Compare, CompareWith);
        AddForBothAccumulators(set, SubtractWithCarry,
            (mc6800Cpu, value, operand) => Mc6800Alu.Subtract(mc6800Cpu, value, operand, true));
        AddForBothAccumulators(set, And, (mc6800Cpu, value, operand) => Mc6800Alu.Load(mc6800Cpu, (byte)(value & operand)));
        AddForBothAccumulators(set, BitTest, TestBitsWith);
        AddForBothAccumulators(set, Load, (mc6800Cpu, _, operand) => Mc6800Alu.Load(mc6800Cpu, operand));
        AddForBothAccumulators(set, ExclusiveOr, (mc6800Cpu, value, operand) => Mc6800Alu.Load(mc6800Cpu, (byte)(value ^ operand)));
        AddForBothAccumulators(set, AddWithCarry, (mc6800Cpu, value, operand) => Mc6800Alu.Add(mc6800Cpu, value, operand, true));
        AddForBothAccumulators(set, Or, (mc6800Cpu, value, operand) => Mc6800Alu.Load(mc6800Cpu, (byte)(value | operand)));
        AddForBothAccumulators(set, Add, (mc6800Cpu, value, operand) => Mc6800Alu.Add(mc6800Cpu, value, operand, false));
    }

    /// <summary>
    ///     Compares an accumulator with a value: sets the flags a subtraction would, and leaves the accumulator alone
    ///     (CMP).
    /// </summary>
    private static byte CompareWith(Mc6800Cpu mc6800Cpu, byte value, byte operand)
    {
        Mc6800Alu.Subtract(mc6800Cpu, value, operand, false);
        return value;
    }

    /// <summary>
    ///     Tests which bits an accumulator shares with a value: sets the flags an AND would, and leaves the accumulator
    ///     alone (BIT).
    /// </summary>
    private static byte TestBitsWith(Mc6800Cpu mc6800Cpu, byte value, byte operand)
    {
        Mc6800Alu.Load(mc6800Cpu, (byte)(value & operand));
        return value;
    }

    /// <summary>Puts one two-value operation in the table for both accumulators.</summary>
    private static void AddForBothAccumulators(Mc6800Instruction[] set, byte operation,
        Func<Mc6800Cpu, byte, byte, byte> compute)
    {
        AddForAccumulator(set, AccumulatorAFamily | operation, Mc6800Accumulator.A, compute);
        AddForAccumulator(set, AccumulatorBFamily | operation, Mc6800Accumulator.B, compute);
    }

    /// <summary>Puts one two-value operation in the table for one accumulator, in all four addressing modes.</summary>
    private static void AddForAccumulator(Mc6800Instruction[] set, int immediateOpcode, Mc6800Accumulator accumulator,
        Func<Mc6800Cpu, byte, byte, byte> compute)
    {
        Define(set, immediateOpcode, mc6800Cpu => Apply(mc6800Cpu, accumulator, compute, mc6800Cpu.FetchByte()));
        Define(set, immediateOpcode + DirectMode,
            mc6800Cpu => Apply(mc6800Cpu, accumulator, compute, mc6800Cpu.ReadByte(mc6800Cpu.DirectAddress())), DirectByteCycles);
        Define(set, immediateOpcode + IndexedMode,
            mc6800Cpu => Apply(mc6800Cpu, accumulator, compute, mc6800Cpu.ReadByte(mc6800Cpu.IndexedAddress())), IndexedByteCycles);
        Define(set, immediateOpcode + ExtendedMode,
            mc6800Cpu => Apply(mc6800Cpu, accumulator, compute, mc6800Cpu.ReadByte(mc6800Cpu.ExtendedAddress())), ExtendedByteCycles);
    }

    /// <summary>Works an operation out between an accumulator and a value, and puts the answer in the accumulator.</summary>
    private static void Apply(Mc6800Cpu mc6800Cpu, Mc6800Accumulator accumulator, Func<Mc6800Cpu, byte, byte, byte> compute,
        byte operand)
    {
        var current = mc6800Cpu.GetAccumulator(accumulator);
        mc6800Cpu.SetAccumulator(accumulator, compute(mc6800Cpu, current, operand));
    }

    /// <summary>STA and STB, at a direct, indexed and full address.</summary>
    private static void AddStores(Mc6800Instruction[] set)
    {
        foreach (var accumulator in new[] { Mc6800Accumulator.A, Mc6800Accumulator.B })
        {
            int family = accumulator == Mc6800Accumulator.A ? AccumulatorAFamily : AccumulatorBFamily;
            Define(set, family + DirectMode + Store, mc6800Cpu => StoreAccumulator(mc6800Cpu, accumulator, mc6800Cpu.DirectAddress()),
                DirectStoreCycles);
            Define(set, family + IndexedMode + Store, mc6800Cpu => StoreAccumulator(mc6800Cpu, accumulator, mc6800Cpu.IndexedAddress()),
                IndexedStoreCycles);
            Define(set, family + ExtendedMode + Store, mc6800Cpu => StoreAccumulator(mc6800Cpu, accumulator, mc6800Cpu.ExtendedAddress()),
                ExtendedStoreCycles);
        }
    }

    /// <summary>Writes an accumulator to memory and sets the flags from it.</summary>
    private static void StoreAccumulator(Mc6800Cpu mc6800Cpu, Mc6800Accumulator accumulator, ushort address)
    {
        var value = Mc6800Alu.Load(mc6800Cpu, mc6800Cpu.GetAccumulator(accumulator));
        mc6800Cpu.WriteByte(address, value);
    }

    /// <summary>The 16-bit loads, stores and compare: LDS, STS, LDX, STX and CPX.</summary>
    private static void AddWordOperations(Mc6800Instruction[] set)
    {
        AddWordLoad(set, AccumulatorAFamily | LoadWord, (mc6800Cpu, value) => mc6800Cpu.StackPointer = value);
        AddWordLoad(set, AccumulatorBFamily | LoadWord, (mc6800Cpu, value) => mc6800Cpu.X = value);
        AddWordStore(set, AccumulatorAFamily | StoreWord, mc6800Cpu => mc6800Cpu.StackPointer);
        AddWordStore(set, AccumulatorBFamily | StoreWord, mc6800Cpu => mc6800Cpu.X);
        AddWordLoad(set, AccumulatorAFamily | CompareIndex, Mc6800Alu.CompareIndex, true);
    }

    /// <summary>Puts one 16-bit load (or CPX) in the table in all four addressing modes.</summary>
    /// <param name="set">The table.</param>
    /// <param name="immediateOpcode">The opcode that takes the word from the next two bytes.</param>
    /// <param name="use">What to do with the word.</param>
    /// <param name="setsFlagsItself">True for CPX, whose flags come from the comparison rather than the load.</param>
    private static void AddWordLoad(Mc6800Instruction[] set, int immediateOpcode, Action<Mc6800Cpu, ushort> use,
        bool setsFlagsItself = false)
    {
        Func<Mc6800Cpu, ushort, ushort> flags = setsFlagsItself ? (_, value) => value : Mc6800Alu.LoadWord;
        Define(set, immediateOpcode, mc6800Cpu => use(mc6800Cpu, flags(mc6800Cpu, mc6800Cpu.FetchWord())), ImmediateWordCycles);
        Define(set, immediateOpcode + DirectMode, mc6800Cpu => use(mc6800Cpu, flags(mc6800Cpu, mc6800Cpu.ReadWord(mc6800Cpu.DirectAddress()))),
            DirectWordCycles);
        Define(set, immediateOpcode + IndexedMode, mc6800Cpu => use(mc6800Cpu, flags(mc6800Cpu, mc6800Cpu.ReadWord(mc6800Cpu.IndexedAddress()))),
            IndexedWordCycles);
        Define(set, immediateOpcode + ExtendedMode, mc6800Cpu => use(mc6800Cpu, flags(mc6800Cpu, mc6800Cpu.ReadWord(mc6800Cpu.ExtendedAddress()))),
            ExtendedWordCycles);
    }

    /// <summary>Puts one 16-bit store in the table at a direct, indexed and full address.</summary>
    private static void AddWordStore(Mc6800Instruction[] set, int immediateOpcode, Func<Mc6800Cpu, ushort> read)
    {
        Define(set, immediateOpcode + DirectMode, mc6800Cpu => StoreWordAt(mc6800Cpu, mc6800Cpu.DirectAddress(), read),
            DirectWordStoreCycles);
        Define(set, immediateOpcode + IndexedMode, mc6800Cpu => StoreWordAt(mc6800Cpu, mc6800Cpu.IndexedAddress(), read),
            IndexedWordStoreCycles);
        Define(set, immediateOpcode + ExtendedMode, mc6800Cpu => StoreWordAt(mc6800Cpu, mc6800Cpu.ExtendedAddress(), read),
            ExtendedWordStoreCycles);
    }

    /// <summary>Writes a 16-bit register to memory and sets the flags from it.</summary>
    private static void StoreWordAt(Mc6800Cpu mc6800Cpu, ushort address, Func<Mc6800Cpu, ushort> read)
    {
        var value = Mc6800Alu.LoadWord(mc6800Cpu, read(mc6800Cpu));
        mc6800Cpu.WriteWord(address, value);
    }

    /// <summary>JMP and JSR.</summary>
    private static void AddJumps(Mc6800Instruction[] set)
    {
        Define(set, JumpIndexed, mc6800Cpu => mc6800Cpu.ProgramCounter = mc6800Cpu.IndexedAddress(), JumpIndexedCycles);
        Define(set, JumpExtended, mc6800Cpu => mc6800Cpu.ProgramCounter = mc6800Cpu.ExtendedAddress(), JumpExtendedCycles);
        Define(set, JumpToSubroutineIndexed, mc6800Cpu => mc6800Cpu.JumpToSubroutine(mc6800Cpu.IndexedAddress()), CallCycles);
        Define(set, JumpToSubroutineExtended, mc6800Cpu => mc6800Cpu.JumpToSubroutine(mc6800Cpu.ExtendedAddress()), CallExtendedCycles);
    }
}
