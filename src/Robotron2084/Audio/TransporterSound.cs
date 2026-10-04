namespace Robotron2084.Audio;

/// <summary>
/// The transporter's hum: the sound made while a brain wave's robots are beamed onto the playfield. It first clears
/// whatever is playing, then sends the sound over and over: every ROM frame for a while, then
/// every other ROM frame for a while longer. "Transporter" is the arcade's own name for beaming the robots in at the start of a brain wave.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRT2.ASM</c>, routine <c>TRSPRC</c> ("TRANSPORTER SOUND PROCESS"), started by
/// <c>TRNSTV</c> ("START TRANSPORTING").</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$4607</c> (<c>PLAY_BRAIN_WAVE_WARP_IN_SOUNDS</c>).</item>
/// </list>
/// The repeated sends go straight to the board through <c>SDOUT</c>, past the priority check, so they
/// cut across any other sound, as they do in the arcade.
/// </remarks>
public sealed class TransporterSound
{
    /// <summary>The warp-in sound number (<c>LDB #$12</c>).</summary>
    private const int WarpInSoundNumber = 0x12;

    /// <summary>How many sends come one vblank apart (<c>LDA #$48</c>). It is the starting value of <see cref="_closeSendsLeft"/>, which counts down.</summary>
    private const int CloseSends = 0x48;

    /// <summary>How many sends then come two vblanks apart (<c>LDA #$24</c>). It is the starting value of <see cref="_spacedSendsLeft"/>, which counts down.</summary>
    private const int SpacedSends = 0x24;

    /// <summary>The vblanks between the spaced sends (<c>NAP 2</c>). It is the value <see cref="_ticksUntilNextSend"/> is set to after each spaced send, and it then counts down.</summary>
    private const int SpacedGapTicks = 2;

    private int _closeSendsLeft;
    private int _spacedSendsLeft;
    private int _ticksUntilNextSend;

    /// <summary>True while the warp-in is still being sent.</summary>
    public bool IsRunning => _closeSendsLeft > 0 || _spacedSendsLeft > 0;

    /// <summary>Starts the warp-in: clears the sound board, then sends the warp-in sound on the ticks that follow.</summary>
    /// <param name="engine">The sound sequencer, which carries the "clear the system" request.</param>
    public void Start(SoundEngine engine)
    {
        engine.Play(SoundTables.ClearTheSystem, 0f);
        _closeSendsLeft = CloseSends;
        _spacedSendsLeft = SpacedSends;
        _ticksUntilNextSend = 0;
    }

    /// <summary>One port tick (one vblank): sends the warp-in sound when it is due.</summary>
    /// <param name="engine">The sound sequencer, which passes the send straight to the board.</param>
    public void Tick(SoundEngine engine)
    {
        if (!IsRunning || --_ticksUntilNextSend > 0)
        {
            return;
        }

        if (_closeSendsLeft > 0)
        {
            SendClose(engine);
            return;
        }

        SendSpaced(engine);
    }

    /// <summary>One of the sends a vblank apart; after the last, <c>TRSPR1</c> sends its first spaced one at once.</summary>
    private void SendClose(SoundEngine engine)
    {
        engine.SendDirect(WarpInSoundNumber, 0f);
        _closeSendsLeft--;
        _ticksUntilNextSend = 1;
        if (_closeSendsLeft == 0)
        {
            SendSpaced(engine);
        }
    }

    /// <summary>One of the sends two vblanks apart.</summary>
    private void SendSpaced(SoundEngine engine)
    {
        engine.SendDirect(WarpInSoundNumber, 0f);
        _spacedSendsLeft--;
        _ticksUntilNextSend = SpacedGapTicks;
    }
}
