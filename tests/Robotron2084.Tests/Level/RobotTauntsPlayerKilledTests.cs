using Robotron2084.Audio.Models;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Robotron2084.Persistence;
using Xunit;
using static Robotron2084.Audio.Models.BerzerkWord;

namespace Robotron2084.Tests.Level;

/// <summary>What the robots say when a player is killed: in EXTENDED only, and in the plural when every player of a two player game is out.</summary>
public sealed class RobotTauntsPlayerKilledTests
{
    private readonly List<(BerzerkVoiceSetting VoiceSetting, SpeechPriority Priority, IReadOnlyList<BerzerkWord> Words)> _said = [];

    private RobotTaunts CreateRobotTaunts(int seed = 1)
    {
        return new RobotTaunts(new Random(seed), () => false, () => false, (voiceSetting, speechPriority, words) => _said.Add((voiceSetting, speechPriority, words)));
    }

    private static GameSession CreateGame(GameStyle gameStyle, GameMode gameMode)
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();
        gameSettings.GameStyle = gameStyle;
        return GameSession.CreateNewGame(gameMode, new FakeInputSource(), new FakeInputSource(), gameSettings: gameSettings);
    }

    [Fact]
    public void InClassic_NothingIsSaid()
    {
        CreateRobotTaunts().SayPlayerKilled(CreateGame(GameStyle.Classic, GameMode.OnePlayer));

        Assert.Empty(_said);
    }

    [Fact]
    public void InExtended_APlayerKilledIsEitherTheHumanoidOrTheIntruder_AndBothComeUp()
    {
        for (var seed = 0; seed < 40; seed++) CreateRobotTaunts(seed).SayPlayerKilled(CreateGame(GameStyle.Extended, GameMode.OnePlayer));

        Assert.All(_said, said => Assert.True(said.Words.SequenceEqual([Got, The, Humanoid]) || said.Words.SequenceEqual([Got, The, Intruder])));
        Assert.Contains(_said, said => said.Words.Contains(Humanoid));
        Assert.Contains(_said, said => said.Words.Contains(Intruder));
    }

    [Fact]
    public void InExtended_APlayerKilledWhileTheOtherStillHasLives_IsOneSentence()
    {
        var gameSession = CreateGame(GameStyle.Extended, GameMode.TwoPlayerAlternate);
        gameSession.PlayerSlots[0].LivesCounter.SetLives(0);

        CreateRobotTaunts().SayPlayerKilled(gameSession);

        Assert.Single(_said);
        Assert.Equal(SpeechPriority.Important, _said[0].Priority);
        Assert.DoesNotContain(_said, said => said.Words.Contains(Humanoids) || said.Words.Contains(Intruders));
    }

    [Fact]
    public void InExtended_WhenEveryPlayerOfATwoPlayerGameIsOut_TheyGetTheHumanoidsThenTheIntruders()
    {
        var gameSession = CreateGame(GameStyle.Extended, GameMode.TwoPlayerAlternate);
        foreach (var playerSlot in gameSession.PlayerSlots) playerSlot.LivesCounter.SetLives(0);

        CreateRobotTaunts().SayPlayerKilled(gameSession);

        Assert.Equal(2, _said.Count);
        Assert.Equal([Got, The, Humanoids], _said[0].Words);
        Assert.Equal([Got, The, Intruders], _said[1].Words);
        Assert.Equal(new BerzerkVoiceSetting(3, 7), _said[0].VoiceSetting);
        Assert.Equal(new BerzerkVoiceSetting(5, 7), _said[1].VoiceSetting);
    }

    [Fact]
    public void InExtended_APlayerOutOfALonePlayerGameIsStillTheSingularSentence()
    {
        var gameSession = CreateGame(GameStyle.Extended, GameMode.OnePlayer);
        gameSession.PlayerSlots[0].LivesCounter.SetLives(0);

        CreateRobotTaunts().SayPlayerKilled(gameSession);

        Assert.Single(_said);
        Assert.Equal(SpeechPriority.Important, _said[0].Priority);
        Assert.DoesNotContain(_said, said => said.Words.Contains(Humanoids) || said.Words.Contains(Intruders));
    }
}
