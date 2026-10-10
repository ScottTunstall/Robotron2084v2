using RobotronSoundPlayer.Emulation;

namespace RobotronSoundPlayer.Tests.Emulation;

/// <summary>A plain 64K of memory for processor tests, which records every write.</summary>
internal sealed class FlatMemoryBus : IMc6800Bus
{
    private readonly byte[] _memory = new byte[ushort.MaxValue + 1];

    /// <summary>Every address written, in order.</summary>
    public List<ushort> Writes { get; } = [];

    public byte ReadByte(ushort address)
    {
        return _memory[address];
    }

    public void WriteByte(ushort address, byte value)
    {
        Writes.Add(address);
        _memory[address] = value;
    }

    /// <summary>Loads bytes into memory without recording them as writes.</summary>
    public void Load(ushort address, params byte[] bytes)
    {
        bytes.CopyTo(_memory, address);
    }

    /// <summary>Points the reset vector at a program and loads it there.</summary>
    public void LoadProgram(ushort address, params byte[] program)
    {
        Load(0xFFFE, (byte)(address >> 8), (byte)address);
        Load(address, program);
    }
}
