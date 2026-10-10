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
        var (sink, _) = Run(1);

        Assert.Contains(sink.Sends, send => send.SoundNumber == ClearTheSystem && send.Tick == 1);
    }

    [Fact]
    public void ItSendsTheWarpIn108Times_OnTheROMsSchedule()
    {
        var (sink, transporter) = Run(200);

        List<int> ticks = [.. sink.Sends.Where(send => send.SoundNumber == WarpIn).Select(send => send.Tick)];
        Assert.Equal(108, ticks.Count);
        Assert.Equal(Enumerable.Range(1, 72), ticks.Take(72));
        Assert.Equal(72, ticks[72]); // TRSPR1 sends its first spaced one in the same vblank
        Assert.Equal(74, ticks[73]);
        Assert.Equal(142, ticks[^1]);
        Assert.False(transporter.IsRunning());
    }

    [Fact]
    public void Stop_EndsTheSends_SoTheyDoNotCutAcrossTheNextSound()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);
        var transporter = new TransporterSound();
        transporter.Start(engine);
        for (var i = 0; i < 10; i++)
        {
            engine.Tick();
            transporter.Tick(engine);
        }

        var sentBeforeStop = sink.Sends.Count(send => send.SoundNumber == WarpIn);
        transporter.Stop();
        for (var i = 0; i < 200; i++)
        {
            engine.Tick();
            transporter.Tick(engine);
        }

        Assert.False(transporter.IsRunning());
        Assert.Equal(sentBeforeStop, sink.Sends.Count(send => send.SoundNumber == WarpIn));
    }

    private static (RecordingSink Sink, TransporterSound Transporter) Run(int ticks)
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);
        var transporter = new TransporterSound();
        transporter.Start(engine);
        for (var i = 0; i < ticks; i++)
        {
            engine.Tick();
            transporter.Tick(engine);
        }

        return (sink, transporter);
    }
}
