namespace Robotron2084.Level;

/// <summary>
/// How a game was started (notes §101). The arcade has one two-player game — both
/// players at once, on their own sticks — so <see cref="TwoPlayerSimultaneous"/> is the
/// faithful one; the author also asked for an ALTERNATE (take-turns) two-player game,
/// which is what the port has been doing with its turn logic all along.
///
/// <see cref="TwoPlayerSimultaneous"/> is wired as far as mode selection and the two
/// inputs; the field itself still runs one player at a time (the author asked for the
/// mode now and the simultaneous play later).
/// </summary>
public enum GameMode
{
    /// <summary>One player, one stick pair.</summary>
    OnePlayer,

    /// <summary>Two players taking turns — the port's existing turn logic (RRG23 PLEND).</summary>
    TwoPlayerAlternate,

    /// <summary>The arcade's two-player game: both players in the field at once. Not built yet.</summary>
    TwoPlayerSimultaneous,
}
