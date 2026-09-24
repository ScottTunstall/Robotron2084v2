using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Rendering;

namespace Robotron2084.States;

/// <summary>
/// The four things the attract sequence's screens all need (notes §101): the sprite
/// set, the high score store, the port's control definitions, and player 1's input.
///
/// They travel together because every attract screen needs all four: passed one by one, each
/// attract screen's constructor grew a parameter whenever a port-only feature needed something,
/// and the DEFINITIONS page made that four. The in-game states do NOT need
/// this bundle: they carry the <see cref="Level.GameSession"/>, which already knows the
/// controls and both players' inputs.
/// </summary>
public sealed record GameServices(
    SpriteSet Sprites,
    HighScoreStore HighScores,
    ControlSettings Controls,
    IPlayerInputSource Input)
{
    /// <summary>
    /// Builds the bundle from a game in progress — what the play states do when a game
    /// ends and the machine goes back to the title (the session already carries the
    /// controls, so nothing has to be threaded through for that).
    /// </summary>
    public static GameServices From(Level.GameSession session, SpriteSet sprites, HighScoreStore highScores, IPlayerInputSource input) =>
        new(sprites, highScores, session.Controls, input);
}
