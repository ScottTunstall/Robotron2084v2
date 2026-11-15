using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;

namespace Robotron2084.States.Models;

/// <summary>
///     The six things the attract sequence's screens all need (notes §101, §131): the sprite
///     set, the high score store, the port's control definitions, player 1's input, the
///     GAME ADJUSTMENT settings, and the one source of random numbers the game shares.
///     They travel together because every attract screen needs all of them: passed one by one, each
///     attract screen's constructor grew a parameter whenever a port-only feature needed something,
///     and the DEFINITIONS page made that four and the settings page five. The in-game states do NOT need
///     this bundle: they carry the <see cref="Level.GameSession" />, which already knows the
///     controls, the settings and both players' inputs.
/// </summary>
public sealed record GameStateParams
{
    /// <summary>Makes what the attract screens are given.</summary>
    /// <param name="textureSet">The sprite set.</param>
    /// <param name="highScoreStore">Where the high score table is kept.</param>
    /// <param name="controlSettings">The port's control definitions.</param>
    /// <param name="playerInputSource">Player 1's input.</param>
    /// <param name="gameSettings">The GAME ADJUSTMENT settings.</param>
    /// <param name="random">The one source of random numbers the game shares.</param>
    /// <exception cref="ArgumentNullException">Any of them is null.</exception>
    public GameStateParams(
        TextureSet textureSet,
        HighScoreStore highScoreStore,
        ControlSettings controlSettings,
        IPlayerInputSource playerInputSource,
        GameSettings gameSettings,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(textureSet);
        ArgumentNullException.ThrowIfNull(highScoreStore);
        ArgumentNullException.ThrowIfNull(controlSettings);
        ArgumentNullException.ThrowIfNull(playerInputSource);
        ArgumentNullException.ThrowIfNull(gameSettings);
        ArgumentNullException.ThrowIfNull(random);

        TextureSet = textureSet;
        HighScoreStore = highScoreStore;
        ControlSettings = controlSettings;
        PlayerInputSource = playerInputSource;
        GameSettings = gameSettings;
        Random = random;
    }

    /// <summary>The sprite set.</summary>
    public TextureSet TextureSet { get; }

    /// <summary>Where the high score table is kept.</summary>
    public HighScoreStore HighScoreStore { get; }

    /// <summary>The port's control definitions.</summary>
    public ControlSettings ControlSettings { get; }

    /// <summary>Player 1's input.</summary>
    public IPlayerInputSource PlayerInputSource { get; }

    /// <summary>The GAME ADJUSTMENT settings.</summary>
    public GameSettings GameSettings { get; }

    /// <summary>The one source of random numbers the game shares.</summary>
    public Random Random { get; }

    /// <summary>
    ///     Builds the bundle from a game in progress — what the play states do when a game
    ///     ends and the machine goes back to the title (the session already carries the
    ///     controls and the settings, so nothing has to be threaded through for that).
    /// </summary>
    /// <param name="gameSession">The game in progress, which knows the controls and the settings.</param>
    /// <param name="textureSet">The sprite set.</param>
    /// <param name="highScoreStore">Where the high score table is kept.</param>
    /// <param name="playerInputSource">Player 1's input.</param>
    /// <param name="random">The one source of random numbers the game shares.</param>
    /// <returns>The bundle.</returns>
    public static GameStateParams CreateFrom(GameSession gameSession, TextureSet textureSet, HighScoreStore highScoreStore,
        IPlayerInputSource playerInputSource, Random random)
    {
        return new GameStateParams(textureSet, highScoreStore, gameSession.ControlSettings, playerInputSource, gameSession.GameSettings,
            random);
    }
}
