namespace Robotron2084.Input;

/// <summary>The buttons that went down on this poll (not held from the last one).</summary>
/// <param name="Fire">The fire button went down.</param>
/// <param name="StartOnePlayer">START 1 went down.</param>
/// <param name="StartTwoPlayers">START 2 went down.</param>
public readonly record struct ButtonPresses(bool Fire, bool StartOnePlayer, bool StartTwoPlayers)
{
    /// <summary>True when any of them went down.</summary>
    public bool Any => Fire || StartOnePlayer || StartTwoPlayers;
}
