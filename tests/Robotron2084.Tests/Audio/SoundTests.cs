using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

[Collection("SoundFacade")]
public sealed class SoundTests : IDisposable
{
    private readonly bool _wasEnabled = Sound.Enabled;

    public void Dispose() => Sound.Enabled = _wasEnabled;

    [Fact]
    public void BeepingIsOffByDefault_SoNothingReachesTheSink()
    {
        var sink = new RecordingSink();
        Sound.Initialize(sink);
        Sound.Enabled = false;

        Sound.Play(SoundTables.PlayerLaser);
        for (int i = 0; i < 20; i++)
        {
            Sound.Tick();
        }

        Assert.Empty(sink.Calls);
        Assert.False(Sound.Enabled);
    }

    [Fact]
    public void TheFlagTurnsItBackOn_AndTheSequencerStillWorks()
    {
        var sink = new RecordingSink();
        Sound.Initialize(sink);
        Sound.Enabled = true;

        Sound.Play(SoundTables.PlayerLaser);

        for (int i = 0; i < 20; i++)
        {
            Sound.Tick();
        }

        Assert.Single(sink.Calls);
    }
}
