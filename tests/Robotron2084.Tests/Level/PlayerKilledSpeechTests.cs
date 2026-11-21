using Robotron2084.Audio;
using Robotron2084.Core.Models;
using Robotron2084.Entities.Extensions;
using Robotron2084.Events;
using Robotron2084.Events.Models;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Robotron2084.Persistence;
using Robotron2084.Tests.Audio;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>The robots say they have got the player the moment the player is killed, not when the next life or wave starts.</summary>
[Collection("SoundFacade")]
public sealed class PlayerKilledSpeechTests : IDisposable
{
    private readonly FakeSpeechVoice _voice = new() { TicksPerLine = 1 };

    /// <summary>Gives each test its own speech service, listened to by a fake voice.</summary>
    public PlayerKilledSpeechTests()
    {
        Speech.Initialise(_voice);
        Speech.AttractMuted = false;
    }

    /// <summary>Leaves the shared speech service as it was before each test.</summary>
    public void Dispose()
    {
        Speech.ResetForTesting();
    }

    [Fact]
    public void AnyOtherEventSaysNothing()
    {
        var speech = new PlayerKilledSpeech(CreateGame(GameStyle.Extended));

        speech.Handle(new GameEvent(GameEventKind.FamilyMemberRescued, IntVector2.Zero));

        Assert.False(Speech.IsBusy());
    }

    [Fact]
    public void TheRobotsSpeakOnTheVeryTickThePlayerIsKilled()
    {
        var gameSession = CreateGame(GameStyle.Extended);
        GameEventHub gameEventHub = new();
        gameEventHub.Subscribe(new PlayerKilledSpeech(gameSession));
        var playField = new PlayFieldBuilder().WithGameEvents(gameEventHub).WithLivesCounter(gameSession.GetCurrent().LivesCounter)
            .WithPlayerInvincible(false).Build();

        playField.KillPlayer();

        Assert.True(playField.Player.IsDying());
        Assert.True(Speech.IsBusy());
    }

    private static GameSession CreateGame(GameStyle gameStyle)
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();
        gameSettings.GameStyle = gameStyle;
        return GameSession.CreateNewGame(GameMode.OnePlayer, new FakeInputSource(), gameSettings: gameSettings);
    }
}
