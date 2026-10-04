using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>
/// The settings INI file (notes §131). Like the controls file it must round-trip exactly and fall
/// back to the factory values rather than throw, and — because it can be hand-edited — a value the
/// cabinet itself could not hold must be ignored rather than put the game into an unreachable state.
/// </summary>
public sealed class GameSettingsStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"robotron-settings-{Guid.NewGuid():N}.ini");

    [Fact]
    public void MissingFile_YieldsTheFactorySettings()
    {
        GameSettings loaded = GameSettingsStore.Load(TempFile());

        Assert.Equal(25, loaded.ExtraManEvery);
        Assert.Equal(3, loaded.TurnsPerPlayer);
        Assert.Equal(5, loaded.Difficulty);
    }

    [Fact]
    public void EverythingSurvivesARoundTrip()
    {
        string path = TempFile();
        try
        {
            var settings = new GameSettings { ExtraManEvery = 50, TurnsPerPlayer = 5, Difficulty = 10, AttractModeSound = false };

            GameSettingsStore.Save(path, settings);
            GameSettings loaded = GameSettingsStore.Load(path);

            Assert.Equal(50, loaded.ExtraManEvery);
            Assert.Equal(5, loaded.TurnsPerPlayer);
            Assert.Equal(10, loaded.Difficulty);
            Assert.False(loaded.AttractModeSound);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("extramanevery=17")]  // off the ROM's five-stop list
    [InlineData("extramanevery=1000")]
    public void OutOfRangeValuesAreIgnored(string line)
    {
        GameSettings loaded = GameSettingsStore.Parse(["[game]", line]);

        Assert.Equal(25, loaded.ExtraManEvery);
    }

    [Theory]
    [InlineData("turnsperplayer=0")]
    [InlineData("turnsperplayer=21")]
    [InlineData("difficulty=11")]
    public void ValuesOutsideTheRomsRangeAreIgnored(string line)
    {
        GameSettings loaded = GameSettingsStore.Parse(["[game]", line]);

        Assert.Equal(3, loaded.TurnsPerPlayer);
        Assert.Equal(5, loaded.Difficulty);
    }

    [Fact]
    public void TheAttractSoundFlagIsSavedAndAnythingButZeroOrOneIsIgnored()
    {
        Assert.False(GameSettingsStore.Parse(["[game]", "attractsound=0"]).AttractModeSound);
        Assert.True(GameSettingsStore.Parse(["[game]", "attractsound=1"]).AttractModeSound);
        Assert.False(GameSettingsStore.Parse(["[game]", "attractsound=7"]).AttractModeSound); // ignored: the factory value (off) stays
    }

    [Fact]
    public void TheBugAndBozoSwitchesAreOnUnlessTheFileSaysOff_AndSurviveARoundTrip()
    {
        GameSettings factory = GameSettingsStore.Parse(["[game]"]);
        Assert.True(factory.TankShellBug);
        Assert.True(factory.BrainsChaseMikeyBug);
        Assert.True(factory.BozoModeEnabled);

        var off = new GameSettings { TankShellBug = false, BrainsChaseMikeyBug = false, BozoModeEnabled = false };
        GameSettings loaded = GameSettingsStore.Parse(GameSettingsStore.Write(off).Split(Environment.NewLine));
        Assert.False(loaded.TankShellBug);
        Assert.False(loaded.BrainsChaseMikeyBug);
        Assert.False(loaded.BozoModeEnabled);

        // Anything but 0 or 1 is ignored, so the factory value (on) stays.
        Assert.True(GameSettingsStore.Parse(["[game]", "tankshellbug=7"]).TankShellBug);
    }

    [Fact]
    public void UnknownKeysAndSectionsAreIgnored()
    {
        GameSettings loaded = GameSettingsStore.Parse(
        [
            "; a comment",
            "[somethingelse]",
            "difficulty=1",
            "[game]",
            "# another comment",
            "notasetting=99",
            "difficulty=1",
        ]);

        Assert.Equal(1, loaded.Difficulty);
        Assert.Equal(25, loaded.ExtraManEvery); // untouched
    }
}
