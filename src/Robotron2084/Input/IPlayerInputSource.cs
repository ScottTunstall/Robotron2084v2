namespace Robotron2084.Input;

/// <summary>A source of player input (keyboard, gamepad, or a composite of both).</summary>
public interface IPlayerInputSource
{
    /// <summary>Reads the current input state. Cheap; called once per tick per consumer.</summary>
    PlayerInputState Poll();
}
