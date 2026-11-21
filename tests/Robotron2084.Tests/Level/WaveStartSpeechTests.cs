using Robotron2084.Audio;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Robotron2084.Tests.Audio;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>"INTRUDER ALERT INTRUDER ALERT" at the start of the game's first wave with Berzerk speech on, and only then.</summary>
[Collection("SoundFacade")]
public sealed class WaveStartSpeechTests : IDisposable
{
    /// <summary>The longest the tests wait for the speech to finish, in ticks.</summary>
    private const int MaximumTicks = 200;

    private readonly FakeSpeechVoice _voice = new() { TicksPerLine = 1 };

    /// <summary>Gives each test its own speech service, listened to by a fake voice.</summary>
    public WaveStartSpeechTests()
    {
        Speech.Initialise(_voice);
    }

    [Fact]
    public void AClassicWaveSaysNothing()
    {
        var gameSession = CreateGame();

        WaveStartSpeech.Say(gameSession, CreateWave(1, GameStyle.Classic));
        Listen();

        Assert.Empty(_voice.SpokenLineNames);
        Assert.False(gameSession.HasSaidIntruderAlert);
    }

    [Fact]
    public void ANewGameSaysItAgain()
    {
        WaveStartSpeech.Say(CreateGame(), CreateWave(1, GameStyle.Extended));
        Listen();
        _voice.SpokenLineNames.Clear();

        WaveStartSpeech.Say(CreateGame(), CreateWave(1, GameStyle.Extended));
        Listen();

        Assert.Equal(["INTRUDER", "ALERT", "INTRUDER", "ALERT"], _voice.SpokenLineNames);
    }

    [Fact]
    public void AWaveWithoutBerzerkSpeechSaysNothingAndLeavesItForTheNextWaveThatHasIt()
    {
        var gameSession = CreateGame();

        WaveStartSpeech.Say(gameSession, CreateWave(5, GameStyle.Extended)); // a brain wave
        Listen();
        Assert.Empty(_voice.SpokenLineNames);

        WaveStartSpeech.Say(gameSession, CreateWave(6, GameStyle.Extended));
        Listen();
        Assert.Equal(["INTRUDER", "ALERT", "INTRUDER", "ALERT"], _voice.SpokenLineNames);
    }

    /// <summary>Leaves the shared speech service as it was before each test.</summary>
    public void Dispose()
    {
        Speech.ResetForTesting();
    }

    [Fact]
    public void ItIsNotSaidAgainInTheSameGame()
    {
        var gameSession = CreateGame();
        WaveStartSpeech.Say(gameSession, CreateWave(1, GameStyle.Extended));
        Listen();
        _voice.SpokenLineNames.Clear();

        // The same wave after a death, every tick of the wave, and the next Berzerk speech wave.
        WaveStartSpeech.Say(gameSession, CreateWave(1, GameStyle.Extended));
        WaveStartSpeech.Say(gameSession, CreateWave(1, GameStyle.Extended));
        WaveStartSpeech.Say(gameSession, CreateWave(2, GameStyle.Extended));
        Listen();

        Assert.Empty(_voice.SpokenLineNames);
    }

    [Fact]
    public void ItIsSaidAtThePitchTheSamplesWereRecordedAt()
    {
        WaveStartSpeech.Say(CreateGame(), CreateWave(1, GameStyle.Extended));
        Listen();

        Assert.All(_voice.SpokenPitchOctaves, pitchOctaves => Assert.Equal(0f, pitchOctaves));
    }

    [Fact]
    public void TheFirstBerzerkSpeechWaveSaysIntruderAlertTwice()
    {
        var gameSession = CreateGame();

        WaveStartSpeech.Say(gameSession, CreateWave(1, GameStyle.Extended));
        Listen();

        Assert.Equal(["INTRUDER", "ALERT", "INTRUDER", "ALERT"], _voice.SpokenLineNames);
        Assert.True(gameSession.HasSaidIntruderAlert);
    }

    private static GameSession CreateGame()
    {
        return GameSession.CreateNewGame(new FakeInputSource(), 1);
    }

    private static WaveParams CreateWave(int waveNumber, GameStyle gameStyle)
    {
        return new WaveParamsGenerator().Generate(waveNumber, gameStyle);
    }

    private void Listen()
    {
        for (var tick = 0; tick < MaximumTicks && Speech.IsBusy(); tick++)
        {
            Speech.Tick();
            _voice.Tick();
        }
    }
}
