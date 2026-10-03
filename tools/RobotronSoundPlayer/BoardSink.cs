using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>Passes the sequencer's sound numbers to a sound board, and records what the board plays each port tick.</summary>
/// <param name="board">The sound board.</param>
/// <param name="renderer">Turns the board's output into samples.</param>
internal sealed class BoardSink(ISoundBoard board, SoundBoardRenderer renderer) : IAudioSink
{
    /// <summary>What the board played during the last port tick.</summary>
    public float[] LastPortTick { get; } = new float[PlayerAudio.SamplesPerPortTick];

    /// <summary>Sends a sound number to the board. The player is mono, so where it is heard is ignored.</summary>
    /// <param name="soundNumber">The sound number (the original source's <c>SND#</c>), 0 to 63.</param>
    /// <param name="pan">Where the game would place the sound; not used.</param>
    public void SendSoundNumber(int soundNumber, float pan) => board.SendSoundNumber(soundNumber);

    /// <summary>Runs the board for one port tick and keeps what it played.</summary>
    public void Tick() => renderer.Render(LastPortTick);
}
