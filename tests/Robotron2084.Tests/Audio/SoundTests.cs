using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

[Collection("SoundFacade")]
public sealed class SoundTests : IDisposable
{
    private readonly bool _wasAttractMuted = Sound.AttractMuted;
    private readonly bool _wasEnabled = Sound.Enabled;

    public void Dispose()
    {
        Sound.Enabled = _wasEnabled;
        Sound.AttractMuted = _wasAttractMuted;
    }

    [Fact]
    public void WithTheSwitchOff_NothingReachesTheSink()
    {
        var recordingSink = new RecordingSink();
        Sound.Initialize(recordingSink);
        Sound.Enabled = false;

        Sound.Play(SoundTables.PlayerLaser);
        for (var i = 0; i < 20; i++) Sound.Tick();

        Assert.Empty(recordingSink.Sends);
    }

    [Fact]
    public void WithTheSwitchOn_ASoundIsSentToTheSink()
    {
        var recordingSink = new RecordingSink();
        Sound.Initialize(recordingSink);
        Sound.Enabled = true;
        Sound.AttractMuted = false;

        Sound.Play(SoundTables.PlayerLaser);
        for (var i = 0; i < 20; i++) Sound.Tick();

        Assert.Single(recordingSink.Sends);
    }

    [Fact]
    public void WhileTheAttractIsMuted_NothingReachesTheSink()
    {
        var recordingSink = new RecordingSink();
        Sound.Initialize(recordingSink);
        Sound.Enabled = true;
        Sound.AttractMuted = true;

        Sound.Play(SoundTables.PlayerLaser);
        for (var i = 0; i < 20; i++) Sound.Tick();

        Assert.Empty(recordingSink.Sends);
    }

    [Fact]
    public void ASoundAskedForWithoutAPlaceIsHeardInTheMiddle()
    {
        var recordingSink = new RecordingSink();
        Sound.Initialize(recordingSink);
        Sound.Enabled = true;

        Sound.Play(SoundTables.PlayerLaser);
        Sound.Tick();

        Assert.Equal(0f, recordingSink.Sends[0].Pan);
    }
}
