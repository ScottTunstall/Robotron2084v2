using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

[Collection("SoundFacade")]
public sealed class SoundTests : IDisposable
{
    private readonly bool _wasEnabled = Sound.Enabled;

    public void Dispose() => Sound.Enabled = _wasEnabled;

    [Fact]
    public void WithTheSwitchOff_NothingReachesTheSink()
    {
        var sink = new RecordingSink();
        Sound.Initialize(sink);
        Sound.Enabled = false;

        Sound.Play(SoundTables.Laser);
        for (int i = 0; i < 20; i++)
        {
            Sound.Tick();
        }

        Assert.Empty(sink.Sends);
    }

    [Fact]
    public void WithTheSwitchOn_ASoundIsSentToTheSink()
    {
        var sink = new RecordingSink();
        Sound.Initialize(sink);
        Sound.Enabled = true;

        Sound.Play(SoundTables.Laser);
        for (int i = 0; i < 20; i++)
        {
            Sound.Tick();
        }

        Assert.Single(sink.Sends);
    }

    [Fact]
    public void ASoundAskedForWithoutAPlaceIsHeardInTheMiddle()
    {
        var sink = new RecordingSink();
        Sound.Initialize(sink);
        Sound.Enabled = true;

        Sound.Play(SoundTables.Laser);
        Sound.Tick();

        Assert.Equal(0f, sink.Sends[0].Pan);
    }
}
