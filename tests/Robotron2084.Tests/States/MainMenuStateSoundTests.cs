using Robotron2084.Audio;
using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.States;
using Robotron2084.States.Models;
using Robotron2084.Tests.Audio;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The menu page plays the sound ED12 whenever something on it changes.</summary>
[Collection("SoundFacade")]
public sealed class MainMenuStateSoundTests : IDisposable
{
    /// <summary>The sound board's number for ED12.</summary>
    private const int Ed12SoundNumber = 0x0C;

    private readonly RecordingSink _recordingSink = new();
    private readonly bool _wasAttractMuted = Sound.AttractMuted;
    private readonly bool _wasEnabled = Sound.Enabled;

    /// <summary>Listens to the shared sound service with a recording sink.</summary>
    public MainMenuStateSoundTests()
    {
        Sound.AttractMuted = false;
        Sound.Enabled = true;
        Sound.Initialise(_recordingSink);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ChangingTheGameShownIsQuiet(int step)
    {
        var menu = CreateMenu();

        menu.ChangeValue(step);

        Assert.Empty(GetSoundsPlayed());
    }

    /// <summary>Leaves the shared sound service as it was before each test.</summary>
    public void Dispose()
    {
        Sound.Enabled = _wasEnabled;
        Sound.AttractMuted = _wasAttractMuted;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void MovingTheCursorIsQuiet(int step)
    {
        var menu = CreateMenu();

        menu.MoveCursor(step);

        Assert.Empty(GetSoundsPlayed());
    }

    [Fact]
    public void OpeningThePageIsQuiet()
    {
        CreateMenu();

        Assert.Empty(GetSoundsPlayed());
    }

    [Theory]
    [InlineData(MainMenuLine.StartGame)]
    [InlineData(MainMenuLine.Settings)]
    public void SelectingAnOptionPlaysEd12(MainMenuLine line)
    {
        var menu = CreateMenu();

        menu.Choose(line, new GameStateManager(menu));

        Assert.Equal(Ed12SoundNumber, GetSoundsPlayed().First());
    }

    private static MainMenuState CreateMenu()
    {
        var gameStateParams = new GameStateParams(TestTextures.SharedTextureSet, new HighScoreStore(), ControlSettings.CreateDefaults(),
            new FakeInputSource(), GameSettings.CreateFactoryDefaults(), new Random(1));
        return new MainMenuState(gameStateParams);
    }

    private List<int> GetSoundsPlayed()
    {
        for (var tick = 0; tick < 5; tick++) Sound.Tick();

        return _recordingSink.SoundNumbers();
    }
}
