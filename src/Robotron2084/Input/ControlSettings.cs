using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;

namespace Robotron2084.Input;

/// <summary>
/// Everything the DEFINE INPUTS page edits (notes §101): both players' eight
/// bindings plus the port's global PAUSE key. Port-only — the cabinet's wiring is
/// fixed — and saved to <c>%LocalAppData%\Robotron2084\controls.ini</c> so a
/// definition survives a restart.
/// </summary>
public sealed class ControlSettings
{
    /// <summary>How many players the page shows columns for (the arcade's PLRCNT maximum).</summary>
    public const int PlayerCount = 2;

    private readonly PlayerControls[] _players = [new(), new()];

    /// <summary>Both players' controls, player 1 first.</summary>
    public PlayerControls this[int playerIndex] => _players[playerIndex];

    /// <summary>
    /// The global PAUSE binding, default <c>P</c>. ONE binding, not a key/pad pair: whichever
    /// device the page captured it on is the one it lives on (notes §101).
    /// </summary>
    public InputBinding Pause { get; set; } = InputBinding.Key(Keys.P);

    /// <summary>The port's factory settings: see <see cref="PlayerControls.Defaults"/>.</summary>
    public static ControlSettings Defaults()
    {
        var settings = new ControlSettings();
        settings.ResetToDefaults();
        return settings;
    }

    /// <summary>The page's <c>R</c>: every line back to the factory scheme.</summary>
    public void ResetToDefaults()
    {
        for (int player = 0; player < PlayerCount; player++)
        {
            _players[player] = PlayerControls.Defaults(player);
        }

        Pause = InputBinding.Key(Keys.P);
    }

    /// <summary>True while the pause binding is held.</summary>
    public bool PauseHeld(KeyboardState keys, GamePadState padOne, GamePadState padTwo) =>
        Pause.IsHeld(keys, padOne, padTwo);

    /// <summary>
    /// Reads one player's controls into the state the game consumes. The start buttons
    /// are NOT bindable (the arcade's START 1 / START 2 have their own keys, and the
    /// title also offers F1/F2/F3), so they stay hardwired here, as do the two
    /// long-standing port aliases: Space as a fire button and <c>Insert</c> as the
    /// skip-level test key (<c>P</c> is PAUSE, not skip-level).
    /// </summary>
    public PlayerInputState ReadPlayer(int playerIndex, KeyboardState keys, GamePadState padOne, GamePadState padTwo)
    {
        PlayerControls controls = _players[playerIndex];
        IntVector2 move = controls.MoveDirection(keys, padOne, padTwo);
        IntVector2 shoot = controls.ShootDirection(keys, padOne, padTwo);
        GamePadState own = playerIndex == 1 ? padTwo : padOne;

        bool fire = controls.Firing(keys, padOne, padTwo)
            || keys.IsKeyDown(Keys.Space)
            || own.IsButtonDown(Buttons.A)
            || own.Triggers.Right > 0.5f;

        return new PlayerInputState(
            move,
            shoot,
            fire,
            keys.IsKeyDown(Keys.Insert),
            keys.IsKeyDown(Keys.D1) || keys.IsKeyDown(Keys.NumPad1) || padOne.IsButtonDown(Buttons.Start),
            keys.IsKeyDown(Keys.D2) || keys.IsKeyDown(Keys.NumPad2) || padOne.IsButtonDown(Buttons.Back));
    }
}
