using Robotron2084.Core;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests;

public sealed class CompositePlayerInputSourceTests
{
    [Fact]
    public void Poll_GamepadActive_GamepadWins()
    {
        var keyboard = new FakeInputSource(new PlayerInputState(new IntVector2(1, 0), true));
        var gamepad = new FakeInputSource(new PlayerInputState(new IntVector2(0, -1), false));
        var source = new CompositePlayerInputSource(keyboard, gamepad);

        Assert.Equal(new PlayerInputState(new IntVector2(0, -1), false), source.Poll());
    }

    [Fact]
    public void Poll_GamepadFireOnly_GamepadWins()
    {
        var keyboard = new FakeInputSource(new PlayerInputState(new IntVector2(1, 0), false));
        var gamepad = new FakeInputSource(new PlayerInputState(IntVector2.Zero, true));
        var source = new CompositePlayerInputSource(keyboard, gamepad);

        Assert.Equal(new PlayerInputState(IntVector2.Zero, true), source.Poll());
    }

    [Fact]
    public void Poll_GamepadIdle_KeyboardIsTheFallback()
    {
        var keyboard = new FakeInputSource(new PlayerInputState(new IntVector2(-1, 1), true));
        var gamepad = new FakeInputSource(default);
        var source = new CompositePlayerInputSource(keyboard, gamepad);

        Assert.Equal(new PlayerInputState(new IntVector2(-1, 1), true), source.Poll());
    }
}
