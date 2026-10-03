namespace RobotronSoundPlayer;

/// <summary>Makes the sound for one request, a port tick at a time.</summary>
internal interface ISoundSource
{
    /// <summary>Moves the sound on by one port tick and fills a buffer with what was heard, each sample from -1 to 1.</summary>
    /// <param name="samples">The buffer: one port tick of samples.</param>
    void RenderPortTick(Span<float> samples);
}
