using RobotronSoundPlayer.Emulation;
using Xunit;

namespace RobotronSoundPlayer.Tests.Emulation;

public class Pia6821Tests
{
    /// <summary>The sound ROM's own port B set-up: CB1 interrupt enabled, rising edge, data register selected.</summary>
    private const byte SoundRomControlB = 0x37;

    [Fact]
    public void ARisingCb1_RequestsAnInterrupt_WhenTheChipIsSetForRisingEdges()
    {
        var pia = new Pia6821();
        pia.WriteRegister(3, SoundRomControlB);

        pia.SetCb1(true);

        Assert.True(pia.IsInterruptRequested);
    }

    [Fact]
    public void AFallingCb1_DoesNothing_WhenTheChipIsSetForRisingEdges()
    {
        var pia = new Pia6821();
        pia.SetCb1(true);
        pia.WriteRegister(3, SoundRomControlB);

        pia.SetCb1(false);

        Assert.False(pia.IsInterruptRequested);
    }

    [Fact]
    public void ReadingPortB_ClearsTheInterrupt_AndReturnsTheInputPins()
    {
        var pia = new Pia6821();
        pia.WriteRegister(3, SoundRomControlB);
        pia.SetPortBInput(0xDA);
        pia.SetCb1(true);

        var value = pia.ReadRegister(2);

        Assert.Equal(0xDA, value);
        Assert.False(pia.IsInterruptRequested);
    }

    [Fact]
    public void PortA_ShowsTheWrittenValueOnOutputPins_AndHighOnInputPins()
    {
        var pia = new Pia6821();
        pia.WriteRegister(0, 0x0F); // direction register: low four pins are outputs
        pia.WriteRegister(1, 0x04); // select the data register
        pia.WriteRegister(0, 0x05);

        Assert.Equal(0xF5, pia.PortAPins);
    }
}
