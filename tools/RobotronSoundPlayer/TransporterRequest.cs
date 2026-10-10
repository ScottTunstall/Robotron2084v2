using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>The transporter's hum, which keeps sending its sound number for a while after it starts. "Transporter" is the arcade's own name for beaming the robots in at the start of a brain wave.</summary>
/// <remarks>Original source: <c>RRT2.ASM</c> <c>TRSPRC</c> ("TRANSPORTER SOUND PROCESS"). Disassembly: <c>PLAY_BRAIN_WAVE_WARP_IN_SOUNDS</c> (<c>$4607</c>).</remarks>
internal sealed class TransporterRequest : ISoundRequest
{
    private readonly TransporterSound _transporterSound = new();

    /// <summary>The name the user picks it by.</summary>
    public string Name => "Transporter";

    /// <summary>Says what it is.</summary>
    public string Description => "BRAIN WAVE: a brain wave's robots beaming in (sends $12 over and over)";

    /// <summary>Clears the board and starts the warp-in.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Start(SoundEngine engine) => _transporterSound.Start(engine);

    /// <summary>Sends the warp-in sound number when it is due.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Tick(SoundEngine engine) => _transporterSound.Tick(engine);
}
