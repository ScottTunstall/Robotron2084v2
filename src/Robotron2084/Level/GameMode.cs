namespace Robotron2084.Level;

/// <summary>
/// How a game was started (notes §101). The arcade has one two-player game — both
/// players at once, on their own sticks — so <see cref="TwoPlayerSimultaneous"/> is the
/// faithful one; <see cref="TwoPlayerAlternate"/> is the port's own take-turns
/// two-player game, which is what its turn logic implements.
///
/// <see cref="TwoPlayerSimultaneous"/> is wired as far as mode selection and the two
/// inputs; the field itself still runs one player at a time, so it currently plays as
/// the alternate game.
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
