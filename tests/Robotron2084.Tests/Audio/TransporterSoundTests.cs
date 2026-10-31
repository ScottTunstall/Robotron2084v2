using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>RRT2 <c>TRSPRC</c>: clear the system, then sound $12 every vblank 72 times and every other vblank 36 times.</summary>
public class TransporterSoundTests
{
    private const int WarpIn = 0x12;
    private const int ClearTheSystem = 0x13;

    [Fact]
    public void ItClearsTheSystemFirst()
    {
        var (recordingSink, _) = Run(1);

        Assert.Contains(recordingSink.Sends, send => send.SoundNumber == ClearTheSystem && send.Tick == 1);
    }

    [Fact]
    public void ItSendsTheWarpIn108Times_OnTheROMsSchedule()
    {
        var (recordingSink, transporterSound) = Run(200);

        List<int> ticks = [.. recordingSink.Sends.Where(send => send.SoundNumber == WarpIn).Select(send => send.Tick)];
        Assert.Equal(108, ticks.Count);
        Assert.Equal(Enumerable.Range(1, 72), ticks.Take(72));
        Assert.Equal(72, ticks[72]); // TRSPR1 sends its first spaced one in the same vblank
        Assert.Equal(74, ticks[73]);
        Assert.Equal(142, ticks[^1]);
        Assert.False(transporterSound.IsRunning());
    }

    [Fact]
    public void Stop_EndsTheSends_SoTheyDoNotCutAcrossTheNextSound()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);
        var transporterSound = new TransporterSound();
        transporterSound.Start(soundEngine);
        for (var i = 0; i < 10; i++)
        {
            soundEngine.Tick();
            transporterSound.Tick(soundEngine);
        }

        var sentBeforeStop = recordingSink.Sends.Count(send => send.SoundNumber == WarpIn);
        transporterSound.Stop();
        for (var i = 0; i < 200; i++)
        {
            soundEngine.Tick();
            transporterSound.Tick(soundEngine);
        }

        Assert.False(transporterSound.IsRunning());
        Assert.Equal(sentBeforeStop, recordingSink.Sends.Count(send => send.SoundNumber == WarpIn));
    }

    private static (RecordingSink RecordingSink, TransporterSound TransporterSound) Run(int ticks)
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);
        var transporterSound = new TransporterSound();
        transporterSound.Start(soundEngine);
        for (var i = 0; i < ticks; i++)
        {
            soundEngine.Tick();
            transporterSound.Tick(soundEngine);
        }

        return (recordingSink, transporterSound);
    }
}
