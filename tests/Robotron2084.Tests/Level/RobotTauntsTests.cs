using Microsoft.Xna.Framework;
using Robotron2084.Audio.Models;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Xunit;
using static Robotron2084.Audio.Models.BerzerkWord;

namespace Robotron2084.Tests.Level;

/// <summary>
///     When the Berzerk robot taunts during a wave and what it says: only on a wave with Berzerk speech on, only while the
///     robots may move, the music has finished and nothing else is being said, when Berzerk's talk timer runs out.
/// </summary>
public sealed class RobotTauntsTests
{
    /// <summary>More ticks than the longest talk timer can run, so a taunt that is allowed is sure to come in them.</summary>
    private const int LongestTalkTimerTicks = (RobotTaunts.LongestTalkTimerSeconds + 1) * TicksPerSecond;

    /// <summary>How many sentences the picking tests pick: enough for every sentence in a list to come up.</summary>
    private const int SentencesPicked = 2000;

    /// <summary>Ticks in a second, which is how often the talk timer is counted down.</summary>
    private const int TicksPerSecond = ArcadeClock.PortTicksPerSecond;

    private static readonly TimeSpan OneTickSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
    private readonly List<IReadOnlyList<BerzerkWord>> _saidSentences = [];
    private readonly List<BerzerkVoiceSetting> _saidVoiceSettings = [];
    private bool _isSpeechBusy;
    private bool _isWaveEndMusicPlaying;

    [Fact]
    public void AChicken_HearsTheOnePlayerChickenSentences()
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame(GameMode.TwoPlayerAlternate);
        gameSession.GetCurrent().RecordWaveCleared(false);

        var sentences = Enumerable.Range(0, SentencesPicked)
            .Select(_ => robotTaunts.GetRandomSentence(gameSession, playField)).ToList();

        Assert.All(sentences, sentence => Assert.Contains(sentence, RobotTauntSentences.OnePlayerChicken));
        Assert.Equal(RobotTauntSentences.OnePlayerChicken.Count, sentences.Distinct().Count());
    }

    [Theory]
    [InlineData(GameMode.OnePlayer, 0)]
    [InlineData(GameMode.OnePlayer, 1)]
    [InlineData(GameMode.TwoPlayerSimultaneous, 0)]
    [InlineData(GameMode.TwoPlayerSimultaneous, 1)]
    public void AChicken_IsCalledAChicken_InAboutHalfTheTaunts(GameMode gameMode, int hulkCount)
    {
        var playField = CreateField(CreateBerzerkWave(hulkCount));
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame(gameMode);
        gameSession.GetCurrent().RecordWaveCleared(false);

        var chickenCalls = Enumerable.Range(0, SentencesPicked)
            .Count(_ => RobotTaunts.IsChickenCall(robotTaunts.GetRandomSentence(gameSession, playField)));

        Assert.InRange(chickenCalls, SentencesPicked * 45 / 100, SentencesPicked * 55 / 100);
    }

    [Fact]
    public void AClassicWave_NeverTaunts()
    {
        var classicWaveParams = new WaveParamsGenerator().Generate(1, GameStyle.Classic);
        var playField = CreateLiveField(classicWaveParams);
        var robotTaunts = CreateRobotTaunts();

        for (var tick = 0; tick < 3 * LongestTalkTimerTicks; tick++) robotTaunts.Tick(CreateGame(), playField);

        Assert.False(robotTaunts.IsAllowedToTaunt(playField));
        Assert.Empty(_saidSentences);
    }

    [Fact]
    public void AfterATaunt_TheNextComesFourEightTwelveOrSixteenSecondsLater()
    {
        var playField = CreateLiveField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();
        List<int> tauntTicks = [];

        for (var tick = 1; tick <= 200 * LongestTalkTimerTicks; tick++)
        {
            robotTaunts.Tick(gameSession, playField);
            if (_saidSentences.Count > tauntTicks.Count) tauntTicks.Add(tick);
        }

        var gapsInSeconds = tauntTicks.Zip(tauntTicks.Skip(1),
            (earlierTick, laterTick) => (laterTick - earlierTick) / TicksPerSecond);
        Assert.Equal([4, 8, 12, 16], gapsInSeconds.Distinct().Order());
    }

    [Fact]
    public void AHulkWithNoFamily_DoesNotAddTheFamilySentences()
    {
        var playField = CreateField(new WaveParams(1, HulkCount: 1, EnableBerzerkSpeak: true));
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();

        var sentences = Enumerable.Range(0, SentencesPicked)
            .Select(_ => robotTaunts.GetRandomSentence(gameSession, playField));

        Assert.All(sentences, sentence => Assert.Contains(sentence, RobotTauntSentences.OnePlayer));
    }

    [Fact]
    public void APlayerWhoIsNotAChicken_HearsOnlyTheOnePlayerSentences()
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();

        var sentences = Enumerable.Range(0, SentencesPicked)
            .Select(_ => robotTaunts.GetRandomSentence(gameSession, playField)).ToList();

        Assert.All(sentences, sentence => Assert.Contains(sentence, RobotTauntSentences.OnePlayer));
        Assert.Equal(RobotTauntSentences.OnePlayer.Count, sentences.Distinct().Count());
    }

    [Fact]
    public void APlayerWhoIsNotAChicken_IsNeverCalledOne_EvenWithTheFamilyAndAHulk()
    {
        var playField = CreateField(CreateBerzerkWave(hulkCount: 1));
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();

        var sentences = Enumerable.Range(0, SentencesPicked).Select(_ => robotTaunts.GetRandomSentence(gameSession, playField));

        Assert.DoesNotContain(sentences, RobotTaunts.IsChickenCall);
    }

    [Fact]
    public void EachTaunt_IsSaidAtARandomPitchAndVolume_AsBerzerkPicksThem()
    {
        var playField = CreateLiveField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();

        for (var tick = 0; tick < 200 * LongestTalkTimerTicks; tick++) robotTaunts.Tick(gameSession, playField);

        Assert.True(_saidVoiceSettings.Count >= 100, $"only {_saidVoiceSettings.Count} taunts");
        var pitches = _saidVoiceSettings.Select(berzerkVoiceSetting => berzerkVoiceSetting.Pitch).Distinct().Order();
        var volumes = _saidVoiceSettings.Select(berzerkVoiceSetting => berzerkVoiceSetting.Volume).Distinct().Order();
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], pitches);
        Assert.Equal([4, 5, 6, 7], volumes);
    }

    [Fact]
    public void InATwoPlayerAlternateGame_OnlyTheCurrentPlayerBeingAChicken_Counts()
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame(GameMode.TwoPlayerAlternate);
        gameSession.PlayerSlots[1].RecordWaveCleared(false);

        var sentences = Enumerable.Range(0, SentencesPicked)
            .Select(_ => robotTaunts.GetRandomSentence(gameSession, playField));

        Assert.All(sentences, sentence => Assert.Contains(sentence, RobotTauntSentences.OnePlayer));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InATwoPlayerSimultaneousGame_EitherPlayerBeingAChicken_Counts(bool isPlayerOneChicken,
        bool isPlayerTwoChicken)
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame(GameMode.TwoPlayerSimultaneous);
        gameSession.PlayerSlots[0].RecordWaveCleared(!isPlayerOneChicken);
        gameSession.PlayerSlots[1].RecordWaveCleared(!isPlayerTwoChicken);
        var expectedSentences = isPlayerOneChicken || isPlayerTwoChicken
            ? RobotTauntSentences.TwoPlayersChicken
            : RobotTauntSentences.TwoPlayers;

        var sentences = Enumerable.Range(0, SentencesPicked)
            .Select(_ => robotTaunts.GetRandomSentence(gameSession, playField)).ToList();

        Assert.All(sentences, sentence => Assert.Contains(sentence, expectedSentences));
        Assert.Equal(expectedSentences.Count, sentences.Distinct().Count());
    }

    [Fact]
    public void InATwoPlayerSimultaneousGame_WithBothPlayersLeft_AChickenIsCalledTheChickens()
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame(GameMode.TwoPlayerSimultaneous);
        gameSession.PlayerSlots[1].RecordWaveCleared(false);

        var chickenCalls = Enumerable.Range(0, SentencesPicked).Select(_ => robotTaunts.GetRandomSentence(gameSession, playField))
            .Where(RobotTaunts.IsChickenCall).ToList();

        Assert.NotEmpty(chickenCalls);
        Assert.All(chickenCalls, sentence => Assert.Contains(Chickens, sentence));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InATwoPlayerSimultaneousGame_WithOnePlayerLeft_TheRobotsTauntThatPlayerAlone(bool isChicken)
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame(GameMode.TwoPlayerSimultaneous);
        gameSession.PlayerSlots[0].RecordWaveCleared(!isChicken);
        gameSession.PlayerSlots[1].LivesCounter.SetLives(0);
        var expectedSentences = isChicken ? RobotTauntSentences.OnePlayerChicken : RobotTauntSentences.OnePlayer;

        var sentences = Enumerable.Range(0, SentencesPicked).Select(_ => robotTaunts.GetRandomSentence(gameSession, playField)).ToList();

        Assert.All(sentences, sentence => Assert.Contains(sentence, expectedSentences));
        Assert.DoesNotContain(sentences, sentence => sentence.Contains(Chickens) || sentence.Contains(Intruders));
    }

    [Fact]
    public void TheFamilyWithAHulk_AddsTheFamilySentences()
    {
        var playField = CreateField(CreateBerzerkWave(1));
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();
        IReadOnlyList<BerzerkWord>[] expectedSentences =
            [.. RobotTauntSentences.OnePlayer, .. RobotTauntSentences.FamilyWithHulksOrBrains];

        var sentences = Enumerable.Range(0, SentencesPicked)
            .Select(_ => robotTaunts.GetRandomSentence(gameSession, playField)).ToList();

        Assert.All(sentences, sentence => Assert.Contains(sentence, expectedSentences));
        Assert.Contains(sentences, sentence => sentence.Contains(Family));
        Assert.Contains(sentences, sentence => sentence.Contains(Intruder));
    }

    [Fact]
    public void TheFirstTaunt_ComesOnTheGamesFirstSecond()
    {
        var playField = CreateLiveField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();

        for (var tick = 1; tick < TicksPerSecond; tick++) robotTaunts.Tick(gameSession, playField);
        Assert.Empty(_saidSentences);

        robotTaunts.Tick(gameSession, playField);
        Assert.Single(_saidSentences);
    }

    [Fact]
    public void TheGetRandomTalkTimerSeconds_IsFourEightTwelveOrSixteen()
    {
        var robotTaunts = CreateRobotTaunts();

        var talkTimers = Enumerable.Range(0, 200).Select(_ => robotTaunts.GetRandomTalkTimerSeconds()).Distinct()
            .Order();

        Assert.Equal([4, 8, 12, 16], talkTimers);
    }

    [Fact]
    public void TheRobots_DoNotStartASentence_WhileAnythingIsBeingSaid()
    {
        var playField = CreateLiveField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        _isSpeechBusy = true;

        TickUntilATauntOrGiveUp(robotTaunts, CreateGame(), playField);

        Assert.Empty(_saidSentences);
    }

    [Fact]
    public void TheRobots_DoNotTaunt_BeforeTheyMayMove()
    {
        var playField = CreateField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();

        Assert.True(playField.RobotsFrozen());
        TickUntilATauntOrGiveUp(robotTaunts, CreateGame(), playField);

        Assert.Empty(_saidSentences);
    }

    [Fact]
    public void TheRobots_DoNotTaunt_WhileTheWaveEndMusicPlays()
    {
        var playField = CreateLiveField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        _isWaveEndMusicPlaying = true;

        TickUntilATauntOrGiveUp(robotTaunts, CreateGame(), playField);

        Assert.Empty(_saidSentences);
    }

    [Fact]
    public void TheTalkTimer_OnlyCountsDown_WhileTheRobotsAreAllowedToTaunt()
    {
        var playField = CreateLiveField(CreateBerzerkWave());
        var robotTaunts = CreateRobotTaunts();
        var gameSession = CreateGame();
        for (var tick = 0; tick < TicksPerSecond; tick++) robotTaunts.Tick(gameSession, playField);
        Assert.Single(_saidSentences);

        _isSpeechBusy = true;
        for (var tick = 0; tick < 3 * LongestTalkTimerTicks; tick++) robotTaunts.Tick(gameSession, playField);
        Assert.Single(_saidSentences);

        _isSpeechBusy = false;
        var quietTicks = 0;
        while (_saidSentences.Count == 1 && quietTicks <= LongestTalkTimerTicks)
        {
            robotTaunts.Tick(gameSession, playField);
            quietTicks++;
        }

        Assert.Equal(2, _saidSentences.Count);
        Assert.InRange(quietTicks, (RobotTaunts.ShortestTalkTimerSeconds - 1) * TicksPerSecond, LongestTalkTimerTicks);
    }

    private static WaveParams CreateBerzerkWave(int hulkCount = 0)
    {
        return new WaveParams(1, MommyCount: 1, HulkCount: hulkCount, EnableBerzerkSpeak: true);
    }

    private static PlayField CreateField(WaveParams waveParams)
    {
        return new PlayFieldBuilder().WithParameters(waveParams).WithSeed(1).Build();
    }

    private static GameSession CreateGame(GameMode gameMode = GameMode.OnePlayer)
    {
        return GameSession.CreateNewGame(gameMode, new FakeInputSource());
    }

    private static PlayField CreateLiveField(WaveParams waveParams)
    {
        var playField = CreateField(waveParams);
        for (var tick = 0; tick < WaveStartTicks.UntilLive(playField); tick++)
            playField.Update(new GameTime(TimeSpan.Zero, OneTickSpan));

        Assert.False(playField.RobotsFrozen());
        return playField;
    }

    private RobotTaunts CreateRobotTaunts(int seed = 1)
    {
        return new RobotTaunts(new Random(seed), () => _isSpeechBusy, () => _isWaveEndMusicPlaying,
            (berzerkVoiceSetting, berzerkWords) =>
            {
                _saidVoiceSettings.Add(berzerkVoiceSetting);
                _saidSentences.Add(berzerkWords);
            });
    }

    private void TickUntilATauntOrGiveUp(RobotTaunts robotTaunts, GameSession gameSession, PlayField playField)
    {
        for (var tick = 0; tick <= LongestTalkTimerTicks && _saidSentences.Count == 0; tick++)
            robotTaunts.Tick(gameSession, playField);
    }
}
