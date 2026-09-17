using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>
/// The <see cref="Sound"/> facade's master switch (notes §68): the port's only sink
/// is the STUB square-wave beeper, so the beeping is OFF by default and a flag (or
/// the <c>ROBOTRON2084_SOUND=1</c> environment variable) turns it back on.
/// </summary>
/// <remarks>
/// DisableParallelization: these tests flip a PROCESS-WIDE switch, so they must not
/// overlap another collection that plays a sound.
/// </remarks>
[CollectionDefinition("SoundFacade", DisableParallelization = true)]
public sealed class SoundFacadeCollection
{
}

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
