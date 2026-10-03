using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>The transporter's warp-in hum, which keeps sending its sound number for a while after it starts.</summary>
internal sealed class TransporterRequest : ISoundRequest
{
    private readonly TransporterSound _transporter = new();

    /// <summary>The name the user picks it by.</summary>
    public string Name => "Transporter";

    /// <summary>Says what it is.</summary>
    public string Description => "a brain wave's robots beaming in (sends $12 over and over)";

    /// <summary>Clears the board and starts the warp-in.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Start(SoundEngine engine) => _transporter.Start(engine);

    /// <summary>Sends the warp-in sound number when it is due.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Tick(SoundEngine engine) => _transporter.Tick(engine);
}
