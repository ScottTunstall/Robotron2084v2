namespace Robotron2084.Input;

/// <summary>
///     Turns the held skip-wave keys into one skip for each press (port only).
///     Enter is also the menu's select key and, with Alt, the full-screen key, and neither of those may skip a wave.
/// </summary>
public sealed class SkipWaveDetector
{
    // A key that is already down when the wave starts must be let go first, so the Enter that starts a game does not skip wave 1.
    private bool _wasHeld = true;

    /// <summary>Reads one tick and remembers it for the next.</summary>
    /// <param name="isSkipWaveHeld">Whether a skip-wave key is held.</param>
    /// <param name="isAltHeld">
    ///     Whether Alt is held. A press made with Alt down never skips, even if Alt is let go before the key is.
    /// </param>
    /// <returns>True when a skip-wave key has just gone down without Alt.</returns>
    public bool Advance(bool isSkipWaveHeld, bool isAltHeld)
    {
        var isPressed = isSkipWaveHeld && !_wasHeld && !isAltHeld;
        _wasHeld = isSkipWaveHeld;
        return isPressed;
    }
}
