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

    /// <summary>
    /// The port's fire alias: SPACE and the pad's A button, on top of the bound SHOOT directions.
    /// Fixed, not rebindable — the DEFINITIONS page binds the eight stick directions and the pause
    /// line, and these two are the fallbacks a player expects without setting anything up.
    /// </summary>
    private const Keys FireAliasKey = Keys.Space;

    /// <inheritdoc cref="FireAliasKey"/>
    private const Buttons FireAliasButton = Buttons.A;

    /// <summary>How far the pad's trigger must be pulled to count as fire (a trigger is an axis).</summary>
    private const float FireTriggerThreshold = 0.5f;

    /// <summary>The port's skip-level test key. Fixed, like the other port-only keys in this block.</summary>
    private const Keys SkipLevelKey = Keys.Insert;

    /// <summary>
    /// The keyboard keys that start player 1's game. Fixed, not rebindable: the arcade's START 1 /
    /// START 2 are cabinet buttons on their own keys, and the title screen offers F1/F2/F3 as well.
    /// </summary>
    private const Keys StartOneKey = Keys.D1;

    /// <inheritdoc cref="StartOneKey"/>
    private const Keys StartOneNumPadKey = Keys.NumPad1;

    /// <inheritdoc cref="StartOneKey"/>
    private const Buttons StartOneButton = Buttons.Start;

    /// <summary>The keyboard keys that start a two-player game. Fixed, like <see cref="StartOneKey"/>.</summary>
    private const Keys StartTwoKey = Keys.D2;

    /// <inheritdoc cref="StartTwoKey"/>
    private const Keys StartTwoNumPadKey = Keys.NumPad2;

    /// <inheritdoc cref="StartTwoKey"/>
    private const Buttons StartTwoButton = Buttons.Back;

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
            || keys.IsKeyDown(FireAliasKey)
            || own.IsButtonDown(FireAliasButton)
            || own.Triggers.Right > FireTriggerThreshold;

        return new PlayerInputState(
            move,
            shoot,
            fire,
            keys.IsKeyDown(SkipLevelKey),
            keys.IsKeyDown(StartOneKey) || keys.IsKeyDown(StartOneNumPadKey) || padOne.IsButtonDown(StartOneButton),
            keys.IsKeyDown(StartTwoKey) || keys.IsKeyDown(StartTwoNumPadKey) || padOne.IsButtonDown(StartTwoButton));
    }
}
