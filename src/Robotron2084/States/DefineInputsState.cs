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
///     The DEFINE INPUTS page (notes §101) — port-only: the cabinet's two sticks are wired
///     to the board, so there is nothing here to be faithful to — the page is where each
///     player lays their own controls out.
///     Its LOOK is the arcade's, though (notes §108): it reads like the GAME
///     ADJUSTMENT page in the cabinet's service mode — a centred heading, a left column of setting
///     names with their values in a second column, that page's own "->" cursor at the left of the line
///     the cursor is on, and the instructions under the list. Headings and instructions are the palette's
///     WHITE; the lines' input text is its GREEN.
///     One column of lines with player 2's eight beneath player 1's — with blank lines between
///     the sections — and eight of them on screen at a time: the cursor keys scroll between the
///     sections. Each line
///     is one of the arcade's two sticks — MOVE UP away through SHOOT RIGHT — and the shared
///     PAUSE line is last.
///     Enter arms the highlighted line and the next thing pressed — key, gamepad button or
///     stick direction — becomes that line's binding for its own device, which is why
///     arming is a separate step: the cursor keys have to be bindable too.
///     Every change is written straight to <c>controls.ini</c>, so the definitions are there
///     the next time the game starts whatever happens next.
/// </summary>
public sealed class DefineInputsState : IGameState
{
    private const string ArmedPrompt = "PRESS AN INPUT";

    // The two columns are centred on the page (notes §102). The cursor sits just left of the label
    // column, exactly where the arcade's GAME ADJUSTMENT page prints its cursor (column $0C), and it
    // is that page's own cursor glyph — the small font's "->" (notes §108.4).
    private const int CursorColumn = 57;

    private const string ExitLine = "F10 - DEVELOPER TOOLS";
    private const int ExitRow = 342;
    private const int FirstLineRow = 70;

    /// <summary>It is one of the palette indices in <see cref="OwnedPaletteIndices" />.</summary>
    private const int HeadingPaletteIndex = 9;

    // The page's own colours (notes §108): the page reads like the arcade's GAME ADJUSTMENT page —
    // HEADINGS and INSTRUCTIONS in the palette's WHITE ($FF, palette index 9), the lines' input text in its
    // GREEN ($38, palette index 6), and the selected line marked by the arcade's own "->" cursor. The word OR
    // between a line's two devices keeps its own BLUE ($C0, palette index 7), so "W OR P1 LEFT STICK UP"
    // reads as two alternatives rather than as one long string (notes §101.12). Those are the
    // palette's plain CRTAB values, and they are the very entries the arcade's page uses: its
    // cursor string sets text colour $99 (entry 9) before printing the glyph and restores $66
    // (entry 6) after it (notes §108.4). The one thing on the page that COLOUR-CYCLES is the
    // selected line's label — strobed in its own palette index the way the intro pages cycle their text
    // (notes §115) — while the line's value, the bound key or joystick input, stays on the page's
    // static green.
    /// <summary>It is one of the palette indices in <see cref="OwnedPaletteIndices" />.</summary>
    private const int InputPaletteIndex = 6;

    private const string Instructions = "USE UP AND DOWN TO MOVE BETWEEN P1 AND P2";

    // The instructions and the exit line sit under the list in the SMALL font, the way the arcade's
    // adjustment page ends — the exit line on its own, a blank line below them.
    private const int InstructionsRow = 294;

    private const int LabelColumn = 75;
    private const int LineStep = 26;

    /// <summary>It is one of the palette indices in <see cref="OwnedPaletteIndices" />.</summary>
    private const int SeparatorPaletteIndex = 7;

    private const string SetAndClear = "ENTER - SET THE INPUT   DEL - CLEAR   R - DEFAULTS";
    private const int SetAndClearRow = 312;
    private const string Title = "DEFINE INPUTS";
    private const int TitleRow = 12;
    private const int ValueColumn = 285;

    /// <summary>
    ///     The three entries this page draws with STATICALLY. It writes them itself on entry (see
    ///     <see cref="RestorePalette" />) because no palette index can be assumed to hold its CRTAB value: the
    ///     screen it is opened FROM leaves its own colours up — the presentation page writes entries 1-7
    ///     (notes §106) and the high score table zeroes all sixteen (notes §98.6) — and F10 is handled by
    ///     the shell, so those pages never stand their colours down. The selected line's label sits in
    ///     <see cref="DefineInputsHighlight.PaletteIndex" />, which that process itself puts on its GREEN on entry
    ///     (notes §115). Palette indices 10-15 are left alone: the in-game animator owns those.
    /// </summary>
    private static readonly int[] OwnedPaletteIndices = [InputPaletteIndex, SeparatorPaletteIndex, HeadingPaletteIndex];

    private readonly ControlSettings _controlSettings;

    private readonly ControlSettingsStore _controlSettingsStore;
    private readonly DefineInputsHighlight _defineInputsHighlight = new();
    private readonly DefineInputsModel _defineInputsModel = new();
    private readonly GameServices _gameServices;
    private readonly SpriteSet _spriteSet;
    private InputSnapshot _previousSnapshot;

    public DefineInputsState(GameServices gameServices, ControlSettingsStore controlSettingsStore)
    {
        _gameServices = gameServices;
        _spriteSet = gameServices.SpriteSet;
        _controlSettings = gameServices.ControlSettings;
        _controlSettingsStore = controlSettingsStore;
        _previousSnapshot = InputSnapshot.Read();
        RestorePalette();

        if (_spriteSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Start(palette);
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawHeading(spriteBatch, Title, TitleRow);

        var last = Math.Min(_defineInputsModel.FirstVisibleLine + DefineInputsModel.VisibleLines, DefineInputsModel.LineCount);
        var row = 0;
        for (var line = _defineInputsModel.FirstVisibleLine; line < last; line++, row++)
            DrawLine(spriteBatch, line, FirstLineRow + row * LineStep);

        DrawInstruction(spriteBatch, Instructions, InstructionsRow);
        DrawInstruction(spriteBatch, SetAndClear, SetAndClearRow);
        DrawInstruction(spriteBatch, ExitLine, ExitRow);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (_spriteSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Update(palette);

        var now = InputSnapshot.Read();

        if (_defineInputsModel.IsArmed)
            CaptureInput(now);
        else
            Navigate(now, gameStateManager);

        _previousSnapshot = now;
    }

    /// <summary>
    ///     One line's LABEL — always with its player on it, so a window that leaves the tail of
    ///     one block and the head of the next on screen together can never be misread.
    /// </summary>
    private static string GetLabel(int line)
    {
        if (line == DefineInputsModel.PauseLine) return "PAUSE";

        return $"P{DefineInputsModel.PlayerOf(line) + 1} {DefineInputsModel.GetAction(line)!.Value.GetLabel()}";
    }

    /// <summary>The armed half of the page: the next input pressed becomes the binding.</summary>
    private void CaptureInput(InputSnapshot now)
    {
        if (WasKeyboardPressed(now, Keys.Back) || WasPadPressed(now, Buttons.B))
        {
            _defineInputsModel.CancelArm();
            return;
        }

        var captured = ControlCapture.GetNewlyPressed(_previousSnapshot, now);
        if (captured.Kind != InputBindingKind.None && _defineInputsModel.Assign(_controlSettings, captured))
            _controlSettingsStore.Save(_controlSettings);
    }

    /// <summary>
    ///     The cursor on the selected line: the arcade's own — the GAME ADJUSTMENT page prints the small
    ///     font's "->" glyph at column $0C on the row the move lever is on, in palette entry 9, and this
    ///     page puts the same glyph in the same relation to its rows (notes §108.4/§108.5). Entry 9 is
    ///     this page's WHITE — the arcade's `04 99` sets exactly that colour before printing the cursor.
    /// </summary>
    private void DrawCursor(SpriteBatch spriteBatch, int y)
    {
        _spriteSet.BlitterDraw.DrawGlyphStatic(spriteBatch, _spriteSet.CursorArrowSprite, CursorColumn, y, HeadingPaletteIndex);
    }

    /// <summary>
    ///     The heading: the arcade's LARGE font, centred, in the page's WHITE.
    /// </summary>
    private void DrawHeading(SpriteBatch spriteBatch, string text, int y)
    {
        _spriteSet.ArcadeText.DrawLargeFontText(spriteBatch, text, GetCenteredX(text, true), y, HeadingPaletteIndex);
    }

    /// <summary>
    ///     An instruction line under the list: the arcade's SMALL font, centred, in the page's WHITE
    ///     (the heading keeps the large font; these do not).
    /// </summary>
    private void DrawInstruction(SpriteBatch spriteBatch, string text, int y)
    {
        DrawText(spriteBatch, text, GetCenteredX(text), y, HeadingPaletteIndex);
    }

    /// <summary>
    ///     One line: the arcade's arrow if the cursor is on it, then its label and its value — or the
    ///     armed prompt. Spacers stay blank.
    /// </summary>
    private void DrawLine(SpriteBatch spriteBatch, int line, int y)
    {
        if (DefineInputsModel.IsSpacer(line)) return;

        if (_defineInputsModel.IsCursorOn(line)) DrawCursor(spriteBatch, y);

        // The arrow hides itself while armed (IsCursorOn): the next input pressed becomes the
        // binding rather than moving the cursor, so the armed line is identified by its prompt.
        var isArmed = _defineInputsModel.IsArmed && _defineInputsModel.Line == line;

        // The selected line's label is the page's one cycling thing (notes §115): it strobes in the
        // highlight's palette index — while armed as well — and the line's value stays on the page's green.
        var labelPaletteIndex = InputPaletteIndex;
        if (_defineInputsModel.Line == line) labelPaletteIndex = DefineInputsHighlight.PaletteIndex;

        DrawText(spriteBatch, GetLabel(line), LabelColumn, y, labelPaletteIndex);
        DrawValue(spriteBatch, line, isArmed, InputPaletteIndex, y);
    }

    private int DrawText(SpriteBatch spriteBatch, string text, int x, int y, int paletteIndex)
    {
        return _spriteSet.ArcadeText.DrawSmallFontText(spriteBatch, text, x, y, paletteIndex);
    }

    /// <summary>
    ///     The value column: the keyboard binding, then the word OR in its own colour when the line
    ///     has both devices, then the gamepad binding — or the armed prompt, or NONE.
    /// </summary>
    private void DrawValue(SpriteBatch spriteBatch, int line, bool isArmed, int paletteIndex, int y)
    {
        if (isArmed)
        {
            DrawText(spriteBatch, ArmedPrompt, ValueColumn, y, paletteIndex);
            return;
        }

        var binding = GetBinding(line);
        var hasKeyBinding = binding.KeyBinding.Kind != InputBindingKind.None;
        var hasPadBinding = binding.PadBinding.Kind != InputBindingKind.None;

        if (!hasKeyBinding && !hasPadBinding)
        {
            DrawText(spriteBatch, "NONE", ValueColumn, y, paletteIndex);
            return;
        }

        var x = ValueColumn;
        if (hasKeyBinding) x = DrawText(spriteBatch, binding.KeyBinding.GetDisplayName(), x, y, paletteIndex);

        if (hasKeyBinding && hasPadBinding) x = DrawText(spriteBatch, " OR ", x, y, SeparatorPaletteIndex);

        if (hasPadBinding) DrawText(spriteBatch, binding.PadBinding.GetDisplayName(), x, y, paletteIndex);
    }

    private ActionBinding GetBinding(int line)
    {
        if (line == DefineInputsModel.PauseLine) return new ActionBinding(_controlSettings.Pause, InputBinding.None);

        return _controlSettings[DefineInputsModel.PlayerOf(line)][DefineInputsModel.GetAction(line)!.Value];
    }

    /// <summary>The X that centres a line of the given font on the canvas.</summary>
    private int GetCenteredX(string text, bool isLarge = false)
    {
        var textWidth = _spriteSet.ArcadeText.MeasureSmallText(text);
        if (isLarge) textWidth = _spriteSet.ArcadeText.MeasureLargeText(text);

        var width = ScreenSize.ToPortPixelsFromArcadePixels(textWidth);
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>The idle half: scrolling, arming, clearing, defaults, and leaving.</summary>
    private void Navigate(InputSnapshot now, GameStateManager gameStateManager)
    {
        ScrollOrArm(now);

        if (WasKeyboardPressed(now, Keys.Delete) || WasPadPressed(now, Buttons.X))
        {
            _defineInputsModel.ClearHighlighted(_controlSettings);
            _controlSettingsStore.Save(_controlSettings);
        }

        if (WasKeyboardPressed(now, Keys.R))
        {
            _defineInputsModel.ResetAll(_controlSettings);
            _controlSettingsStore.Save(_controlSettings);
        }

        if (WasKeyboardPressed(now, Keys.F10))
        {
            _controlSettingsStore.Save(_controlSettings);
            gameStateManager.TransitionTo(new DeveloperToolsState(_gameServices, _controlSettingsStore));
        }
    }

    /// <summary>
    ///     Puts this page's three entries back on their CRTAB values (notes §108). They are what the page
    ///     draws with, and the screen it came from may have left them holding anything at all — see
    ///     <see cref="OwnedPaletteIndices" />.
    /// </summary>
    private void RestorePalette()
    {
        if (_spriteSet.BlitterDraw.GamePalette is not { } palette) return;

        foreach (var paletteIndex in OwnedPaletteIndices) palette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);
    }

    /// <summary>Moves the highlight up or down, or arms the highlighted row for a new binding.</summary>
    private void ScrollOrArm(InputSnapshot now)
    {
        if (WasKeyboardPressed(now, Keys.Up) || WasPadPressed(now, Buttons.DPadUp)) _defineInputsModel.MoveUp();

        if (WasKeyboardPressed(now, Keys.Down) || WasPadPressed(now, Buttons.DPadDown)) _defineInputsModel.MoveDown();

        if (WasKeyboardPressed(now, Keys.Enter) || WasPadPressed(now, Buttons.A)) _defineInputsModel.Arm();
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
}
