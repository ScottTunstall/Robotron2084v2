using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>Something the player can be asked to play: one of the game's sounds, or one of the board's sound numbers.</summary>
internal interface ISoundRequest
{
    /// <summary>The name the user picks it by.</summary>
    string Name { get; }

    /// <summary>A short line saying what it is.</summary>
    string Description { get; }

    /// <summary>Asks the sequencer for the sound.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    void Start(SoundEngine engine);

    /// <summary>Moves the request on by one port tick, for sounds that keep sending after they start.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    void Tick(SoundEngine engine);
}
