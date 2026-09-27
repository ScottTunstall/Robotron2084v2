namespace Robotron2084.Audio.Hardware;

/// <summary>The memory and chips a 6800 processor can read from and write to.</summary>
public interface IMc6800Bus
{
    /// <summary>Reads the byte at an address.</summary>
    /// <param name="address">The address to read.</param>
    /// <returns>The byte at that address.</returns>
    byte ReadByte(ushort address);

    /// <summary>Writes a byte to an address.</summary>
    /// <param name="address">The address to write.</param>
    /// <param name="value">The byte to write.</param>
    void WriteByte(ushort address, byte value);
}
