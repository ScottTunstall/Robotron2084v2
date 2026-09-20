using Microsoft.Xna.Framework.Input;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Input;

/// <summary>
/// The definitions page's vocabulary (notes §101): every binding must survive a trip
/// through the text the page shows and the INI file stores, because those two are the
/// same words.
/// </summary>
public sealed class InputBindingTests
{
    public static TheoryData<InputBinding, string> Bindings()
    {
        var data = new TheoryData<InputBinding, string>
        {
            { InputBinding.Key(Keys.W), "W" },
            { InputBinding.Key(Keys.Up), "UP" },
            { InputBinding.Key(Keys.Insert), "INSERT" },
            { InputBinding.Key(Keys.NumPad8), "NUMPAD8" },
            { InputBinding.Button(0, Buttons.A), "P1 A" },
            { InputBinding.Button(1, Buttons.RightShoulder), "P2 RIGHTSHOULDER" },
            { InputBinding.Stick(0, rightStick: false, 0, -1), "P1 LEFT STICK UP" },
            { InputBinding.Stick(0, rightStick: true, 0, 1), "P1 RIGHT STICK DOWN" },
            { InputBinding.Stick(1, rightStick: true, -1, -1), "P2 RIGHT STICK UP LEFT" },
            { InputBinding.None, "NONE" },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(Bindings))]
    public void DisplayName_AndTryParse_AreTheSameLanguage(InputBinding binding, string text)
    {
        Assert.Equal(text, binding.DisplayName);
        Assert.True(InputBinding.TryParse(text, out InputBinding parsed));
        Assert.Equal(binding, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("NONE")]
    public void AnEmptyValueIsTheUnboundBinding(string text)
    {
        Assert.True(InputBinding.TryParse(text, out InputBinding parsed));
        Assert.Equal(InputBindingKind.None, parsed.Kind);
    }

    [Fact]
    public void TheAbbreviatedStyleStillParses()
    {
        // A file hand-written before the sticks were spelled out should still load.
        Assert.True(InputBinding.TryParse("P1-LS-UP", out InputBinding stick));
        Assert.Equal(InputBinding.Stick(0, rightStick: false, 0, -1), stick);

        Assert.True(InputBinding.TryParse("p2-rs-dn-lt", out InputBinding diagonal));
        Assert.Equal(InputBinding.Stick(1, rightStick: true, -1, 1), diagonal);
    }

    [Theory]
    [InlineData("NOTAKEY")]
    [InlineData("P3 A")]
    [InlineData("P1 LS SIDEWAYS")]
    [InlineData("P1 NotAButton")]
    public void GarbageIsRejectedRatherThanGuessed(string text) =>
        Assert.False(InputBinding.TryParse(text, out _));

    [Fact]
    public void AStickBindingNeedsADirection() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => InputBinding.Stick(0, rightStick: false, 0, 0));

    [Fact]
    public void StickDirections_AreStoredScreenSpace()
    {
        InputBinding down = InputBinding.Stick(0, rightStick: false, 0, 1);

        Assert.Equal(0, down.DirectionX);
        Assert.Equal(1, down.DirectionY); // Y down, like every other coordinate in the port
    }

    [Fact]
    public void AKeyIsHeld_OnlyWhenItsKeyIsDown()
    {
        var binding = InputBinding.Key(Keys.W);
        var empty = new GamePadState();

        Assert.True(binding.IsHeld(new KeyboardState(Keys.W), empty, empty));
        Assert.False(binding.IsHeld(new KeyboardState(Keys.A), empty, empty));
    }

    [Fact]
    public void AStickBinding_RespondsToThatStickAndNoOther()
    {
        var binding = InputBinding.Stick(0, rightStick: true, 1, 0); // pad 1 right stick, right
        GamePadState pad = TestPads.Pad(leftStick: new Microsoft.Xna.Framework.Vector2(0f, 1f), rightStick: new Microsoft.Xna.Framework.Vector2(1f, 0f));
        var empty = new KeyboardState();

        Assert.True(binding.IsHeld(empty, pad, new GamePadState()));
        Assert.False(binding.IsHeld(empty, new GamePadState(), pad)); // it said pad 1, not pad 2
    }

    [Fact]
    public void TheLeftStickBinding_IgnoresTheRightStick()
    {
        var binding = InputBinding.Stick(0, rightStick: false, 1, 0);
        GamePadState rightStickOnly = TestPads.Pad(rightStick: new Microsoft.Xna.Framework.Vector2(1f, 0f));

        Assert.False(binding.IsHeld(new KeyboardState(), rightStickOnly, new GamePadState()));
    }

    [Fact]
    public void ALineWithBothDevices_SaysOrBetweenThem()
    {
        // The author: "the controls don't clearly show that W OR stick up can be used." Both
        // halves are shown with the word between them; a hyphen could not have stood in for it,
        // because the arcade's small font has no '-' and the two would have run together.
        var both = new ActionBinding(InputBinding.Key(Keys.W), InputBinding.Stick(0, rightStick: false, 0, -1));
        var padOnly = new ActionBinding(InputBinding.None, InputBinding.Stick(0, rightStick: false, 0, -1));

        Assert.Equal("W OR P1 LEFT STICK UP", both.DisplayName);
        Assert.Equal("W", new ActionBinding(InputBinding.Key(Keys.W), InputBinding.None).DisplayName);
        Assert.Equal("P1 LEFT STICK UP", padOnly.DisplayName);
        Assert.Equal("NONE", ActionBinding.None.DisplayName); // not "-": that glyph is not in the small font
    }
}
