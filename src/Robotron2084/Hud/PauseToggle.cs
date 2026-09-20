using Robotron2084.Input;

namespace Robotron2084.Hud;

/// <summary>
/// The port's PAUSE toggle (notes §101) — port-only, the arcade has no pause. Pure and
/// unit-tested because it is an edge detector: the key is held while the player reaches
/// for the door, and a held key must not flap the game in and out of pause.
/// </summary>
public sealed class PauseToggle
{
    private bool _wasHeld;

    /// <summary>True while the game is paused.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>One tick: toggles when <paramref name="held"/> goes from up to down.</summary>
    public void Tick(bool held)
    {
        if (held && !_wasHeld)
        {
            IsPaused = !IsPaused;
        }

        _wasHeld = held;
    }

    /// <summary>Clears the latch when leaving the game (a new game starts unpaused).</summary>
    public void Reset()
    {
        IsPaused = false;
        _wasHeld = true; // swallowing the current state, so the key that left must be released
    }
}
