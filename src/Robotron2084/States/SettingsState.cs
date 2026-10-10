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
///     the attract screens and closed with F10.
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

    private const string ExitLine = "F10 - TITLE";
    private const int ExitRow = 342;
    private const int FirstLineRow = 76;

    /// <summary>It is one of the palette slots in <see cref="OwnedSlots" />.</summary>
    private const int HeadingSlot = 9;

    // The page's own colours (notes §108): headings, instructions and the words under each value in
    // the palette's WHITE ($FF, slot 9), the settings' own text in its GREEN ($38, slot 6). They are
    // the entries the arcade's page uses (its text colour $66 is slot 6). No slot can be assumed to
    // hold its CRTAB value — the page is opened from an attract screen, which leaves its own colours
    // up — so the page writes the two itself on entry (see OwnedSlots).
    /// <summary>It is one of the palette slots in <see cref="OwnedSlots" />.</summary>
    private const int InputSlot = 6;

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
    ///     sits in <see cref="DefineInputsHighlight.Slot" />, which that process itself puts on its GREEN
    ///     (notes §115). Slots 10-15 are left alone: the in-game animator owns those.
    /// </summary>
    private static readonly int[] OwnedSlots = [InputSlot, HeadingSlot];

    private readonly GameSettings _gameSettings;
    private readonly GameSettingsStore _gameSettingsStore;
    private readonly HighScoreStore _highScoreStore;

    private readonly DefineInputsHighlight _highlight = new();
    private readonly SettingsModel _model = new();
    private readonly GameServices _services;
    private readonly SpriteSet _sprites;
    private InputSnapshot _previousSnapshot;

    public SettingsState(GameServices services, GameSettingsStore settingsStore)
    {
        _services = services;
        _sprites = services.Sprites;
        _gameSettings = services.GameSettings;
        _highScoreStore = services.HighScoreStore;
        _gameSettingsStore = settingsStore;
        _previousSnapshot = InputSnapshot.Read();
        RestorePalette();

        if (_sprites.Blitter.Palette is { } palette) _highlight.Start(palette);
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

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (_sprites.Blitter.Palette is { } palette) _highlight.Update(palette);

        var now = InputSnapshot.Read();

        if (WasPressed(now, Keys.Up, Buttons.DPadUp)) _model.MoveUp();

        if (WasPressed(now, Keys.Down, Buttons.DPadDown)) _model.MoveDown();

        if (WasPressed(now, Keys.Left, Buttons.DPadLeft)) Change(-1);

        if (WasPressed(now, Keys.Right, Buttons.DPadRight)) Change(1);

        if (WasPressed(now, Keys.Enter, Buttons.A)) Activate();

        if (WasKeyboardPressed(now, Keys.F10))
        {
            _gameSettingsStore.Save(_gameSettings);
            manager.TransitionTo(new TitleScreenState(_services));
        }

        _previousSnapshot = now;
    }

    /// <summary>The X that centres a line of the given font on the canvas.</summary>
    private int GetCenteredX(string text, bool isLarge = false)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(isLarge
            ? _sprites.TextRenderer.MeasureLargeText(text)
            : _sprites.TextRenderer.MeasureSmallText(text));
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>
    ///     The cursor on the selected line: the arcade's own — the GAME ADJUSTMENT page prints the small
    ///     font's "->" glyph at column $0C on the row the move lever is on, in palette entry 9.
    /// </summary>
    private void DrawCursor(SpriteBatch spriteBatch, int y)
    {
        _sprites.Blitter.DrawGlyphStatic(spriteBatch, _sprites.CursorArrowSprite, CursorColumn, y, HeadingSlot);
    }

    /// <summary>The heading: the arcade's LARGE font, centred, in the page's WHITE.</summary>
    private void DrawHeading(SpriteBatch spriteBatch, string text, int y)
    {
        _sprites.TextRenderer.DrawLargeFontText(spriteBatch, text, GetCenteredX(text, true), y, HeadingSlot);
    }

    /// <summary>An instruction line under the list: the SMALL font, centred, in the page's WHITE.</summary>
    private void DrawInstruction(SpriteBatch spriteBatch, string text, int y)
    {
        DrawText(spriteBatch, text, GetCenteredX(text), y, HeadingSlot);
    }

    /// <summary>
    ///     One line: the arcade's "->" if the cursor is on it, then its label and its value, then the
    ///     descriptive word (or the action row's prompt). The selected line's label is the page's one
    ///     cycling thing (notes §115); the value and the word stay on the page's static colours.
    /// </summary>
    private void DrawLine(SpriteBatch spriteBatch, int line, int y)
    {
        if (_model.Line == line) DrawCursor(spriteBatch, y);

        var labelSlot = _model.Line == line ? DefineInputsHighlight.Slot : InputSlot;
        DrawText(spriteBatch, SettingsModel.GetLabel(line), LabelColumn, y, labelSlot);
        DrawText(spriteBatch, _model.GetValue(_gameSettings, line), ValueColumn, y, InputSlot);

        var note = SettingsModel.IsActionLine(line)
            ? _model.GetActionHint(line)
            : SettingsModel.GetNote(_gameSettings, line);
        if (note.Length > 0) DrawText(spriteBatch, note, NoteColumn, y, HeadingSlot);
    }

    private int DrawText(SpriteBatch spriteBatch, string text, int x, int y, int slot)
    {
        return _sprites.TextRenderer.DrawSmallFontText(spriteBatch, text, x, y, slot);
    }

    private bool WasKeyboardPressed(InputSnapshot now, Keys key)
    {
        return now.Keys.IsKeyDown(key) && !_previousSnapshot.Keys.IsKeyDown(key);
    }

    /// <summary>True on the tick the key or the pad button (either pad) goes down.</summary>
    private bool WasPressed(InputSnapshot now, Keys key, Buttons button)
    {
        return WasKeyboardPressed(now, key) || WasPadPressed(now, button);
    }

    private bool WasPadPressed(InputSnapshot now, Buttons button)
    {
        return (now.PadOne.IsButtonDown(button) && !_previousSnapshot.PadOne.IsButtonDown(button))
               || (now.PadTwo.IsButtonDown(button) && !_previousSnapshot.PadTwo.IsButtonDown(button));
    }

    /// <summary>Steps the highlighted row, writing the change straight to the file.</summary>
    private void Change(int direction)
    {
        _model.Change(_gameSettings, direction);
        _gameSettingsStore.Save(_gameSettings);
    }

    /// <summary>Performs the highlighted action row, if it reads YES.</summary>
    private void Activate()
    {
        switch (_model.Activate(_gameSettings))
        {
            case SettingsAction.RestoreFactorySettings:
                _gameSettingsStore.Save(_gameSettings);
                break;
            case SettingsAction.ResetHighScores:
                _highScoreStore.Save(HighScoreTable.CreateWithFactoryScores());
                break;
        }
    }

    /// <summary>
    ///     Puts this page's two entries back on their CRTAB values (notes §108). They are what the page
    ///     draws with, and the screen it came from may have left them holding anything at all.
    /// </summary>
    private void RestorePalette()
    {
        if (_sprites.Blitter.Palette is not { } palette) return;

        foreach (var slot in OwnedSlots) palette.SetSlot(slot, GamePalette.DefaultSlotValues[slot]);
    }
}
