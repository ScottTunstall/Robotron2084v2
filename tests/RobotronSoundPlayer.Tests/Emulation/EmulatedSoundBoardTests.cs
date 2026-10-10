using RobotronSoundPlayer.Emulation;
using Xunit;

namespace RobotronSoundPlayer.Tests.Emulation;

/// <summary>
///     The board's wiring, checked with a tiny hand-written sound ROM: its interrupt routine copies the
///     sound lines straight to the output, so the output shows exactly what the board received.
/// </summary>
public class EmulatedSoundBoardTests
{
    [Fact]
    public void TheBoardStartsSilent_UntilItIsSentASoundNumber()
    {
        var board = new EmulatedSoundBoard(EchoRom());

        RunInstructions(board, 100);

        Assert.Equal(0, board.OutputLevel);
    }

    [Fact]
    public void ASoundNumber_ReachesTheBoardFlipped_WithTheTopTwoLinesHigh()
    {
        var board = new EmulatedSoundBoard(EchoRom());
        RunInstructions(board, 100);

        board.SendSoundNumber(0x25);
        RunInstructions(board, 20);

        // SNDOUT sends the number with its bits flipped (COMB, ANDB #$3F); the wiring holds the top two lines high.
        Assert.Equal(0xDA, board.OutputLevel);
    }

    [Fact]
    public void EachNewSoundNumber_InterruptsTheBoardAgain()
    {
        var board = new EmulatedSoundBoard(EchoRom());
        RunInstructions(board, 100);
        board.SendSoundNumber(0x25);
        RunInstructions(board, 20);

        board.SendSoundNumber(0x11);
        RunInstructions(board, 20);

        Assert.Equal(0xEE, board.OutputLevel);
    }

    [Fact]
    public void ARomOfTheWrongSize_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new EmulatedSoundBoard(new byte[100]));
    }

    private static void RunInstructions(EmulatedSoundBoard board, int count)
    {
        for (var i = 0; i < count; i++) board.Run(1);
    }

    /// <summary>
    ///     A 4K ROM that sets the input-output chip up the way the real sound ROM does, then waits; its
    ///     interrupt routine copies the sound lines (port B) to the output (port A).
    /// </summary>
    private static byte[] EchoRom()
    {
        var rom = new byte[EmulatedSoundBoard.RomLength];
        byte[] start =
        [
            0x8E, 0x00, 0x7F, // LDS #$007F
            0xCE, 0x04, 0x00, // LDX #$0400
            0x6F, 0x01, // CLR 1,X     port A: direction register
            0x86, 0xFF, // LDAA #$FF
            0xA7, 0x00, // STAA 0,X    port A: all outputs
            0x86, 0x04, // LDAA #$04
            0xA7, 0x01, // STAA 1,X    port A: data register
            0x6F, 0x03, // CLR 3,X     port B: direction register
            0x6F, 0x02, // CLR 2,X     port B: all inputs
            0x86, 0x37, // LDAA #$37
            0xA7, 0x03, // STAA 3,X    port B: interrupt on a rising CB1, data register
            0x6F, 0x00, // CLR 0,X     output 0
            0x0E, // CLI
            0x3E, // WAI
            0x20, 0xFD // BRA WAI
        ];
        byte[] interrupt =
        [
            0xB6, 0x04, 0x02, // LDAA $0402  read the sound lines (clears the interrupt)
            0xB7, 0x04, 0x00, // STAA $0400  copy them to the output
            0x3B // RTI
        ];
        start.CopyTo(rom, 0x000);
        interrupt.CopyTo(rom, 0x100);
        rom[0xFF8] = 0xF1; // interrupt vector: $F100
        rom[0xFF9] = 0x00;
        rom[0xFFE] = 0xF0; // reset vector: $F000
        rom[0xFFF] = 0x00;
        return rom;
    }
}
