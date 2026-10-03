using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Persistence;

namespace Robotron2084.States;

/// <summary>
/// The five things the attract sequence's screens all need (notes §101, §131): the sprite
/// set, the high score store, the port's control definitions, player 1's input, and the
/// GAME ADJUSTMENT settings.
///
/// They travel together because every attract screen needs all of them: passed one by one, each
/// attract screen's constructor grew a parameter whenever a port-only feature needed something,
/// and the DEFINITIONS page made that four and the settings page five. The in-game states do NOT need
/// this bundle: they carry the <see cref="Level.GameSession"/>, which already knows the
/// controls, the settings and both players' inputs.
/// </summary>
public sealed record GameServices(
    SpriteSet Sprites,
    HighScoreStore HighScores,
    ControlSettings Controls,
    IPlayerInputSource Input,
    GameSettings Settings)
{
    /// <summary>
    /// Builds the bundle from a game in progress — what the play states do when a game
    /// ends and the machine goes back to the title (the session already carries the
    /// controls and the settings, so nothing has to be threaded through for that).
    /// </summary>
    public static GameServices CreateFrom(Level.GameSession session, SpriteSet sprites, HighScoreStore highScores, IPlayerInputSource input) =>
        new(sprites, highScores, session.Controls, input, session.Settings);
}
