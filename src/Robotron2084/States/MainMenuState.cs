using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Audio;
using Robotron2084.Audio.Models;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input.Models;
using Robotron2084.Level.Models;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.States.Models;

namespace Robotron2084.States;

/// <summary>
///     The menu page: the cabinet art's robot over the game to start, the CLASSIC or EXTENDED choice, the input
///     definitions, the settings and the developer tools. It works like the GAME ADJUSTMENT page: a row for each choice, with its value beside it.
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
///             In EXTENDED, left alone for <see cref="NoCoinsIdleSeconds" />, the Berzerk robot says "NO COINS DETECTED IN
///             POCKET" (<see cref="NoCoinsSpeech" />), once for that spell of being left alone. Using the page lets it say it again the
///             next time the page is left alone.
///         </item>
///         <item>
///             The picture of the robots fills the screen, in its own colours. Each block of words sits on a dark box
///             the picture shows through, so the words can be read. The row the cursor is on strobes, as on the
///             DEVELOPER TOOLS page.
///         </item>
///     </list>
/// </remarks>
public sealed class MainMenuState : IGameState
{
    private const string ChooseLine = "ENTER - DO IT";

    private const int ChooseLineRow = 348;

    /// <summary>The gap between the cursor and the start of its row, in port pixels.</summary>
    private const int CursorGap = 18;

    /// <summary>Where the first row of the menu is, in port pixels from the top.</summary>
    private const int FirstLineRow = 190;

    /// <summary>How long the page waits with no key or button pressed before going back to the title, in seconds.</summary>
    private const int IdleSeconds = 30;

    /// <summary>The gap from one row of the menu to the next, in port pixels.</summary>
    private const int LineStep = 26;

    /// <summary>How long the page waits with no key or button pressed before the Berzerk robot speaks, in seconds, in EXTENDED.</summary>
    /// <remarks>Port only: the author's choice.</remarks>
    private const int NoCoinsIdleSeconds = 10;

    private const string SelectLine = "UP DOWN - SELECT   LEFT RIGHT - CHANGE";

    private const int SelectLineRow = 330;

    /// <summary>
    ///     The sound board's number for the sound played whenever an option on the page is selected: ED12 (see
    ///     <see cref="BoardSounds" />).
    /// </summary>
    /// <remarks>Port only: the author's choice.</remarks>
    private const int SelectSoundNumber = 0x0C;

    /// <summary>How dark the box behind the words is: 0 lets all of the picture show through, 1 hides it.</summary>
    private const float ShadeOpacity = 0.65f;

    /// <summary>The gap between the words and the edge of the dark box behind them, in port pixels.</summary>
    private const int ShadePadding = 8;

    /// <summary>
    ///     The palette index of the rows, the cursor and the instructions: white. The page puts it back on its usual value
    ///     when it opens, since the screen it came from may have left it holding anything.
    /// </summary>
    private const int TextPaletteIndex = 9;

    /// <summary>The gap between the longest label and the values, in port pixels.</summary>
    private const int ValueGap = 24;

    /// <summary>The function keys that start a game at once, each with the game it starts.</summary>
    private static readonly (Keys Key, GameMode GameMode)[] GameModeKeys =
    [
        (Keys.F1, GameMode.OnePlayer),
        (Keys.F2, GameMode.TwoPlayerAlternate),
        (Keys.F3, GameMode.TwoPlayerSimultaneous)
    ];

    /// <summary>The games LEFT and RIGHT step through, in order.</summary>
    private static readonly GameMode[] GameModes =
    [
        GameMode.OnePlayer,
        GameMode.TwoPlayerAlternate,
        GameMode.TwoPlayerSimultaneous
    ];

    /// <summary>The menu's rows, in order.</summary>
    private static readonly MainMenuLine[] Lines =
    [
        MainMenuLine.StartGame,
        MainMenuLine.GameStyle,
        MainMenuLine.DefineInputs,
        MainMenuLine.Settings,
        MainMenuLine.DeveloperTools
    ];

    /// <summary>The words the Berzerk robot says when the page is left alone: "NO COINS DETECTED IN POCKET".</summary>
    /// <remarks>
    ///     Original source: the author's Berzerk disassembly, <c>berzerk.asm</c> <c>COINS_DETECTED_IN_POCKET</c>
    ///     (<c>$1AD6</c>), which has no NO at the start; the author asked for it.
    /// </remarks>
    private static readonly BerzerkWord[] NoCoinsSpeech =
        [BerzerkWord.No, BerzerkWord.Coins, BerzerkWord.Detected, BerzerkWord.In, BerzerkWord.Pocket];

    /// <summary>
    ///     The voice that speech is said in: Berzerk's own control byte for it, <c>$65</c> (pitch 5, volume 4), from the same
    ///     routine.
    /// </summary>
    private static readonly BerzerkVoiceSetting NoCoinsVoiceSetting = BerzerkVoiceSetting.CreateFromControlByte(0x65);

    private readonly DefineInputsHighlight _defineInputsHighlight = new();
    private readonly GameSettingsStore _gameSettingsStore;
    private readonly GameStateParams _gameStateParams;
    private readonly TimeSpan _idleDuration = TimeSpan.FromSeconds(IdleSeconds);
    private readonly TextureSet _textureSet;
    private GameMode _gameMode = GameMode.OnePlayer;

    /// <summary>True once the robot has spoken for the spell of being left alone that is going on.</summary>
    private bool _hasSpokenThisIdleSpell;

    private TimeSpan _idleElapsed;
    private InputSnapshot _previousSnapshot;

    /// <summary>Opens the page with the cursor on its first row.</summary>
    /// <param name="gameStateParams">The attract screens' shared services.</param>
    /// <param name="gameSettingsStore">Where the game style is saved when it changes, and what SETTINGS hands on.</param>
    public MainMenuState(GameStateParams gameStateParams, GameSettingsStore? gameSettingsStore = null)
    {
        _gameStateParams = gameStateParams;
        _textureSet = gameStateParams.TextureSet;
        _gameSettingsStore = gameSettingsStore ?? new GameSettingsStore();
        _previousSnapshot = InputSnapshot.Read();

        if (_textureSet.BlitterDraw.GamePalette is not { } palette) return;

        palette.SetPaletteIndexValue(TextPaletteIndex, GamePalette.DefaultPaletteValues[TextPaletteIndex]);
        _defineInputsHighlight.Start(palette);
    }

    /// <summary>The place in the menu of the row the cursor is on, counting from 0.</summary>
    internal int Line { get; private set; }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        _textureSet.BlitterDraw.DrawSprite(spriteBatch, _textureSet.MainMenuRobotTexture,
            new Rectangle(0, 0, ScreenSize.Width, ScreenSize.Height), Color.White);

        var labelColumn = GetLabelColumn();
        var menuLeft = labelColumn - CursorGap;
        var lastLineRow = FirstLineRow + (Lines.Length - 1) * LineStep;
        DrawShade(spriteBatch, menuLeft, ScreenSize.Width - menuLeft, FirstLineRow, lastLineRow);

        var selectLineLeft = GetCentredSmallTextColumn(SelectLine);
        DrawShade(spriteBatch, selectLineLeft, ScreenSize.Width - selectLineLeft, SelectLineRow, ChooseLineRow);

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
            _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, GetValue(Lines[index], _gameMode, _gameStateParams.GameSettings.GameStyle),
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
        if (IsAnyKeyOrButtonPressed(now))
        {
            _idleElapsed = TimeSpan.Zero;
            _hasSpokenThisIdleSpell = false;
        }

        SayNoCoinsIfLeftAlone();

        var isLeaving = HandleShortcuts(now, gameStateManager) || HandleCursorKeys(now, gameStateManager);
        _previousSnapshot = now;
        if (isLeaving) return;

        if (_idleElapsed >= _idleDuration) gameStateManager.TransitionTo(new TitleScreenState(_gameStateParams));
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
            MainMenuLine.DefineInputs => "DEFINE INPUTS",
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

    /// <summary>
    ///     Changes the value of the row the cursor is on, as LEFT (-1) and RIGHT (1) do on the GAME ADJUSTMENT page: the game
    ///     shown steps through its three, wrapping at either end, and the game style switches whichever way it is pushed.
    ///     A row with no value does nothing. It is silent.
    /// </summary>
    /// <param name="step">-1 for LEFT, 1 for RIGHT.</param>
    internal void ChangeValue(int step)
    {
        if (Lines[Line] == MainMenuLine.StartGame)
        {
            var place = Array.IndexOf(GameModes, _gameMode);
            _gameMode = GameModes[(place + step + GameModes.Length) % GameModes.Length];
            return;
        }

        if (Lines[Line] == MainMenuLine.GameStyle) SwitchGameStyle();
    }

    /// <summary>
    ///     Does what a row says: starts the game shown, switches the game style, or opens a page. It plays the select sound
    ///     first, for every row.
    /// </summary>
    /// <param name="line">The row.</param>
    /// <param name="gameStateManager">The screens, for a row that opens another.</param>
    /// <returns>True when the page has been left for another.</returns>
    internal bool Choose(MainMenuLine line, GameStateManager gameStateManager)
    {
        PlaySelectSound();
        Line = Array.IndexOf(Lines, line);
        switch (line)
        {
            case MainMenuLine.StartGame:
                gameStateManager.TransitionTo(PlayingState.CreateNewGame(_gameStateParams.ControlSettings, _gameStateParams.GameSettings,
                    _gameMode, _textureSet, _gameStateParams.HighScoreStore, _gameStateParams.Random));
                return true;

            case MainMenuLine.GameStyle:
                SwitchGameStyle();
                return false;

            case MainMenuLine.Settings:
                gameStateManager.TransitionTo(new SettingsState(_gameStateParams, _gameSettingsStore));
                return true;

            case MainMenuLine.DefineInputs:
                gameStateManager.TransitionTo(new DefineInputsState(_gameStateParams, new ControlSettingsStore()));
                return true;

            default:
                gameStateManager.TransitionTo(new DeveloperToolsState(_gameStateParams, null, _gameSettingsStore));
                return true;
        }
    }

    /// <summary>Moves the cursor up (-1) or down (1) a row, wrapping at either end. It is silent.</summary>
    internal void MoveCursor(int step)
    {
        Line = (Line + step + Lines.Length) % Lines.Length;
    }

    /// <summary>Plays the sound that tells the player an option has been selected (<see cref="SelectSoundNumber" />).</summary>
    private static void PlaySelectSound()
    {
        Sound.SendDirect(SelectSoundNumber);
    }

    /// <summary>Draws a line in the small font, in the middle of the screen.</summary>
    private void DrawCentredSmallText(SpriteBatch spriteBatch, string text, int y)
    {
        _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, text, GetCentredSmallTextColumn(text), y, TextPaletteIndex);
    }

    /// <summary>Draws the cursor at the left of the line it is on: the arcade's own "->".</summary>
    private void DrawCursor(SpriteBatch spriteBatch, int x, int y)
    {
        _textureSet.BlitterDraw.DrawGlyphStatic(spriteBatch, _textureSet.CursorArrowTexture, x, y, TextPaletteIndex);
    }

    /// <summary>
    ///     Draws a see-through dark box behind a block of words in the small font, a little bigger than the words on
    ///     every side.
    /// </summary>
    /// <param name="spriteBatch">Where the box is drawn.</param>
    /// <param name="left">Where the words start, in port pixels from the left.</param>
    /// <param name="right">Where the words end, in port pixels from the left.</param>
    /// <param name="firstRow">The top of the first line of words, in port pixels from the top.</param>
    /// <param name="lastRow">The top of the last line of words, in port pixels from the top.</param>
    private void DrawShade(SpriteBatch spriteBatch, int left, int right, int firstRow, int lastRow)
    {
        var textHeight = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.FontSmallTextures[0].Height);
        var bounds = new Rectangle(left - ShadePadding, firstRow - ShadePadding, right - left + 2 * ShadePadding,
            lastRow - firstRow + textHeight + 2 * ShadePadding);
        _textureSet.BlitterDraw.DrawSolidRectangle(spriteBatch, bounds, Color.Black * ShadeOpacity);
    }

    /// <summary>Gets where a line in the small font starts, so that it is in the middle of the screen.</summary>
    /// <param name="text">The line.</param>
    /// <returns>Where the line starts, in port pixels from the left.</returns>
    private int GetCentredSmallTextColumn(string text)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.ArcadeText.MeasureSmallText(text));
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>Gets where the labels start, so that the labels and the longest value together are in the middle of the screen.</summary>
    private int GetLabelColumn()
    {
        var widestValue = GameModes.Max(gameMode => _textureSet.ArcadeText.MeasureSmallText(GetGameModeName(gameMode)));
        var width = GetWidestLabelWidth() + ValueGap + ScreenSize.ToPortPixelsFromArcadePixels(widestValue);
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>Gets the width of the longest label, in port pixels.</summary>
    private int GetWidestLabelWidth()
    {
        var widest = Lines.Max(line => _textureSet.ArcadeText.MeasureSmallText(GetLabel(line)));
        return ScreenSize.ToPortPixelsFromArcadePixels(widest);
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

    /// <summary>
    ///     In EXTENDED, once the page has been left alone for <see cref="NoCoinsIdleSeconds" />, has the Berzerk robot say
    ///     "NO COINS DETECTED IN POCKET". It says it once for each spell of being left alone, not again every
    ///     <see cref="NoCoinsIdleSeconds" />.
    /// </summary>
    private void SayNoCoinsIfLeftAlone()
    {
        var isLeftAlone = _idleElapsed >= TimeSpan.FromSeconds(NoCoinsIdleSeconds);
        var isExtended = _gameStateParams.GameSettings.GameStyle == GameStyle.Extended;
        if (_hasSpokenThisIdleSpell || !isLeftAlone || !isExtended) return;

        _hasSpokenThisIdleSpell = true;
        Speech.SayBerzerk(NoCoinsVoiceSetting, NoCoinsSpeech);
    }

    /// <summary>Switches between CLASSIC and EXTENDED, and saves the choice at once.</summary>
    private void SwitchGameStyle()
    {
        _gameStateParams.GameSettings.ToggleGameStyle();
        _gameSettingsStore.Save(_gameStateParams.GameSettings);
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
