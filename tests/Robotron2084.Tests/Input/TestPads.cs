using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Robotron2084.Tests.Input;

/// <summary>
/// Gamepad snapshots for the input tests (notes §101). MonoGame's <c>GamePadState</c>
/// cannot be built from the XNA <c>GamePadThumbSticks</c>/<c>GamePadButtons</c>/
/// <c>GamePadTriggers</c> trio — it takes raw vectors — so the awkward construction
/// lives here once instead of in every test.
/// </summary>
internal static class TestPads
{
    /// <summary>A pad with the given sticks, buttons and right trigger; nothing else.</summary>
    public static GamePadState Pad(
        Vector2 leftStick = default,
        Vector2 rightStick = default,
        Buttons button = Buttons.None,
        float rightTrigger = 0f)
    {
        var buttons = new List<Buttons>();
        if (button != Buttons.None)
        {
            buttons.Add(button);
        }

        return new GamePadState(leftStick, rightStick, 0f, rightTrigger, [.. buttons]);
    }

    /// <summary>A pad with exactly one button pressed.</summary>
    public static GamePadState WithButton(Buttons button) => Pad(button: button);
}
