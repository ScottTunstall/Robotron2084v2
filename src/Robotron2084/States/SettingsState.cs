using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Palette;
using Robotron2084.Persistence;

namespace Robotron2084.States;

/// <summary>
///     The GAME ADJUSTMENT page (notes §131) — the cabinet's service-mode settings, opened with F5 from
///     the attract screens and closed with F10, which goes back to the DEVELOPER TOOLS page that leads
///     to every service page.
///     It is the same page the arcade's own GAME ADJUSTMENT screen is: a left column of setting names
///     with their values in a second column, the page's own "->" cursor at the left of the line the
///     cursor is on, and the instructions under the list (notes §108) — WHITE for the heading, the
///     instructions and the descriptive words, GREEN for the settings themselves.
///     Only the arcade's gameplay rows are built (its coin/pricing rows are not): EXTRA MAN EVERY,
///     TURNS PER PLAYER, DIFFICULTY OF PLAY, RESTORE FACTORY SETTINGS and HIGH SCORE TABLE RESET — plus
///     the port's own ATTRACT MODE SOUND row, which keeps the attract sequence's sounds (the demo machine
///     playing itself) controllable from this page, and its TANK SHELL BUG, BRAINS CHASE MIKEY BUG and
///     BOZO MODE rows, which switch three of the arcade's own behaviours off.
///     Every change is written straight to <c>settings.ini</c>, so the settings are there the next time
///     the game starts whatever happens next.
/// </summary>
public sealed class SettingsState : IGameState
{
    private const string ActivateLine = "ENTER - ACTIVATE A ROW THAT READS YES";
    private const string ChooseLine = "UP DOWN - SELECT A SETTING   LEFT RIGHT - CHANGE IT";

    // The cursor sits just left of the label column, exactly where the arcade's GAME ADJUSTMENT page
    // prints its cursor (column $0C), and it is that page's own cursor glyph — the small font's "->".
    private const int CursorColumn = 57;

    private const string ExitLine = "F10 - DEVELOPER TOOLS";
    private const int ExitRow = 342;
    private const int FirstLineRow = 76;

    /// <summary>It is one of the palette indices in <see cref="OwnedPaletteIndices" />.</summary>
    private const int HeadingPaletteIndex = 9;

    // The page's own colours (notes §108): headings, instructions and the words under each value in
    // the palette's WHITE ($FF, palette index 9), the settings' own text in its GREEN ($38, palette index 6). They are
    // the entries the arcade's page uses (its text colour $66 is palette index 6). No palette index can be assumed to
    // hold its CRTAB value — the page is opened from an attract screen, which leaves its own colours
    // up — so the page writes the two itself on entry (see OwnedPaletteIndices).
    /// <summary>It is one of the palette indices in <see cref="OwnedPaletteIndices" />.</summary>
    private const int InputPaletteIndex = 6;

    private const int InstructionsRow = 294;
    private const int LabelColumn = 75;
    private const int LineStep = 24;

    // The value column clears the longest label ("RESTORE FACTORY SETTINGS", 24 small-font glyphs
    // ≈ 240 px from LabelColumn), and the descriptive word column clears the longest value.
    private const int NoteColumn = 440;

    private const int SecondInstructionRow = 312;

    private const string Title = "GAME ADJUSTMENT";
    private const int TitleRow = 12;
    private const int ValueColumn = 340;

    /// <summary>
    ///     The two entries this page draws with STATICALLY, written on entry. The selected line's label
    ///     sits in <see cref="DefineInputsHighlight.PaletteIndex" />, which that process itself puts on its GREEN
    ///     (notes §115). Palette indices 10-15 are left alone: the in-game animator owns those.
    /// </summary>
    private static readonly int[] OwnedPaletteIndices = [InputPaletteIndex, HeadingPaletteIndex];

    private readonly GameSettings _gameSettings;
    private readonly GameSettingsStore _gameSettingsStore;
    private readonly DefineInputsHighlight _defineInputsHighlight = new();
    private readonly HighScoreStore _highScoreStore;
    private readonly SettingsModel _settingsModel = new();
    private readonly GameServices _gameServices;
    private readonly SpriteSet _spriteSet;
    private InputSnapshot _previousSnapshot;

    public SettingsState(GameServices gameServices, GameSettingsStore gameSettingsStore)
    {
        _gameServices = gameServices;
        _spriteSet = gameServices.SpriteSet;
        _gameSettings = gameServices.GameSettings;
        _highScoreStore = gameServices.HighScoreStore;
        _gameSettingsStore = gameSettingsStore;
        _previousSnapshot = InputSnapshot.Read();
        RestorePalette();

        if (_spriteSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Start(palette);
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawHeading(spriteBatch, Title, TitleRow);

        for (var line = 0; line < SettingsModel.LineCount; line++)
            DrawLine(spriteBatch, line, FirstLineRow + line * LineStep);

        DrawInstruction(spriteBatch, ChooseLine, InstructionsRow);
        DrawInstruction(spriteBatch, ActivateLine, SecondInstructionRow);
        DrawInstruction(spriteBatch, ExitLine, ExitRow);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (_spriteSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Update(palette);

        var now = InputSnapshot.Read();

        if (WasPressed(now, Keys.Up, Buttons.DPadUp)) _settingsModel.MoveUp();

        if (WasPressed(now, Keys.Down, Buttons.DPadDown)) _settingsModel.MoveDown();

        if (WasPressed(now, Keys.Left, Buttons.DPadLeft)) Change(-1);

        if (WasPressed(now, Keys.Right, Buttons.DPadRight)) Change(1);

        if (WasPressed(now, Keys.Enter, Buttons.A)) Activate();

        if (WasKeyboardPressed(now, Keys.F10))
        {
            _gameSettingsStore.Save(_gameSettings);
            gameStateManager.TransitionTo(new DeveloperToolsState(_gameServices, null, _gameSettingsStore));
        }

        _previousSnapshot = now;
    }

    /// <summary>Performs the highlighted action row, if it reads YES.</summary>
    private void Activate()
    {
        switch (_settingsModel.Activate(_gameSettings))
        {
            case SettingsAction.RestoreFactorySettings:
                _gameSettingsStore.Save(_gameSettings);
                break;

            case SettingsAction.ResetHighScores:
                _highScoreStore.Save(HighScoreTable.CreateWithFactoryScores());
                break;
        }
    }

    /// <summary>Steps the highlighted row, writing the change straight to the file.</summary>
    private void Change(int direction)
    {
        _settingsModel.Change(_gameSettings, direction);
        _gameSettingsStore.Save(_gameSettings);
    }

    /// <summary>
    ///     The cursor on the selected line: the arcade's own — the GAME ADJUSTMENT page prints the small
    ///     font's "->" glyph at column $0C on the row the move lever is on, in palette entry 9.
    /// </summary>
    private void DrawCursor(SpriteBatch spriteBatch, int y)
    {
        _spriteSet.BlitterDraw.DrawGlyphStatic(spriteBatch, _spriteSet.CursorArrowSprite, CursorColumn, y, HeadingPaletteIndex);
    }

    /// <summary>The heading: the arcade's LARGE font, centred, in the page's WHITE.</summary>
    private void DrawHeading(SpriteBatch spriteBatch, string text, int y)
    {
        _spriteSet.ArcadeText.DrawLargeFontText(spriteBatch, text, GetCenteredX(text, true), y, HeadingPaletteIndex);
    }

    /// <summary>An instruction line under the list: the SMALL font, centred, in the page's WHITE.</summary>
    private void DrawInstruction(SpriteBatch spriteBatch, string text, int y)
    {
        DrawText(spriteBatch, text, GetCenteredX(text), y, HeadingPaletteIndex);
    }

    /// <summary>
    ///     One line: the arcade's "->" if the cursor is on it, then its label and its value, then the
    ///     descriptive word (or the action row's prompt). The selected line's label is the page's one
    ///     cycling thing (notes §115); the value and the word stay on the page's static colours.
    /// </summary>
    private void DrawLine(SpriteBatch spriteBatch, int line, int y)
    {
        if (_settingsModel.Line == line) DrawCursor(spriteBatch, y);

        var labelPaletteIndex = InputPaletteIndex;
        if (_settingsModel.Line == line) labelPaletteIndex = DefineInputsHighlight.PaletteIndex;

        DrawText(spriteBatch, SettingsModel.GetLabel(line), LabelColumn, y, labelPaletteIndex);
        DrawText(spriteBatch, _settingsModel.GetValue(_gameSettings, line), ValueColumn, y, InputPaletteIndex);

        string note;
        if (SettingsModel.IsActionLine(line)) note = _settingsModel.GetActionHint(line);
        else note = SettingsModel.GetNote(_gameSettings, line);

        if (note.Length > 0) DrawText(spriteBatch, note, NoteColumn, y, HeadingPaletteIndex);
    }

    private int DrawText(SpriteBatch spriteBatch, string text, int x, int y, int paletteIndex)
    {
        return _spriteSet.ArcadeText.DrawSmallFontText(spriteBatch, text, x, y, paletteIndex);
    }

    /// <summary>The X that centres a line of the given font on the canvas.</summary>
    private int GetCenteredX(string text, bool isLarge = false)
    {
        var textWidth = _spriteSet.ArcadeText.MeasureSmallText(text);
        if (isLarge) textWidth = _spriteSet.ArcadeText.MeasureLargeText(text);

        var width = ScreenSize.ToPortPixelsFromArcadePixels(textWidth);
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>
    ///     Puts this page's two entries back on their CRTAB values (notes §108). They are what the page
    ///     draws with, and the screen it came from may have left them holding anything at all.
    /// </summary>
    private void RestorePalette()
    {
        if (_spriteSet.BlitterDraw.GamePalette is not { } palette) return;

        foreach (var paletteIndex in OwnedPaletteIndices) palette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);
    }

    private bool WasKeyboardPressed(InputSnapshot now, Keys key)
    {
        return now.KeyboardState.IsKeyDown(key) && !_previousSnapshot.KeyboardState.IsKeyDown(key);
    }

    private bool WasPadPressed(InputSnapshot now, Buttons button)
    {
        return (now.PlayerOneGamePadState.IsButtonDown(button) && !_previousSnapshot.PlayerOneGamePadState.IsButtonDown(button))
               || (now.PlayerTwoGamePadState.IsButtonDown(button) && !_previousSnapshot.PlayerTwoGamePadState.IsButtonDown(button));
    }

    /// <summary>True on the tick the key or the pad button (either pad) goes down.</summary>
    private bool WasPressed(InputSnapshot now, Keys key, Buttons button)
    {
        return WasKeyboardPressed(now, key) || WasPadPressed(now, button);
    }
}
