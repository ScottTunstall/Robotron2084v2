using Microsoft.Xna.Framework;
using Robotron2084.Audio;
using Robotron2084.Input;
using Robotron2084.Level.Models;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.States.Models;
using Robotron2084.States;
using Robotron2084.Tests.Audio;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>In EXTENDED, the menu page's Berzerk robot says "NO COINS DETECTED IN POCKET" once when it is left alone for 10 seconds.</summary>
[Collection("SoundFacade")]
public sealed class MainMenuStateIdleSpeechTests : IDisposable
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly FakeSpeechVoice _voice = new() { TicksPerLine = 1 };

    /// <summary>Gives each test its own speech service, listened to by a fake voice.</summary>
    public MainMenuStateIdleSpeechTests()
    {
        Speech.Initialise(_voice);
        Speech.AttractMuted = false;
    }

    /// <summary>Leaves the shared speech service as it was before each test.</summary>
    public void Dispose()
    {
        Speech.ResetForTesting();
    }

    private static MainMenuState CreateMenu(GameStyle gameStyle)
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();
        gameSettings.GameStyle = gameStyle;
        var gameStateParams = new GameStateParams(TestTextures.SharedTextureSet, new HighScoreStore(), ControlSettings.CreateDefaults(),
            new FakeInputSource(), gameSettings, new Random(1));
        return new MainMenuState(gameStateParams);
    }

    /// <summary>Leaves the page alone for some seconds, listening to the speech as it goes.</summary>
    private void LeaveAlone(MainMenuState menu, int seconds)
    {
        GameStateManager gameStateManager = new(menu);
        for (var second = 0; second < seconds; second++)
        {
            menu.Update(new GameTime(TimeSpan.Zero, OneSecond), gameStateManager);
            for (var tick = 0; tick < 100 && Speech.IsBusy(); tick++)
            {
                Speech.Tick();
                _voice.Tick();
            }
        }
    }

    [Fact]
    public void InExtended_AfterTenSecondsAlone_TheRobotSaysNoCoinsDetectedInPocket()
    {
        LeaveAlone(CreateMenu(GameStyle.Extended), 10);

        Assert.Equal(["NO", "COINS", "DETECTED", "IN", "POCKET"], _voice.SpokenLineNames);
    }

    [Fact]
    public void InExtended_ItIsQuietBeforeTenSeconds()
    {
        LeaveAlone(CreateMenu(GameStyle.Extended), 9);

        Assert.Empty(_voice.SpokenLineNames);
    }

    [Fact]
    public void ItSaysItOnce_NotAgainEveryTenSeconds()
    {
        LeaveAlone(CreateMenu(GameStyle.Extended), 29);

        Assert.Equal(5, _voice.SpokenLineNames.Count);
    }

    [Fact]
    public void InClassic_ItIsQuiet()
    {
        LeaveAlone(CreateMenu(GameStyle.Classic), 29);

        Assert.Empty(_voice.SpokenLineNames);
    }

    [Fact]
    public void ItIsSaidInBerzerksOwnVoiceForIt_PitchFiveAtVolumeFour()
    {
        LeaveAlone(CreateMenu(GameStyle.Extended), 10);

        Assert.All(_voice.SpokenPitchOctaves, octaves => Assert.Equal(BerzerkVoice.GetPitchOctaves(5), octaves));
        Assert.All(_voice.SpokenVolumeShares, volumeShare => Assert.Equal(BerzerkVoice.GetVolumeShare(4), volumeShare));
    }
}
