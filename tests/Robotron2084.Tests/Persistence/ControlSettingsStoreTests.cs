using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>
/// The controls INI file (notes §101). The author asked for INI rather than JSON so it
/// can be read and hand-edited, which makes two things load-bearing: the file must
/// round-trip exactly, and anything unreadable in it must fall back to the factory
/// scheme rather than leave the player with no controls at all.
/// </summary>
public sealed class ControlSettingsStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"robotron-controls-{Guid.NewGuid():N}.ini");

    [Fact]
    public void MissingFile_YieldsTheFactoryScheme()
    {
        ControlSettings loaded = ControlSettingsStore.Load(TempFile());

        Assert.Equal("W", loaded[0][InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("P", loaded.Pause.DisplayName);
    }

    [Fact]
    public void EverythingSurvivesARoundTrip()
    {
        string path = TempFile();
        try
        {
            ControlSettings settings = ControlSettings.Defaults();
            settings[0][InputAction.MoveUp] = settings[0][InputAction.MoveUp].With(InputBinding.Key(Keys.Z));
            settings[1][InputAction.ShootRight] = settings[1][InputAction.ShootRight].With(InputBinding.Button(1, Buttons.RightShoulder));
            settings[1][InputAction.MoveLeft] = settings[1][InputAction.MoveLeft].With(InputBinding.Stick(1, rightStick: false, -1, -1));
            settings.Pause = InputBinding.Key(Keys.Escape);

            ControlSettingsStore.Save(path, settings);
            ControlSettings loaded = ControlSettingsStore.Load(path);

            Assert.Equal("Z", loaded[0][InputAction.MoveUp].Key.DisplayName);
            Assert.Equal("P1 LEFT STICK UP", loaded[0][InputAction.MoveUp].Pad.DisplayName);
            Assert.Equal("P2 RIGHTSHOULDER", loaded[1][InputAction.ShootRight].Pad.DisplayName);
            Assert.Equal("NUMPAD6", loaded[1][InputAction.ShootRight].Key.DisplayName);
            Assert.Equal("P2 LEFT STICK UP LT", loaded[1][InputAction.MoveLeft].Pad.DisplayName);
            Assert.Equal("ESCAPE", loaded.Pause.DisplayName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheFileIsReadableIni_WithThePagesOwnVocabulary()
    {
        string text = ControlSettingsStore.Write(ControlSettings.Defaults());

        Assert.Contains("[player1]", text);
        Assert.Contains("[player2]", text);
        Assert.Contains("[pause]", text);
        Assert.Contains("moveup.key=W", text);
        Assert.Contains("moveup.pad=P1 LEFT STICK UP", text);
        Assert.Contains("shootright.pad=P2 RIGHT STICK RT", text);
        Assert.Contains("input=P", text);
        // PAUSE is ONE value, not a key/pad pair (the author asked for a single pause key).
        Assert.DoesNotContain("pause.key", text);
        Assert.DoesNotContain("pause.pad", text);
        // Nothing in a VALUE may need a character the arcade's small font has not got
        // (no lowercase, no colon, no hyphen) — notes §101.
        string values = string.Join(
            "\n",
            text.Split('\n').Where(line => !line.TrimStart().StartsWith(';')));
        Assert.DoesNotContain(":", values);
        Assert.DoesNotContain("-", values);
    }

    [Fact]
    public void ClearingOneSlotByHand_LeavesTheOtherAlone()
    {
        // The trap: "key=-" must clear the KEYBOARD slot only, not the whole line.
        ControlSettings parsed = ControlSettingsStore.Parse(
        [
            "[player1]",
            "moveup.key=-",
            "moveup.pad=",
        ]);

        Assert.Equal(InputBindingKind.None, parsed[0][InputAction.MoveUp].Key.Kind);
        Assert.Equal(InputBindingKind.None, parsed[0][InputAction.MoveUp].Pad.Kind);
        Assert.Equal("S", parsed[0][InputAction.MoveDown].Key.DisplayName); // the rest is untouched
    }

    [Fact]
    public void TheOlderPauseNames_StillSetTheOneValue()
    {
        // The pause line used to be a key/pad pair; both old names now set the single value.
        ControlSettings byKey = ControlSettingsStore.Parse(["[pause]", "key=Q"]);
        Assert.Equal("Q", byKey.Pause.DisplayName);

        ControlSettings byPad = ControlSettingsStore.Parse(["[pause]", "pad=P1 A"]);
        Assert.Equal("P1 A", byPad.Pause.DisplayName);
    }

    [Fact]
    public void PartialFiles_KeepTheFactoryValueForEverythingElse()
    {
        ControlSettings parsed = ControlSettingsStore.Parse(["[player1]", "moveup.key=Z"]);

        Assert.Equal("Z", parsed[0][InputAction.MoveUp].Key.DisplayName);
        Assert.Equal("S", parsed[0][InputAction.MoveDown].Key.DisplayName);
        Assert.Equal("UP", parsed[1][InputAction.MoveUp].Key.DisplayName);
    }

    [Fact]
    public void GarbageIsIgnored_NotGuessed()
    {
        ControlSettings parsed = ControlSettingsStore.Parse(
        [
            "; a comment",
            "# another",
            "[nonsense]",
            "moveup.key=Z",
            "[player1]",
            "notanaction.key=Z",
            "moveup.key=NotAKey",
            "moveup.pad=[player1]",
        ]);

        Assert.Equal("W", parsed[0][InputAction.MoveUp].Key.DisplayName); // the bad value left the default
        Assert.Equal("P1 LEFT STICK UP", parsed[0][InputAction.MoveUp].Pad.DisplayName);
    }

    [Fact]
    public void ACorruptFile_FallsBackToTheFactoryScheme()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, "this is not an ini file at all \0\u0001");

            ControlSettings loaded = ControlSettingsStore.Load(path);

            Assert.Equal("W", loaded[0][InputAction.MoveUp].Key.DisplayName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveCreatesTheDirectory_AndWritesNoBom()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"robotron-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "controls.ini");
        try
        {
            ControlSettingsStore.Save(path, ControlSettings.Defaults());

            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 0);
            Assert.NotEqual(0xEF, bytes[0]); // no BOM: the file should open cleanly in Notepad
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReadPlayer_TurnsTheBindingsIntoAState()
    {
        ControlSettings settings = ControlSettings.Defaults();
        var keys = new KeyboardState(Keys.W, Keys.L); // player 1: move up, shoot right

        PlayerInputState state = settings.ReadPlayer(0, keys, new GamePadState(), new GamePadState());

        Assert.Equal(new IntVector2(0, -1), state.MoveDirection);
        Assert.Equal(new IntVector2(1, 0), state.AimDirection);
        Assert.True(state.FirePressed);
        Assert.False(state.SkipLevelPressed);
    }

    [Fact]
    public void APlayerTwoBinding_DrivesPlayerTwo()
    {
        ControlSettings settings = ControlSettings.Defaults();
        var keys = new KeyboardState(Keys.NumPad8); // player 2's default shoot-up

        PlayerInputState state = settings.ReadPlayer(1, keys, new GamePadState(), new GamePadState());

        Assert.Equal(new IntVector2(0, -1), state.AimDirection);
        Assert.True(state.FirePressed);
    }

    [Fact]
    public void SpaceAndInsert_StayAsPortAliases()
    {
        ControlSettings settings = ControlSettings.Defaults();

        PlayerInputState state = settings.ReadPlayer(0, new KeyboardState(Keys.Space, Keys.Insert), new GamePadState(), new GamePadState());

        Assert.True(state.FirePressed);      // Space has always fired
        Assert.True(state.SkipLevelPressed); // Insert is the skip-level key since P became PAUSE
    }

    [Fact]
    public void TheStartButtons_AreStillTheArcanes()
    {
        ControlSettings settings = ControlSettings.Defaults();

        PlayerInputState state = settings.ReadPlayer(0, new KeyboardState(Keys.D2), new GamePadState(), new GamePadState());

        Assert.True(state.StartTwoPlayersPressed);
        Assert.False(state.StartOnePlayerPressed);
    }
}
