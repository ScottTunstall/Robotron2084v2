namespace RobotronSoundPlayer.Emulation;

/// <summary>
/// The sound board's input-output chip (a Motorola 6821). It takes in the sound number the main
/// board sends, taps the sound board's processor on the shoulder when one arrives, and passes the
/// processor's output level on to the loudspeaker circuit.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: none. The main board's side of the hand-over is <c>RRS22.ASM</c>, routine
/// <c>SNDOUT</c>, which writes to <c>SOUND</c> (<c>$C80E</c>).</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$D3B6</c> (the main board's side, writing
/// <c>rom_pia_datab</c>).</item>
/// </list>
/// Only what the sound board uses is modelled: each port's data and direction registers, and the
/// CB1 interrupt the main board raises when it sends a sound number. Nothing on this board drives
/// CA1, CA2 or CB2 as inputs, so their interrupt flags never set.
/// </remarks>
public sealed class Pia6821
{
    /// <summary>Register 0: port A's data (or its direction register).</summary>
    private const int PortARegister = 0;

    /// <summary>Register 1: port A's control register.</summary>
    private const int ControlARegister = 1;

    /// <summary>Register 2: port B's data (or its direction register).</summary>
    private const int PortBRegister = 2;

    /// <summary>Control bit 0: let the first interrupt input through to the processor.</summary>
    private const byte InterruptEnableBit = 0x01;

    /// <summary>Control bit 1: the first interrupt input fires on a rise (set) or a fall (clear).</summary>
    private const byte RisingEdgeBit = 0x02;

    /// <summary>Control bit 2: register 0 or 2 reaches the port's data (set) or its direction register (clear).</summary>
    private const byte DataSelectBit = 0x04;

    /// <summary>Control bit 7: the first interrupt input has fired.</summary>
    private const byte FirstInterruptFlag = 0x80;

    /// <summary>Control bits 6 and 7: the interrupt flags, which the processor can read but not write.</summary>
    private const byte InterruptFlags = 0xC0;

    private byte _controlA;
    private byte _controlB;
    private byte _directionA;
    private byte _directionB;
    private byte _inputB;
    private bool _isCb1High;
    private byte _outputA;
    private byte _outputB;

    /// <summary>True while port B's interrupt has fired and is let through to the processor.</summary>
    public bool IsInterruptRequested =>
        (_controlB & FirstInterruptFlag) != 0 && (_controlB & InterruptEnableBit) != 0;

    /// <summary>What port A's pins show: the processor's output on output pins, and high on input pins.</summary>
    public byte PortAPins => (byte)((_outputA & _directionA) | ~_directionA);

    /// <summary>Reads one of the chip's four registers.</summary>
    /// <param name="register">The register, 0 to 3.</param>
    /// <returns>Its value.</returns>
    public byte ReadRegister(int register) => register switch
    {
        PortARegister => ReadPortA(),
        ControlARegister => _controlA,
        PortBRegister => ReadPortB(),
        _ => _controlB,
    };

    /// <summary>Raises or lowers the CB1 input; a change in the chosen direction fires port B's interrupt.</summary>
    /// <param name="isHigh">The input's new level.</param>
    public void SetCb1(bool isHigh)
    {
        bool hasChanged = _isCb1High != isHigh;
        _isCb1High = isHigh;
        bool firesOnRise = (_controlB & RisingEdgeBit) != 0;
        if (hasChanged && isHigh == firesOnRise)
        {
            _controlB |= FirstInterruptFlag;
        }
    }

    /// <summary>Sets what the outside world presents on port B's pins.</summary>
    /// <param name="value">The pins' levels.</param>
    public void SetPortBInput(byte value) => _inputB = value;

    /// <summary>Writes one of the chip's four registers.</summary>
    /// <param name="register">The register, 0 to 3.</param>
    /// <param name="value">The value to write.</param>
    public void WriteRegister(int register, byte value)
    {
        switch (register)
        {
            case PortARegister:
                WritePortA(value);
                return;
            case ControlARegister:
                _controlA = KeepInterruptFlags(_controlA, value);
                return;
            case PortBRegister:
                WritePortB(value);
                return;
            default:
                _controlB = KeepInterruptFlags(_controlB, value);
                return;
        }
    }

    /// <summary>A control register after a write: the written bits, with the interrupt flags left as they were.</summary>
    private static byte KeepInterruptFlags(byte current, byte written) =>
        (byte)((current & InterruptFlags) | (written & ~InterruptFlags));

    /// <summary>Reads port A's data (clearing its interrupt flags) or its direction register.</summary>
    private byte ReadPortA()
    {
        if ((_controlA & DataSelectBit) == 0)
        {
            return _directionA;
        }

        _controlA = (byte)(_controlA & ~InterruptFlags);
        return PortAPins;
    }

    /// <summary>Reads port B's data (clearing its interrupt flags, which lets the interrupt go) or its direction register.</summary>
    private byte ReadPortB()
    {
        if ((_controlB & DataSelectBit) == 0)
        {
            return _directionB;
        }

        _controlB = (byte)(_controlB & ~InterruptFlags);
        return (byte)((_inputB & ~_directionB) | (_outputB & _directionB));
    }

    /// <summary>Writes port A's data or its direction register.</summary>
    private void WritePortA(byte value)
    {
        if ((_controlA & DataSelectBit) == 0)
        {
            _directionA = value;
            return;
        }

        _outputA = value;
    }

    /// <summary>Writes port B's data or its direction register.</summary>
    private void WritePortB(byte value)
    {
        if ((_controlB & DataSelectBit) == 0)
        {
            _directionB = value;
            return;
        }

        _outputB = value;
    }
}
