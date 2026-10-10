namespace Robotron2084.Input;

/// <summary>The buttons that went down on this poll (not held from the last one).</summary>
/// <param name="FirePressed">The fire button went down.</param>
/// <param name="StartOnePlayerPressed">START 1 went down.</param>
/// <param name="StartTwoPlayersPressed">START 2 went down.</param>
public readonly record struct ButtonPresses(bool FirePressed, bool StartOnePlayerPressed, bool StartTwoPlayersPressed)
{
    /// <summary>True when any of them went down.</summary>
    public bool AnyPressed() => FirePressed || StartOnePlayerPressed || StartTwoPlayersPressed;
}
