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
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(Sequence(240, (1, 16, 0x25)), 0f);
        RunTicks(soundEngine, 16);

        // The request primes SNDTMR and SNDREP to 1, so the first tick lands straight on the first line.
        Assert.Equal([0x25], recordingSink.SoundNumbers());
        Assert.Equal([1], recordingSink.SendTicks());
        Assert.Equal(240, soundEngine.CurrentPriority);

        RunTicks(soundEngine, 1);

        Assert.Equal(0, soundEngine.CurrentPriority);
    }

    [Fact]
    public void ARepeatedLineIsResentAfterItsTime_ThenTheNextLineFollows()
    {
        // R5 $30EF player death: (2, 8, $11) then (1, 32, $17).
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(SoundTables.PlayerDeath, 0f);
        RunTicks(soundEngine, 60);

        Assert.Equal([0x11, 0x11, 0x17], recordingSink.SoundNumbers());
        Assert.Equal([1, 9, 17], recordingSink.SendTicks());
        Assert.Equal(0, soundEngine.CurrentPriority);
    }

    [Fact]
    public void TheSamePriorityIsIgnoredWhileASoundPlays()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);
        var sound = Sequence(208, (1, 16, 0x25));

        soundEngine.Play(sound, 0f);
        soundEngine.Tick();
        soundEngine.Play(sound, 0f); // 208 against 208: not strictly higher (BLO SNDLDX)
        RunTicks(soundEngine, 16);

        Assert.Single(recordingSink.Sends);
    }

    [Fact]
    public void ALowerPriorityIsIgnored_AndAHigherOneTakesOver()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(Sequence(200, (1, 8, 0x04)), 0f);
        soundEngine.Tick();
        soundEngine.Play(Sequence(192, (1, 8, 0x06)), 0f);
        soundEngine.Tick();
        soundEngine.Play(Sequence(208, (1, 4, 0x14)), 0f);
        soundEngine.Tick();

        Assert.Equal([0x04, 0x14], recordingSink.SoundNumbers());
    }

    [Fact]
    public void TheVoiceIsFreeOnceTheTableEnds()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(Sequence(255, (1, 2, 0x13)), 0f);
        RunTicks(soundEngine, 10);
        soundEngine.Play(Sequence(192, (1, 10, 0x06)), 0f);
        soundEngine.Tick();

        Assert.Equal([0x13, 0x06], recordingSink.SoundNumbers());
    }

    [Fact]
    public void TicksWithNothingAskedForSendNothing()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        RunTicks(soundEngine, 10);

        Assert.Empty(recordingSink.Sends);
    }

    [Fact]
    public void EverySendOfASoundCarriesThePlaceItWasAskedForFrom()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(SoundTables.PlayerDeath, -0.5f);
        RunTicks(soundEngine, 20);

        Assert.All(recordingSink.Sends, send => Assert.Equal(-0.5f, send.Pan));
    }

    [Fact]
    public void AnIgnoredRequestDoesNotMoveTheSoundThatIsPlaying()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(SoundTables.PlayerDeath, 0.7f);
        soundEngine.Tick();
        soundEngine.Play(SoundTables.TankFire, -0.7f); // 200 is below the death's 238
        RunTicks(soundEngine, 20);

        Assert.All(recordingSink.Sends, send => Assert.Equal(0.7f, send.Pan));
    }

    private static void RunTicks(SoundEngine soundEngine, int ticks)
    {
        for (var i = 0; i < ticks; i++) soundEngine.Tick();
    }

    [Fact]
    public void AHeldVoiceIsNotTakenByALowerSound_UntilTheHoldRunsOut()
    {
        // The wave-end music: the board loops it after the ROM's table has finished asking, so the
        // voice is held for the music's whole play-out (notes §128).
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(SoundTables.WaveEnd, 0f);
        soundEngine.HoldVoice(SoundTuning.WaveEndMusicTicks);
        RunTicks(soundEngine, SoundTuning.WaveEndMusicTicks - 1); // one tick of the hold left

        soundEngine.Play(SoundTables.RobotMove, 0f); // $C0, below the wave end's $E0
        RunTicks(soundEngine, 1);

        Assert.DoesNotContain(0x06, recordingSink.SoundNumbers());

        RunTicks(soundEngine, 1); // the music has run out and the voice is free
        soundEngine.Play(SoundTables.RobotMove, 0f);
        RunTicks(soundEngine, 1);

        Assert.Contains(0x06, recordingSink.SoundNumbers());
    }

    [Fact]
    public void AnImportantSoundStillTakesAHeldVoice()
    {
        var recordingSink = new RecordingSink();
        var soundEngine = new SoundEngine(recordingSink);

        soundEngine.Play(SoundTables.WaveEnd, 0f);
        soundEngine.HoldVoice(SoundTuning.WaveEndMusicTicks);
        soundEngine.Play(SoundTables.PlayerDeath, 0f); // $EE, above the wave end's $E0
        RunTicks(soundEngine, 1);

        Assert.Equal(0x11, recordingSink.SoundNumbers()[^1]);
        Assert.Equal(SoundTables.PlayerDeath.Priority, soundEngine.CurrentPriority);
    }

    private static SoundSequence Sequence(int priority,
        params (byte Repetitions, byte LengthVblanks, byte SoundNumber)[] lines)
    {
        return new SoundSequence(priority, 0,
            [.. lines.Select(line => new SoundEntry(line.Repetitions, line.LengthVblanks, line.SoundNumber))]);
    }
}
