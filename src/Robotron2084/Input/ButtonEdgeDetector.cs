namespace Robotron2084.Input;

/// <summary>
///     Turns the held fire and START buttons of each poll into the presses (rising edges) the attract screens react
///     to.
/// </summary>
public sealed class ButtonEdgeDetector
{
    private PlayerInputState _previousInputState;

    /// <summary>Reads one poll and remembers it for the next.</summary>
    /// <param name="now">This poll's input.</param>
    /// <returns>The buttons that are down now and were not down on the previous poll.</returns>
    public ButtonPresses Advance(PlayerInputState now)
    {
        ButtonPresses presses = new(
            now.FireHeld && !_previousInputState.FireHeld,
            now.StartOnePlayerHeld && !_previousInputState.StartOnePlayerHeld,
            now.StartTwoPlayersHeld && !_previousInputState.StartTwoPlayersHeld);
        _previousInputState = now;
        return presses;
    }
}
