namespace Robotron2084.Audio.Hardware;

/// <summary>One instruction the 6800 processor knows: what it does and how long it takes.</summary>
/// <param name="Execute">Carries the instruction out, once its opcode byte has been read.</param>
/// <param name="Cycles">How many clock cycles the instruction takes (Motorola MC6800 data sheet).</param>
public readonly record struct Mc6800Instruction(Action<Mc6800Cpu> Execute, int Cycles);
