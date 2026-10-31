namespace Robotron2084.Input;

/// <summary>
///     Turns the held fire and START buttons of each poll into the presses the attract screens react
///     to.
/// </summary>
public sealed class ButtonEdgeDetector
{
    private PlayerInputState _previousInputState;

    /// <summary>Reads one poll and remembers it for the next.</summary>
    /// <param name="currentInputState">This poll's input.</param>
    /// <returns>The buttons that are down now and were not down on the previous poll.</returns>
    public ButtonPresses Advance(PlayerInputState currentInputState)
    {
        ButtonPresses presses = new(
            currentInputState.FireHeld && !_previousInputState.FireHeld,
            currentInputState.StartOnePlayerHeld && !_previousInputState.StartOnePlayerHeld,
            currentInputState.StartTwoPlayersHeld && !_previousInputState.StartTwoPlayersHeld);
        _previousInputState = currentInputState;
        return presses;
    }
}
