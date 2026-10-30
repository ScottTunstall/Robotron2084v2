namespace Robotron2084.Input;

/// <summary>Somewhere a player's controls can be read from.</summary>
public interface IPlayerInputSource
{
    /// <summary>Reads the controls as they are now.</summary>
    PlayerInputState Poll();
}
