namespace Robotron2084.Input;

/// <summary>Turns the held fire and START buttons of each poll into the presses (rising edges) the attract screens react to.</summary>
public sealed class ButtonEdgeDetector
{
    private PlayerInputState _previous;

    /// <summary>Reads one poll and remembers it for the next.</summary>
    /// <param name="now">This poll's input.</param>
    /// <returns>The buttons that are down now and were not down on the previous poll.</returns>
    public ButtonPresses Advance(PlayerInputState now)
    {
        ButtonPresses presses = new(
            now.FireHeld && !_previous.FireHeld,
            now.StartOnePlayerHeld && !_previous.StartOnePlayerHeld,
            now.StartTwoPlayersHeld && !_previous.StartTwoPlayersHeld);
        _previous = now;
        return presses;
    }
}
