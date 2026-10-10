using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>
///     The settings INI file (notes §131). Like the controls file it must round-trip exactly, and a
///     missing file yields the factory values. Because it can be hand-edited, a value the cabinet
///     itself could not hold is ignored rather than putting the game into an unreachable state. A file
///     that EXISTS but cannot be opened throws a <see cref="PersistenceException" /> naming it (ERR-1):
///     silently falling back would let the next save overwrite the user's settings.
/// </summary>
public sealed class GameSettingsStoreTests
{
    private static string TempFile()
    {
        return Path.Combine(Path.GetTempPath(), $"robotron-settings-{Guid.NewGuid():N}.ini");
    }

    [Fact]
    public void MissingFile_YieldsTheFactorySettings()
    {
        var loaded = GameSettingsStore.Load(TempFile());

        Assert.Equal(25, loaded.ExtraManEvery);
        Assert.Equal(3, loaded.TurnsPerPlayer);
        Assert.Equal(5, loaded.Difficulty);
    }

    [Fact]
    public void AFileThatCannotBeOpened_ThrowsNamingTheFile()
    {
        var path = TempFile();
        File.WriteAllText(path, "[game]\n");
        try
        {
            // Hold the file with no sharing so the store's read fails the way a locked file does;
            // the handle is released before the finally deletes the file.
            using var lockHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

            var exception = Assert.Throws<PersistenceException>(() => GameSettingsStore.Load(path));

            Assert.Contains(path, exception.Message); // the user must be told WHICH file failed
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EverythingSurvivesARoundTrip()
    {
        var path = TempFile();
        try
        {
            var settings = new GameSettings
                { ExtraManEvery = 50, TurnsPerPlayer = 5, Difficulty = 10, AttractModeSoundEnabled = false };

            GameSettingsStore.Save(path, settings);
            var loaded = GameSettingsStore.Load(path);

            Assert.Equal(50, loaded.ExtraManEvery);
            Assert.Equal(5, loaded.TurnsPerPlayer);
            Assert.Equal(10, loaded.Difficulty);
            Assert.False(loaded.AttractModeSoundEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("extramanevery=17")] // off the ROM's five-stop list
    [InlineData("extramanevery=1000")]
    public void OutOfRangeValuesAreIgnored(string line)
    {
        var loaded = GameSettingsStore.Parse(["[game]", line]);

        Assert.Equal(25, loaded.ExtraManEvery);
    }

    [Theory]
    [InlineData("turnsperplayer=0")]
    [InlineData("turnsperplayer=21")]
    [InlineData("difficulty=11")]
    public void ValuesOutsideTheRomsRangeAreIgnored(string line)
    {
        var loaded = GameSettingsStore.Parse(["[game]", line]);

        Assert.Equal(3, loaded.TurnsPerPlayer);
        Assert.Equal(5, loaded.Difficulty);
    }

    [Fact]
    public void TheAttractSoundFlagIsSavedAndAnythingButZeroOrOneIsIgnored()
    {
        Assert.False(GameSettingsStore.Parse(["[game]", "attractsound=0"]).AttractModeSoundEnabled);
        Assert.True(GameSettingsStore.Parse(["[game]", "attractsound=1"]).AttractModeSoundEnabled);
        Assert.False(GameSettingsStore.Parse(["[game]", "attractsound=7"])
            .AttractModeSoundEnabled); // ignored: the factory value (off) stays
    }

    [Fact]
    public void TheBugAndBozoSwitchesAreOnUnlessTheFileSaysOff_AndSurviveARoundTrip()
    {
        var factory = GameSettingsStore.Parse(["[game]"]);
        Assert.True(factory.TankShellBugEnabled);
        Assert.True(factory.BrainsChaseMikeyBugEnabled);
        Assert.True(factory.BozoModeEnabled);

        var off = new GameSettings
            { TankShellBugEnabled = false, BrainsChaseMikeyBugEnabled = false, BozoModeEnabled = false };
        var loaded = GameSettingsStore.Parse(GameSettingsStore.Write(off).Split(Environment.NewLine));
        Assert.False(loaded.TankShellBugEnabled);
        Assert.False(loaded.BrainsChaseMikeyBugEnabled);
        Assert.False(loaded.BozoModeEnabled);

        // Anything but 0 or 1 is ignored, so the factory value (on) stays.
        Assert.True(GameSettingsStore.Parse(["[game]", "tankshellbug=7"]).TankShellBugEnabled);
    }

    [Fact]
    public void UnknownKeysAndSectionsAreIgnored()
    {
        var loaded = GameSettingsStore.Parse(
        [
            "; a comment",
            "[somethingelse]",
            "difficulty=1",
            "[game]",
            "# another comment",
            "notasetting=99",
            "difficulty=1"
        ]);

        Assert.Equal(1, loaded.Difficulty);
        Assert.Equal(25, loaded.ExtraManEvery); // untouched
    }
}
