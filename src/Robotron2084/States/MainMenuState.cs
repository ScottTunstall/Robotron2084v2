using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Persistence;

namespace Robotron2084.States;

/// <summary>
///     The menu page: the cabinet art's robot over the game to start, the CLASSIC or EXTENDED choice, the settings and the
///     developer tools. It works like the GAME ADJUSTMENT page: a row for each choice, with its value beside it.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade starts a game from its START buttons.</item>
///         <item>
///             Keys: Up and Down, or a controller's pad or left stick, move the cursor. Left and Right, or a controller's pad
///             left and right, change the value of the row the cursor is on (coding standards UI-1): the game for one
///             player, for two players in turn, or for two players at once; or CLASSIC or EXTENDED. Enter or a
///             controller's A button does what the row says: starts the game shown, switches the style, or opens a page.
///         </item>
///         <item>
///             The only function keys are shortcuts to start a game. F1, F2 and F3 start a one player, two player (alternate) and two player
///             (simultaneous) game at once. F10 is the game's own key
///             for the DEVELOPER TOOLS page on every screen, so this page does not handle it.
///         </item>
///         <item>The game style is saved as soon as it changes. The game shown starts as one player each time the page opens.</item>
///         <item>Left alone for <see cref="IdleSeconds" />, the page goes back to the title, and the attract cycle runs.</item>
///         <item>
///             The robot is drawn in its own colours, in the space the title page's ROBOTRON: and 2084 take up. The row the
///             cursor is on strobes, as on the DEVELOPER TOOLS page.
///         </item>
///     </list>
/// </remarks>
public sealed class MainMenuState : IGameState
{
    /// <summary>How long the page waits with no key or button pressed before going back to the title, in seconds.</summary>
    private const int IdleSeconds = 30;

    /// <summary>Where the top of the robot is, in port pixels from the top.</summary>
    private const int RobotRow = 30;

    /// <summary>Where the first row of the menu is, in port pixels from the top.</summary>
    private const int FirstLineRow = 190;

    /// <summary>The gap from one row of the menu to the next, in port pixels.</summary>
    private const int LineStep = 26;

    /// <summary>The gap between the cursor and the start of its row, in port pixels.</summary>
    private const int CursorGap = 18;

    /// <summary>The gap between the longest label and the values, in port pixels.</summary>
    private const int ValueGap = 24;

    private const string SelectLine = "UP DOWN - SELECT   LEFT RIGHT - CHANGE";
    private const int SelectLineRow = 330;
    private const string ChooseLine = "ENTER - DO IT";
    private const int ChooseLineRow = 348;

    /// <summary>
    ///     The palette index of the rows, the cursor and the instructions: white. The page puts it back on its usual value
    ///     when it opens, since the screen it came from may have left it holding anything.
    /// </summary>
    private const int TextPaletteIndex = 9;

    /// <summary>The menu's rows, in order.</summary>
    private static readonly MainMenuLine[] Lines =
    [
        MainMenuLine.StartGame,
        MainMenuLine.GameStyle,
        MainMenuLine.Settings,
        MainMenuLine.DeveloperTools
    ];

    /// <summary>The games LEFT and RIGHT step through, in order.</summary>
    private static readonly GameMode[] GameModes =
    [
        GameMode.OnePlayer,
        GameMode.TwoPlayerAlternate,
        GameMode.TwoPlayerSimultaneous
    ];

    /// <summary>The function keys that start a game at once, each with the game it starts.</summary>
    private static readonly (Keys Key, GameMode GameMode)[] GameModeKeys =
    [
        (Keys.F1, GameMode.OnePlayer),
        (Keys.F2, GameMode.TwoPlayerAlternate),
        (Keys.F3, GameMode.TwoPlayerSimultaneous)
    ];

    private readonly DefineInputsHighlight _defineInputsHighlight = new();
    private readonly GameServices _gameServices;
    private readonly GameSettingsStore _gameSettingsStore;
    private readonly TimeSpan _idleDuration = TimeSpan.FromSeconds(IdleSeconds);
    private readonly TextureSet _textureSet;
    private GameMode _gameMode = GameMode.OnePlayer;
    private TimeSpan _idleElapsed;
    private InputSnapshot _previousSnapshot;

    /// <summary>Opens the page with the cursor on its first row.</summary>
    /// <param name="gameServices">The attract screens' shared services.</param>
    /// <param name="gameSettingsStore">Where the game style is saved when it changes, and what SETTINGS hands on.</param>
    public MainMenuState(GameServices gameServices, GameSettingsStore? gameSettingsStore = null)
    {
        _gameServices = gameServices;
        _textureSet = gameServices.TextureSet;
        _gameSettingsStore = gameSettingsStore ?? new GameSettingsStore();
        _previousSnapshot = InputSnapshot.Read();

        if (_textureSet.BlitterDraw.GamePalette is not { } palette) return;

        palette.SetPaletteIndexValue(TextPaletteIndex, GamePalette.DefaultPaletteValues[TextPaletteIndex]);
        _defineInputsHighlight.Start(palette);
    }

    /// <summary>The place in the menu of the row the cursor is on, counting from 0.</summary>
    internal int Line { get; private set; }

    /// <summary>Gets the words at the left of a row: what it is, and the function keys that are its shortcuts.</summary>
    /// <param name="line">The row.</param>
    /// <returns>The words, in characters the small font has.</returns>
    internal static string GetLabel(MainMenuLine line)
    {
        return line switch
        {
            MainMenuLine.StartGame => "PLAYER MODE",
            MainMenuLine.GameStyle => "GAME STYLE",
            MainMenuLine.Settings => "SETTINGS",
            _ => "F10 - DEVELOPER TOOLS"
        };
    }

    /// <summary>Gets the words of a row's value, which LEFT and RIGHT change; nothing for a row that has no value.</summary>
    /// <param name="line">The row.</param>
    /// <param name="gameMode">The game shown on the first row.</param>
    /// <param name="gameStyle">The game style chosen now.</param>
    /// <returns>The words, in characters the small font has, or an empty string.</returns>
    internal static string GetValue(MainMenuLine line, GameMode gameMode, GameStyle gameStyle)
    {
        if (line == MainMenuLine.StartGame) return GetGameModeName(gameMode);

        if (line == MainMenuLine.GameStyle) return GetGameStyleName(gameStyle);

        return string.Empty;
    }

    /// <summary>Gets the name of a game, as the first row shows it.</summary>
    internal static string GetGameModeName(GameMode gameMode)
    {
        return gameMode switch
        {
            GameMode.OnePlayer => "START ONE PLAYER GAME (F1)",
            GameMode.TwoPlayerAlternate => "START TWO PLAYER ALTERNATE (F2)",
            _ => "START TWO PLAYER SIMULTANEOUS (F3)"
        };
    }

    /// <summary>Gets the name of a game style, as the second row shows it.</summary>
    internal static string GetGameStyleName(GameStyle gameStyle)
    {
        if (gameStyle == GameStyle.Extended) return "EXTENDED";

        return "CLASSIC";
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawCentredLogo(spriteBatch, _textureSet.MainMenuRobotTexture, RobotRow);

        var labelColumn = GetLabelColumn();
        var valueColumn = labelColumn + GetWidestLabelWidth() + ValueGap;
        for (var index = 0; index < Lines.Length; index++)
        {
            var y = FirstLineRow + index * LineStep;
            var paletteIndex = TextPaletteIndex;
            if (index == Line)
            {
                DrawCursor(spriteBatch, labelColumn - CursorGap, y);
                paletteIndex = DefineInputsHighlight.PaletteIndex;
            }

            _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, GetLabel(Lines[index]), labelColumn, y, paletteIndex);
            _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, GetValue(Lines[index], _gameMode, _gameServices.GameSettings.GameStyle),
                valueColumn, y, paletteIndex);
        }

        DrawCentredSmallText(spriteBatch, SelectLine, SelectLineRow);
        DrawCentredSmallText(spriteBatch, ChooseLine, ChooseLineRow);
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (_textureSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Update(palette);

        var now = InputSnapshot.Read();
        _idleElapsed += gameTime.ElapsedGameTime;
        if (IsAnyKeyOrButtonPressed(now)) _idleElapsed = TimeSpan.Zero;

        var isLeaving = HandleShortcuts(now, gameStateManager) || HandleCursorKeys(now, gameStateManager);
        _previousSnapshot = now;
        if (isLeaving) return;

        if (_idleElapsed >= _idleDuration) gameStateManager.TransitionTo(new TitleScreenState(_gameServices));
    }

    /// <summary>
    ///     Starts the game a function key names, if one went down: F1 a one player game, F2 a two player game in turn, F3 a two
    ///     player game at once.
    /// </summary>
    /// <param name="now">This tick's input.</param>
    /// <param name="gameStateManager">The screens, for the game it starts.</param>
    /// <returns>True when the page has been left for another.</returns>
    private bool HandleShortcuts(InputSnapshot now, GameStateManager gameStateManager)
    {
        foreach (var (key, gameMode) in GameModeKeys)
        {
            if (!WasKeyPressed(now, key)) continue;

            _gameMode = gameMode;
            return Choose(MainMenuLine.StartGame, gameStateManager);
        }

        return false;
    }

    /// <summary>
    ///     Moves the cursor on Up and Down, changes the value of its row on Left and Right, and does what its row says on
    ///     Enter or A.
    /// </summary>
    /// <param name="now">This tick's input.</param>
    /// <param name="gameStateManager">The screens, for a row that opens another.</param>
    /// <returns>True when the page has been left for another.</returns>
    private bool HandleCursorKeys(InputSnapshot now, GameStateManager gameStateManager)
    {
        if (WasPressed(now, Keys.Up, Buttons.DPadUp) || WasPressed(now, Keys.Up, Buttons.LeftThumbstickUp)) MoveCursor(-1);

        if (WasPressed(now, Keys.Down, Buttons.DPadDown) || WasPressed(now, Keys.Down, Buttons.LeftThumbstickDown)) MoveCursor(1);

        if (WasPressed(now, Keys.Left, Buttons.DPadLeft)) ChangeValue(-1);

        if (WasPressed(now, Keys.Right, Buttons.DPadRight)) ChangeValue(1);

        if (WasPressed(now, Keys.Enter, Buttons.A)) return Choose(Lines[Line], gameStateManager);

        return false;
    }

    /// <summary>Moves the cursor up (-1) or down (1) a row, wrapping at either end.</summary>
    private void MoveCursor(int step)
    {
        Line = (Line + step + Lines.Length) % Lines.Length;
    }

    /// <summary>
    ///     Changes the value of the row the cursor is on, as LEFT (-1) and RIGHT (1) do on the GAME ADJUSTMENT page: the game
    ///     shown steps through its three, wrapping at either end, and the game style switches whichever way it is pushed.
    ///     A row with no value does nothing.
    /// </summary>
    /// <param name="step">-1 for LEFT, 1 for RIGHT.</param>
    private void ChangeValue(int step)
    {
        if (Lines[Line] == MainMenuLine.StartGame)
        {
            var place = Array.IndexOf(GameModes, _gameMode);
            _gameMode = GameModes[(place + step + GameModes.Length) % GameModes.Length];
            return;
        }

        if (Lines[Line] == MainMenuLine.GameStyle) SwitchGameStyle();
    }

    /// <summary>Switches between CLASSIC and EXTENDED, and saves the choice at once.</summary>
    private void SwitchGameStyle()
    {
        _gameServices.GameSettings.ToggleGameStyle();
        _gameSettingsStore.Save(_gameServices.GameSettings);
    }

    /// <summary>Does what a row says: starts the game shown, switches the game style, or opens a page.</summary>
    /// <param name="line">The row.</param>
    /// <param name="gameStateManager">The screens, for a row that opens another.</param>
    /// <returns>True when the page has been left for another.</returns>
    private bool Choose(MainMenuLine line, GameStateManager gameStateManager)
    {
        Line = Array.IndexOf(Lines, line);
        switch (line)
        {
            case MainMenuLine.StartGame:
                gameStateManager.TransitionTo(PlayingState.CreateNewGame(_gameServices.ControlSettings, _gameServices.GameSettings,
                    _gameMode, _textureSet, _gameServices.HighScoreStore, _gameServices.Random));
                return true;

            case MainMenuLine.GameStyle:
                SwitchGameStyle();
                return false;

            case MainMenuLine.Settings:
                gameStateManager.TransitionTo(new SettingsState(_gameServices, _gameSettingsStore));
                return true;

            default:
                gameStateManager.TransitionTo(new DeveloperToolsState(_gameServices, null, _gameSettingsStore));
                return true;
        }
    }

    /// <summary>Gets the width of the longest label, in port pixels.</summary>
    private int GetWidestLabelWidth()
    {
        var widest = Lines.Max(line => _textureSet.ArcadeText.MeasureSmallText(GetLabel(line)));
        return ScreenSize.ToPortPixelsFromArcadePixels(widest);
    }

    /// <summary>Gets where the labels start, so that the labels and the longest value together are in the middle of the screen.</summary>
    private int GetLabelColumn()
    {
        var widestValue = GameModes.Max(gameMode => _textureSet.ArcadeText.MeasureSmallText(GetGameModeName(gameMode)));
        var width = GetWidestLabelWidth() + ValueGap + ScreenSize.ToPortPixelsFromArcadePixels(widestValue);
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>Draws the cursor at the left of the line it is on: the arcade's own "->".</summary>
    private void DrawCursor(SpriteBatch spriteBatch, int x, int y)
    {
        _textureSet.BlitterDraw.DrawGlyphStatic(spriteBatch, _textureSet.CursorArrowTexture, x, y, TextPaletteIndex);
    }

    /// <summary>Draws a logo in the middle of the screen, at the port's sprite scale.</summary>
    private void DrawCentredLogo(SpriteBatch spriteBatch, Texture2D texture, int y)
    {
        var width = texture.Width * ScreenSize.PortPixelsPerArcadePixel;
        var height = texture.Height * ScreenSize.PortPixelsPerArcadePixel;
        _textureSet.BlitterDraw.DrawSprite(spriteBatch, texture, new Rectangle((ScreenSize.Width - width) / 2, y, width, height),
            Color.White);
    }

    /// <summary>Draws a line in the small font, in the middle of the screen.</summary>
    private void DrawCentredSmallText(SpriteBatch spriteBatch, string text, int y)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.ArcadeText.MeasureSmallText(text));
        _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, TextPaletteIndex);
    }

    /// <summary>Says whether any key, or any button on either controller, went down this tick.</summary>
    private bool IsAnyKeyOrButtonPressed(InputSnapshot now)
    {
        var isKeyPressed = now.KeyboardState.GetPressedKeys().Any(key => !_previousSnapshot.KeyboardState.IsKeyDown(key));
        var isPadOnePressed = now.PlayerOneGamePadState.Buttons != _previousSnapshot.PlayerOneGamePadState.Buttons ||
                              now.PlayerOneGamePadState.DPad != _previousSnapshot.PlayerOneGamePadState.DPad;
        var isPadTwoPressed = now.PlayerTwoGamePadState.Buttons != _previousSnapshot.PlayerTwoGamePadState.Buttons ||
                              now.PlayerTwoGamePadState.DPad != _previousSnapshot.PlayerTwoGamePadState.DPad;
        return isKeyPressed || isPadOnePressed || isPadTwoPressed;
    }

    /// <summary>True on the tick a key goes down.</summary>
    private bool WasKeyPressed(InputSnapshot now, Keys key)
    {
        return now.KeyboardState.IsKeyDown(key) && !_previousSnapshot.KeyboardState.IsKeyDown(key);
    }

    /// <summary>True on the tick a key, or a button on either controller, goes down.</summary>
    private bool WasPressed(InputSnapshot now, Keys key, Buttons button)
    {
        var isPadOnePressed = now.PlayerOneGamePadState.IsButtonDown(button) &&
                              !_previousSnapshot.PlayerOneGamePadState.IsButtonDown(button);
        var isPadTwoPressed = now.PlayerTwoGamePadState.IsButtonDown(button) &&
                              !_previousSnapshot.PlayerTwoGamePadState.IsButtonDown(button);
        return WasKeyPressed(now, key) || isPadOnePressed || isPadTwoPressed;
    }
}
