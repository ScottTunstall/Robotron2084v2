using RobotronSoundPlayer.Emulation;
using Xunit;

namespace RobotronSoundPlayer.Tests.Emulation;

public class Mc6800CpuTests
{
    private const ushort ProgramStart = 0x1000;

    [Fact]
    public void Reset_StartsAtTheResetVector_WithInterruptsMasked()
    {
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0x01);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);

        mc6800Cpu.Reset();

        Assert.Equal(ProgramStart, mc6800Cpu.ProgramCounter);
        Assert.True(mc6800Cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.InterruptMask));
    }

    [Fact]
    public void AddingPastTheLargestPositiveByte_SetsOverflowNegativeAndHalfCarry()
    {
        // LDAA #$7F / ADDA #$01
        var mc6800Cpu = Run(2, 0x86, 0x7F, 0x8B, 0x01);

        Assert.Equal(0x80, mc6800Cpu.A);
        Assert.Equal(
            Mc6800ConditionCodes.Overflow | Mc6800ConditionCodes.Negative | Mc6800ConditionCodes.HalfCarry,
            mc6800Cpu.ConditionCodes & ~Mc6800ConditionCodes.InterruptMask);
    }

    [Fact]
    public void SubtractingFromZero_Borrows()
    {
        // LDAA #$00 / SUBA #$01
        var mc6800Cpu = Run(2, 0x86, 0x00, 0x80, 0x01);

        Assert.Equal(0xFF, mc6800Cpu.A);
        Assert.True(mc6800Cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Carry));
        Assert.True(mc6800Cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Negative));
    }

    [Fact]
    public void ACountdownLoop_RunsToZero_InTheDataSheetsCycles()
    {
        // LDAB #3 / loop: DECB / BNE loop
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0xC6, 0x03, 0x5A, 0x26, 0xFD);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();

        var cycles = 0;
        for (var step = 0; step < 7; step++) cycles += mc6800Cpu.Step();

        Assert.Equal(0, mc6800Cpu.B);
        Assert.Equal(2 + 3 * (2 + 4), cycles);
    }

    [Fact]
    public void ASubroutineCall_ReturnsToTheInstructionAfterIt()
    {
        // $1000: LDS #$01FF / JSR $1010 / NOP ... $1010: LDAA #$42 / RTS
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0x8E, 0x01, 0xFF, 0xBD, 0x10, 0x10, 0x01);
        flatMemoryBus.Load(0x1010, 0x86, 0x42, 0x39);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();

        StepTimes(mc6800Cpu, 4);

        Assert.Equal(0x42, mc6800Cpu.A);
        Assert.Equal(0x1006, mc6800Cpu.ProgramCounter);
        Assert.Equal(0x01FF, mc6800Cpu.StackPointer);
    }

    [Fact]
    public void AnInterrupt_SavesEveryRegister_AndReturnFromInterruptPutsThemBack()
    {
        // $1000: LDS #$01FF / LDAA #$11 / LDAB #$22 / CLI / NOP;  handler $2000: CLRA / CLRB / RTI
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0x8E, 0x01, 0xFF, 0x86, 0x11, 0xC6, 0x22, 0x0E, 0x01);
        flatMemoryBus.Load(0xFFF8, 0x20, 0x00);
        flatMemoryBus.Load(0x2000, 0x4F, 0x5F, 0x3B);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();
        StepTimes(mc6800Cpu, 4);

        mc6800Cpu.IsInterruptRequested = true;
        mc6800Cpu.Step();
        mc6800Cpu.IsInterruptRequested = false;

        Assert.Equal(0x2000, mc6800Cpu.ProgramCounter);
        Assert.Equal(0x01FF - 7, mc6800Cpu.StackPointer);

        StepTimes(mc6800Cpu, 3);

        Assert.Equal(0x11, mc6800Cpu.A);
        Assert.Equal(0x22, mc6800Cpu.B);
        Assert.Equal(0x1008, mc6800Cpu.ProgramCounter);
        Assert.Equal(0x01FF, mc6800Cpu.StackPointer);
        Assert.False(mc6800Cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.InterruptMask));
    }

    [Fact]
    public void WaitingForAnInterrupt_SavesTheRegistersOnlyOnce()
    {
        // $1000: LDS #$01FF / CLI / WAI;  handler $2000: NOP
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0x8E, 0x01, 0xFF, 0x0E, 0x3E);
        flatMemoryBus.Load(0xFFF8, 0x20, 0x00);
        flatMemoryBus.Load(0x2000, 0x01);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();
        StepTimes(mc6800Cpu, 3);
        var pc = mc6800Cpu.ProgramCounter;

        StepTimes(mc6800Cpu, 5);
        Assert.Equal(pc, mc6800Cpu.ProgramCounter);

        mc6800Cpu.IsInterruptRequested = true;
        mc6800Cpu.Step();

        Assert.Equal(0x2000, mc6800Cpu.ProgramCounter);
        Assert.Equal(0x01FF - 7, mc6800Cpu.StackPointer);
    }

    [Fact]
    public void TestingMemory_ReadsItWithoutWritingIt()
    {
        // TST $0040
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0x7D, 0x00, 0x40);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();

        mc6800Cpu.Step();

        Assert.Empty(flatMemoryBus.Writes);
        Assert.True(mc6800Cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Zero));
    }

    [Fact]
    public void AnIndexedStore_WritesAtTheIndexPlusTheOffset()
    {
        // LDX #$0400 / LDAA #$5A / STAA 2,X
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, 0xCE, 0x04, 0x00, 0x86, 0x5A, 0xA7, 0x02);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();

        StepTimes(mc6800Cpu, 3);

        Assert.Equal(0x5A, flatMemoryBus.ReadByte(0x0402));
    }

    [Fact]
    public void DecimalAdjust_TurnsABinarySumBackIntoTwoDecimalDigits()
    {
        // LDAA #$09 / ADDA #$01 / DAA
        var mc6800Cpu = Run(3, 0x86, 0x09, 0x8B, 0x01, 0x19);

        Assert.Equal(0x10, mc6800Cpu.A);
    }

    [Fact]
    public void ComparingTheIndexRegister_SetsZeroWhenEqual()
    {
        // LDX #$1234 / CPX #$1234
        var mc6800Cpu = Run(2, 0xCE, 0x12, 0x34, 0x8C, 0x12, 0x34);

        Assert.True(mc6800Cpu.ConditionCodes.HasFlag(Mc6800ConditionCodes.Zero));
        Assert.Equal(0x1234, mc6800Cpu.X);
    }

    private static Mc6800Cpu Run(int instructions, params byte[] program)
    {
        var flatMemoryBus = new FlatMemoryBus();
        flatMemoryBus.LoadProgram(ProgramStart, program);
        var mc6800Cpu = new Mc6800Cpu(flatMemoryBus);
        mc6800Cpu.Reset();
        StepTimes(mc6800Cpu, instructions);
        return mc6800Cpu;
    }

    private static void StepTimes(Mc6800Cpu mc6800Cpu, int steps)
    {
        for (var i = 0; i < steps; i++) mc6800Cpu.Step();
    }
}
