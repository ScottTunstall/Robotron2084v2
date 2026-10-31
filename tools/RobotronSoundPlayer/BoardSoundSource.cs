using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>Plays a request on a sound board through the game's own sequencer, a port tick at a time.</summary>
internal sealed class BoardSoundSource : ISoundSource
{
    private readonly SoundEngine _soundEngine;
    private readonly ISoundRequest _soundRequest;
    private readonly BoardSink _boardSink;

    /// <summary>Asks a board for the request.</summary>
    /// <param name="soundBoard">The sound board.</param>
    /// <param name="soundRequest">What to play.</param>
    public BoardSoundSource(ISoundBoard soundBoard, ISoundRequest soundRequest)
    {
        var soundBoardRenderer = new SoundBoardRenderer(soundBoard, PlayerAudio.SampleRate);
        _boardSink = new BoardSink(soundBoard, soundBoardRenderer);
        _soundEngine = new SoundEngine(_boardSink);
        _soundRequest = soundRequest;
        _soundRequest.Start(_soundEngine);
    }

    /// <summary>Moves the sequencer and the board on by one port tick, in the game's order, and copies out what was heard.</summary>
    /// <param name="samples">The buffer: one port tick of samples.</param>
    public void RenderPortTick(Span<float> samples)
    {
        _soundEngine.Tick();
        _soundRequest.Tick(_soundEngine);
        _boardSink.LastPortTick.CopyTo(samples);
    }
}
