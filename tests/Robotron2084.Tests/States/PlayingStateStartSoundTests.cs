using Robotron2084.Audio;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.States;
using Robotron2084.Tests.Audio;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The arcade's start sound when a game begins: in a CLASSIC game only, so it does not play over EXTENDED's speech.</summary>
[Collection("SoundFacade")]
public sealed class PlayingStateStartSoundTests : IDisposable
{
    /// <summary>The one-player start sound's number (the original source's <c>SND#</c>).</summary>
    private const int StartOnePlayerSoundNumber = 0x28;

    /// <summary>Ticks to let any start sound reach the board.</summary>
    private const int TicksToListen = 30;

    private readonly RecordingSink _recordingSink = new();
    private readonly bool _wasAttractMuted = Sound.AttractMuted;
    private readonly bool _wasEnabled = Sound.Enabled;

    /// <summary>Listens to the shared sound service with a recording sink.</summary>
    public PlayingStateStartSoundTests()
    {
        Sound.AttractMuted = false;
        Sound.Enabled = true;
        Sound.Initialise(_recordingSink);
    }

    /// <summary>Leaves the shared sound service as it was before each test.</summary>
    public void Dispose()
    {
        Sound.Enabled = _wasEnabled;
        Sound.AttractMuted = _wasAttractMuted;
    }

    private List<int> StartGameAndListen(GameStyle gameStyle)
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();
        gameSettings.GameStyle = gameStyle;
        _ = PlayingState.CreateNewGame(ControlSettings.CreateDefaults(), gameSettings, GameMode.OnePlayer,
            TestTextures.SharedTextureSet, null!, new Random(1));
        for (var tick = 0; tick < TicksToListen; tick++) Sound.Tick();

        return _recordingSink.SoundNumbers();
    }

    [Fact]
    public void AClassicGameStartsWithTheArcadesStartSound()
    {
        Assert.Contains(StartOnePlayerSoundNumber, StartGameAndListen(GameStyle.Classic));
    }

    [Fact]
    public void AnExtendedGameStartsWithoutIt()
    {
        Assert.DoesNotContain(StartOnePlayerSoundNumber, StartGameAndListen(GameStyle.Extended));
    }
}
