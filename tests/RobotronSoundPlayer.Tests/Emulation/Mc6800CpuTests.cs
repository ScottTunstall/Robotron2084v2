using RobotronSoundPlayer.Emulation;
using Xunit;

namespace RobotronSoundPlayer.Tests.Emulation;

public class Mc6800CpuTests
{
    private const ushort ProgramStart = 0x1000;

    [Fact]
    public void Reset_StartsAtTheResetVector_WithInterruptsMasked()
    {
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0x01);
        var cpu = new Mc6800Cpu(bus);

        cpu.Reset();

        Assert.Equal(ProgramStart, cpu.ProgramCounter);
        Assert.True(cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.InterruptMask));
    }

    [Fact]
    public void AddingPastTheLargestPositiveByte_SetsOverflowNegativeAndHalfCarry()
    {
        // LDAA #$7F / ADDA #$01
        Mc6800Cpu cpu = Run(2, 0x86, 0x7F, 0x8B, 0x01);

        Assert.Equal(0x80, cpu.A);
        Assert.Equal(
            Mc6800ConditionCodes.Overflow | Mc6800ConditionCodes.Negative | Mc6800ConditionCodes.HalfCarry,
            cpu.ConditionCodes & ~Mc6800ConditionCodes.InterruptMask);
    }

    [Fact]
    public void SubtractingFromZero_Borrows()
    {
        // LDAA #$00 / SUBA #$01
        Mc6800Cpu cpu = Run(2, 0x86, 0x00, 0x80, 0x01);

        Assert.Equal(0xFF, cpu.A);
        Assert.True(cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Carry));
        Assert.True(cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Negative));
    }

    [Fact]
    public void ACountdownLoop_RunsToZero_InTheDataSheetsCycles()
    {
        // LDAB #3 / loop: DECB / BNE loop
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0xC6, 0x03, 0x5A, 0x26, 0xFD);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();

        int cycles = 0;
        for (int step = 0; step < 7; step++)
        {
            cycles += cpu.Step();
        }

        Assert.Equal(0, cpu.B);
        Assert.Equal(2 + (3 * (2 + 4)), cycles);
    }

    [Fact]
    public void ASubroutineCall_ReturnsToTheInstructionAfterIt()
    {
        // $1000: LDS #$01FF / JSR $1010 / NOP ... $1010: LDAA #$42 / RTS
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0x8E, 0x01, 0xFF, 0xBD, 0x10, 0x10, 0x01);
        bus.Load(0x1010, 0x86, 0x42, 0x39);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();

        StepTimes(cpu, 4);

        Assert.Equal(0x42, cpu.A);
        Assert.Equal(0x1006, cpu.ProgramCounter);
        Assert.Equal(0x01FF, cpu.StackPointer);
    }

    [Fact]
    public void AnInterrupt_SavesEveryRegister_AndReturnFromInterruptPutsThemBack()
    {
        // $1000: LDS #$01FF / LDAA #$11 / LDAB #$22 / CLI / NOP;  handler $2000: CLRA / CLRB / RTI
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0x8E, 0x01, 0xFF, 0x86, 0x11, 0xC6, 0x22, 0x0E, 0x01);
        bus.Load(0xFFF8, 0x20, 0x00);
        bus.Load(0x2000, 0x4F, 0x5F, 0x3B);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();
        StepTimes(cpu, 4);

        cpu.IsInterruptRequested = true;
        cpu.Step();
        cpu.IsInterruptRequested = false;

        Assert.Equal(0x2000, cpu.ProgramCounter);
        Assert.Equal(0x01FF - 7, cpu.StackPointer);

        StepTimes(cpu, 3);

        Assert.Equal(0x11, cpu.A);
        Assert.Equal(0x22, cpu.B);
        Assert.Equal(0x1008, cpu.ProgramCounter);
        Assert.Equal(0x01FF, cpu.StackPointer);
        Assert.False(cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.InterruptMask));
    }

    [Fact]
    public void WaitingForAnInterrupt_SavesTheRegistersOnlyOnce()
    {
        // $1000: LDS #$01FF / CLI / WAI;  handler $2000: NOP
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0x8E, 0x01, 0xFF, 0x0E, 0x3E);
        bus.Load(0xFFF8, 0x20, 0x00);
        bus.Load(0x2000, 0x01);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();
        StepTimes(cpu, 3);
        ushort pc = cpu.ProgramCounter;

        StepTimes(cpu, 5);
        Assert.Equal(pc, cpu.ProgramCounter);

        cpu.IsInterruptRequested = true;
        cpu.Step();

        Assert.Equal(0x2000, cpu.ProgramCounter);
        Assert.Equal(0x01FF - 7, cpu.StackPointer);
    }

    [Fact]
    public void TestingMemory_ReadsItWithoutWritingIt()
    {
        // TST $0040
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0x7D, 0x00, 0x40);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();

        cpu.Step();

        Assert.Empty(bus.Writes);
        Assert.True(cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Zero));
    }

    [Fact]
    public void AnIndexedStore_WritesAtTheIndexPlusTheOffset()
    {
        // LDX #$0400 / LDAA #$5A / STAA 2,X
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, 0xCE, 0x04, 0x00, 0x86, 0x5A, 0xA7, 0x02);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();

        StepTimes(cpu, 3);

        Assert.Equal(0x5A, bus.ReadByte(0x0402));
    }

    [Fact]
    public void DecimalAdjust_TurnsABinarySumBackIntoTwoDecimalDigits()
    {
        // LDAA #$09 / ADDA #$01 / DAA
        Mc6800Cpu cpu = Run(3, 0x86, 0x09, 0x8B, 0x01, 0x19);

        Assert.Equal(0x10, cpu.A);
    }

    [Fact]
    public void ComparingTheIndexRegister_SetsZeroWhenEqual()
    {
        // LDX #$1234 / CPX #$1234
        Mc6800Cpu cpu = Run(2, 0xCE, 0x12, 0x34, 0x8C, 0x12, 0x34);

        Assert.True(cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Zero));
        Assert.Equal(0x1234, cpu.X);
    }

    private static Mc6800Cpu Run(int instructions, params byte[] program)
    {
        var bus = new FlatMemoryBus();
        bus.LoadProgram(ProgramStart, program);
        var cpu = new Mc6800Cpu(bus);
        cpu.Reset();
        StepTimes(cpu, instructions);
        return cpu;
    }

    private static void StepTimes(Mc6800Cpu cpu, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            cpu.Step();
        }
    }
}
