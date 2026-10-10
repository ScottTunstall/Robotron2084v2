using Robotron2084.Audio;

namespace RobotronSoundPlayer.Emulation;

/// <summary>
///     The arcade's sound board, emulated: its processor runs the real sound ROM. The player plays it so the
///     game's own board (<c>Robotron2084.Audio.Synthesis.SoundBoard</c>), which is rebuilt from the ROM's source,
///     can be checked against it by ear and by the fidelity tests.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRS22.ASM</c>, routine <c>SNDOUT</c> (the main board's side: it writes
///             the sound number to <c>SOUND</c>, <c>$C80E</c>).
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>$D3B6</c> (the same routine: "tell sound board to
///             get ready.." then "tell sound board to play sound").
///         </item>
///     </list>
///     The board's program is the sound ROM (<c>video_sound_rom_3_std_767.ic12</c>). The memory map
///     follows MAME's Williams sound board: 128 bytes of RAM at <c>$0000</c>, the input-output chip at
///     <c>$0400</c>, and the ROM at <c>$F000</c>. The ROM's own start-up code sets the chip to interrupt
///     on a rising CB1, which is what the main board's hand-over produces.
/// </remarks>
public sealed class EmulatedSoundBoard : IMc6800Bus, ISoundBoard
{
    /// <summary>The sound ROM's size in bytes.</summary>
    public const int RomLength = 0x1000;

    /// <summary>Where the sound ROM starts in the board's memory.</summary>
    private const int RomStart = 0xF000;

    /// <summary>The address bits the RAM uses: 128 bytes.</summary>
    private const int RamAddressBits = 0x007F;

    /// <summary>Address bits that must be clear to reach the RAM (the RAM repeats every 256 bytes up to <c>$0F7F</c>).</summary>
    private const int RamDecodeBits = 0xF080;

    /// <summary>The input-output chip's address, once its register bits and the ignored top bit are removed.</summary>
    private const int PiaAddress = 0x0400;

    /// <summary>Address bits the input-output chip ignores: its two register bits and the top bit.</summary>
    private const int PiaIgnoredBits = 0x8003;

    /// <summary>The two register-select bits of an input-output chip address.</summary>
    private const int PiaRegisterBits = 0x0003;

    /// <summary>The six sound lines (<c>RRF.ASM</c>: "B0-B5 SOUND").</summary>
    private const int SoundLines = 0x3F;

    /// <summary>The two top lines, which the board's wiring holds high.</summary>
    private const int HeldHighLines = 0xC0;

    /// <summary>Every line high: what the board sees when the main board sends "get ready".</summary>
    private const int AllLinesHigh = 0xFF;

    private readonly Mc6800Cpu _cpu;
    private readonly Pia6821 _pia = new();
    private readonly byte[] _ram = new byte[RamAddressBits + 1];
    private readonly byte[] _rom;

    /// <summary>Builds the board around its sound ROM and switches it on.</summary>
    /// <param name="rom">The sound ROM's bytes.</param>
    /// <exception cref="ArgumentException">The ROM is not <see cref="RomLength" /> bytes long.</exception>
    public EmulatedSoundBoard(byte[] rom)
    {
        if (rom.Length != RomLength)
            throw new ArgumentException($"The sound ROM must be {RomLength} bytes long, not {rom.Length}.",
                nameof(rom));

        _rom = rom;
        _cpu = new Mc6800Cpu(this);
        _cpu.Reset();
    }

    /// <summary>Reads the byte at an address in the board's memory.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The byte there; an address with nothing wired to it reads as zero.</returns>
    public byte ReadByte(ushort address)
    {
        if (IsPiaAddress(address)) return _pia.ReadRegister(address & PiaRegisterBits);

        if (IsRamAddress(address)) return _ram[address & RamAddressBits];

        return address >= RomStart ? _rom[address - RomStart] : (byte)0;
    }

    /// <summary>Writes a byte to an address in the board's memory; the ROM and unwired addresses ignore it.</summary>
    /// <param name="address">The address.</param>
    /// <param name="value">The byte.</param>
    public void WriteByte(ushort address, byte value)
    {
        if (IsPiaAddress(address))
        {
            _pia.WriteRegister(address & PiaRegisterBits, value);
            return;
        }

        if (IsRamAddress(address)) _ram[address & RamAddressBits] = value;
    }

    /// <summary>The level the board is sending to the loudspeaker circuit right now, 0 to 255.</summary>
    public byte OutputLevel => _pia.PortAPins;

    /// <summary>Runs the board's processor for one instruction, however many cycles were asked for.</summary>
    /// <param name="maxCycles">Not used: an instruction cannot be split.</param>
    /// <returns>How many clock cycles passed.</returns>
    public int Run(int maxCycles)
    {
        _cpu.IsInterruptRequested = _pia.IsInterruptRequested;
        return _cpu.Step();
    }

    /// <summary>
    ///     Sends the board a sound number, the way the main board's <c>SNDOUT</c> does: first every line
    ///     high ("get ready"), then the number with its bits flipped. The second write raises CB1, which
    ///     interrupts the board's processor so it starts the new sound.
    /// </summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    public void SendSoundNumber(int soundNumber)
    {
        SetSoundLines(SoundLines);
        SetSoundLines(~soundNumber & SoundLines);
    }

    /// <summary>True when an address reaches the input-output chip.</summary>
    private static bool IsPiaAddress(ushort address)
    {
        return (address & ~PiaIgnoredBits) == PiaAddress;
    }

    /// <summary>True when an address reaches the RAM.</summary>
    private static bool IsRamAddress(ushort address)
    {
        return (address & RamDecodeBits) == 0;
    }

    /// <summary>Puts a value on the sound lines, as the main board's write to <c>SOUND</c> does.</summary>
    /// <param name="value">The six sound lines' levels.</param>
    private void SetSoundLines(int value)
    {
        var lines = (byte)(value | HeldHighLines);
        _pia.SetPortBInput(lines);
        _pia.SetCb1(lines != AllLinesHigh);
    }
}
