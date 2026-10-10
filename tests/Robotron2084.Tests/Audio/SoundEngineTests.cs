using Robotron2084.Audio;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class SoundEngineTests
{
    [Fact]
    public void ASingleLineIsSentOnTheFirstTick_AndFreesTheVoiceWhenItsTimeRunsOut()
    {
        // One line: (1, 16, $25), priority 240 (the shape of ST2SND).
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(Sequence(240, (1, 16, 0x25)), 0f);
        RunTicks(engine, 16);

        // The request primes SNDTMR and SNDREP to 1, so the first tick lands straight on the first line.
        Assert.Equal([0x25], sink.SoundNumbers());
        Assert.Equal([1], sink.SendTicks());
        Assert.Equal(240, engine.CurrentPriority);

        RunTicks(engine, 1);

        Assert.Equal(0, engine.CurrentPriority);
    }

    [Fact]
    public void ARepeatedLineIsResentAfterItsTime_ThenTheNextLineFollows()
    {
        // R5 $30EF player death: (2, 8, $11) then (1, 32, $17).
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.PlayerDeath, 0f);
        RunTicks(engine, 60);

        Assert.Equal([0x11, 0x11, 0x17], sink.SoundNumbers());
        Assert.Equal([1, 9, 17], sink.SendTicks());
        Assert.Equal(0, engine.CurrentPriority);
    }

    [Fact]
    public void TheSamePriorityIsIgnoredWhileASoundPlays()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);
        var sound = Sequence(208, (1, 16, 0x25));

        engine.Play(sound, 0f);
        engine.Tick();
        engine.Play(sound, 0f); // 208 against 208: not strictly higher (BLO SNDLDX)
        RunTicks(engine, 16);

        Assert.Single(sink.Sends);
    }

    [Fact]
    public void ALowerPriorityIsIgnored_AndAHigherOneTakesOver()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(Sequence(200, (1, 8, 0x04)), 0f);
        engine.Tick();
        engine.Play(Sequence(192, (1, 8, 0x06)), 0f);
        engine.Tick();
        engine.Play(Sequence(208, (1, 4, 0x14)), 0f);
        engine.Tick();

        Assert.Equal([0x04, 0x14], sink.SoundNumbers());
    }

    [Fact]
    public void TheVoiceIsFreeOnceTheTableEnds()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(Sequence(255, (1, 2, 0x13)), 0f);
        RunTicks(engine, 10);
        engine.Play(Sequence(192, (1, 10, 0x06)), 0f);
        engine.Tick();

        Assert.Equal([0x13, 0x06], sink.SoundNumbers());
    }

    [Fact]
    public void TicksWithNothingAskedForSendNothing()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        RunTicks(engine, 10);

        Assert.Empty(sink.Sends);
    }

    [Fact]
    public void EverySendOfASoundCarriesThePlaceItWasAskedForFrom()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.PlayerDeath, -0.5f);
        RunTicks(engine, 20);

        Assert.All(sink.Sends, send => Assert.Equal(-0.5f, send.Pan));
    }

    [Fact]
    public void AnIgnoredRequestDoesNotMoveTheSoundThatIsPlaying()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.PlayerDeath, 0.7f);
        engine.Tick();
        engine.Play(SoundTables.TankFire, -0.7f); // 200 is below the death's 238
        RunTicks(engine, 20);

        Assert.All(sink.Sends, send => Assert.Equal(0.7f, send.Pan));
    }

    private static void RunTicks(SoundEngine engine, int ticks)
    {
        for (var i = 0; i < ticks; i++) engine.Tick();
    }

    [Fact]
    public void AHeldVoiceIsNotTakenByALowerSound_UntilTheHoldRunsOut()
    {
        // The wave-end music: the board loops it after the ROM's table has finished asking, so the
        // voice is held for the music's whole play-out (notes §128).
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.WaveEnd, 0f);
        engine.HoldVoice(SoundTuning.WaveEndMusicTicks);
        RunTicks(engine, SoundTuning.WaveEndMusicTicks - 1); // one tick of the hold left

        engine.Play(SoundTables.RobotMove, 0f); // $C0, below the wave end's $E0
        RunTicks(engine, 1);

        Assert.DoesNotContain(0x06, sink.SoundNumbers());

        RunTicks(engine, 1); // the music has run out and the voice is free
        engine.Play(SoundTables.RobotMove, 0f);
        RunTicks(engine, 1);

        Assert.Contains(0x06, sink.SoundNumbers());
    }

    [Fact]
    public void AnImportantSoundStillTakesAHeldVoice()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.WaveEnd, 0f);
        engine.HoldVoice(SoundTuning.WaveEndMusicTicks);
        engine.Play(SoundTables.PlayerDeath, 0f); // $EE, above the wave end's $E0
        RunTicks(engine, 1);

        Assert.Equal(0x11, sink.SoundNumbers()[^1]);
        Assert.Equal(SoundTables.PlayerDeath.Priority, engine.CurrentPriority);
    }

    private static SoundSequence Sequence(int priority,
        params (byte Repetitions, byte LengthVblanks, byte SoundNumber)[] lines)
    {
        return new SoundSequence(priority, 0,
            [.. lines.Select(line => new SoundEntry(line.Repetitions, line.LengthVblanks, line.SoundNumber))]);
    }
}
