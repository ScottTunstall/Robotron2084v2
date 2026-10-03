using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>Plays a request on a sound board through the game's own sequencer, a port tick at a time.</summary>
internal sealed class BoardSoundSource : ISoundSource
{
    private readonly SoundEngine _engine;
    private readonly ISoundRequest _request;
    private readonly BoardSink _sink;

    /// <summary>Asks a board for the request.</summary>
    /// <param name="board">The sound board.</param>
    /// <param name="request">What to play.</param>
    public BoardSoundSource(ISoundBoard board, ISoundRequest request)
    {
        var renderer = new SoundBoardRenderer(board, PlayerAudio.SampleRate);
        _sink = new BoardSink(board, renderer);
        _engine = new SoundEngine(_sink);
        _request = request;
        _request.Start(_engine);
    }

    /// <summary>Moves the sequencer and the board on by one port tick, in the game's order, and copies out what was heard.</summary>
    /// <param name="samples">The buffer: one port tick of samples.</param>
    public void RenderPortTick(Span<float> samples)
    {
        _engine.Tick();
        _request.Tick(_engine);
        _sink.LastPortTick.CopyTo(samples);
    }
}
