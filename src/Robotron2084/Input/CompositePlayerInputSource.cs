using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// Combines gamepad and keyboard (spec: "Supports either Gamepad or
/// Keyboard"): the gamepad wins when actively used (any movement or fire),
/// otherwise the keyboard — the always-available fallback — is polled.
/// </summary>
public sealed class CompositePlayerInputSource : IPlayerInputSource
{
    private readonly IPlayerInputSource _keyboard;
    private readonly IPlayerInputSource _gamepad;

    public CompositePlayerInputSource(IPlayerInputSource keyboard, IPlayerInputSource gamepad)
    {
        _keyboard = keyboard;
        _gamepad = gamepad;
    }

    public PlayerInputState Poll()
    {
        PlayerInputState gamepad = _gamepad.Poll();
        if (gamepad.MoveDirection != IntVector2.Zero || gamepad.FirePressed)
        {
            return gamepad;
        }

        return _keyboard.Poll();
    }
}
