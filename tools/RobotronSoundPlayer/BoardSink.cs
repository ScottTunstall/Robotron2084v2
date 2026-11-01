using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>Passes the sequencer's sound numbers to a sound board, and records what the board plays each port tick.</summary>
/// <param name="soundBoard">The sound board.</param>
/// <param name="soundBoardRenderer">Turns the board's output into samples.</param>
internal sealed class BoardSink(ISoundBoard soundBoard, SoundBoardRenderer soundBoardRenderer) : IAudioSink
{
    /// <summary>What the board played during the last port tick.</summary>
    public float[] LastPortTick { get; } = new float[PlayerAudio.SamplesPerPortTick];

    /// <summary>Always false: the player does not ask whether a sound is still playing.</summary>
    public bool IsPlaying() => false;

    /// <summary>Sends a sound number to the board. The player is mono, so where it is heard is ignored.</summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    /// <param name="pan">Where the game would place the sound; not used.</param>
    public void SendSoundNumber(int soundNumber, float pan)
    {
        soundBoard.SendSoundNumber(soundNumber);
    }

    /// <summary>Runs the board for one port tick and keeps what it played.</summary>
    public void Tick()
    {
        soundBoardRenderer.Render(LastPortTick);
    }
}
