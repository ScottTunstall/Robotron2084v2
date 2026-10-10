using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>One sound number sent straight to the board, once, without any of the game's tables.</summary>
/// <param name="soundNumber">The sound number (the original source's <c>SND#</c>).</param>
internal sealed class SoundNumberRequest(int soundNumber) : ISoundRequest
{
    /// <summary>The sound number, written as the source writes it, such as <c>$0E</c>.</summary>
    public string Name => $"${soundNumber:X2}";

    /// <summary>Says that it is a bare sound number.</summary>
    public string Description => "the board's sound number on its own";

    /// <summary>Sends the sound number to the board.</summary>
    /// <param name="engine">The sequencer that passes the number straight to the board.</param>
    public void Start(SoundEngine engine)
    {
        engine.SendDirect(soundNumber, 0f);
    }

    /// <summary>Does nothing: the number is sent once.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Tick(SoundEngine engine)
    {
    }
}
