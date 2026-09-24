using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Rendering;

namespace Robotron2084.States;

/// <summary>
/// The DEFINE INPUTS page (notes §101) — port-only: the cabinet's two sticks are wired
/// to the board, so there is nothing here to be faithful to, only the author's request
/// for a page where each player can lay their own controls out.
///
/// Its LOOK is the arcade's, though (author, 2026-09-20; notes §108): it reads like the GAME
/// ADJUSTMENT page in the cabinet's service mode — a centred heading, a left column of setting
/// names with their values in a second column, that page's own "->" cursor at the left of the line
/// the cursor is on, and the instructions under the list. Headings and instructions are the palette's
/// WHITE; the lines' input text is its GREEN.
///
/// One column of lines with player 2's eight beneath player 1's — with blank lines between
/// the sections — and eight of them on screen at a time: the cursor keys scroll between the
/// sections (the author's ask, after the first side-by-side version felt too full). Each line
/// is one of the arcade's two sticks — MOVE UP away through SHOOT RIGHT — and the shared
/// PAUSE line is last.
///
/// Enter arms the highlighted line and the next thing pressed — key, gamepad button or
/// stick direction — becomes that line's binding for its own device, which is why
/// arming is a separate step: the cursor keys have to be bindable too.
///
/// Every change is written straight to <c>controls.ini</c>, so the definitions are there
/// the next time the game starts whatever happens next.
/// </summary>
public sealed class DefineInputsState : IGameState
{
    private const string Title = "DEFINE INPUTS";
    private const string ArmedPrompt = "PRESS AN INPUT";
    private const string Instructions = "USE UP AND DOWN TO MOVE BETWEEN P1 AND P2";
    private const string SetAndClear = "ENTER - SET THE INPUT   DEL - CLEAR   R - DEFAULTS";
    private const string ExitLine = "F10 - TITLE";

    private const int TitleRow = 12;
    private const int FirstLineRow = 70;
    private const int LineStep = 26;

    // The two columns sit 3-4 characters right of where they started: the author found the rows
    // reading left of centre on the page (notes §102). The cursor sits just left of the label
    // column, exactly where the arcade's GAME ADJUSTMENT page prints its cursor (column $0C), and it
    // is that page's own cursor glyph — the small font's "->" (author, 2026-09-20: *"the arrow that
    // shows what row you're on is pointing the wrong way"*; notes §108.4).
    private const int CursorColumn = 57;
    private const int LabelColumn = 75;
    private const int ValueColumn = 285;

    // The instructions and the exit line sit under the list in the SMALL font, the way the arcade's
    // adjustment page ends — the exit line on its own, a blank line below them (author, 2026-09-20).
    private const int InstructionsRow = 294;
    private const int SetAndClearRow = 312;
    private const int ExitRow = 342;

    // The author's restyle (notes §108): the page reads like the arcade's GAME ADJUSTMENT page —
    // HEADINGS and INSTRUCTIONS in the palette's WHITE ($FF, slot 9), the lines' input text in its
    // GREEN ($38, slot 6), and the selected line marked by the arcade's own "->" cursor. The word OR
    // between a line's two devices keeps its own BLUE ($C0, slot 7), so "W OR P1 LEFT STICK UP"
    // reads as two alternatives rather than as one long string (slot 5's yellow was tried first and
    // rejected as too bright, slot 4's amber as the wrong hue — notes §101.12). Those are the
    // palette's plain CRTAB values, and they are the very entries the arcade's page uses: its
    // cursor string sets text colour $99 (entry 9) before printing the glyph and restores $66
    // (entry 6) after it (notes §108.4). The one thing on the page that COLOUR-CYCLES is the
    // selected line's label — strobed in its own slot the way the intro pages cycle their text
    // (notes §115) — while the line's value, the bound key or joystick input, stays on the page's
    // static green.
    private const int InputSlot = 6;
    private const int SeparatorSlot = 7;
    private const int HeadingSlot = 9;

    /// <summary>
    /// The three entries this page draws with STATICALLY. It writes them itself on entry (see
    /// <see cref="RestorePalette"/>) because no slot can be assumed to hold its CRTAB value: the
    /// screen it is opened FROM leaves its own colours up — the presentation page writes entries 1-7
    /// (notes §106) and the high score table zeroes all sixteen (notes §98.6) — and F10 is handled by
    /// the shell, so those pages never stand their colours down. The selected line's label sits in
    /// <see cref="DefineInputsHighlight.Slot"/>, which that process itself puts on its GREEN on entry
    /// (notes §115). Slots 10-15 are left alone: the in-game animator owns those.
    /// </summary>
    private static readonly int[] OwnedSlots = [InputSlot, SeparatorSlot, HeadingSlot];

    private readonly SpriteSet _sprites;
    private readonly ControlSettingsStore _controlStore;
    private readonly GameServices _services;
    private readonly ControlSettings _settings;
    private readonly DefineInputsModel _model = new();
    private readonly DefineInputsHighlight _highlight = new();
    private InputSnapshot _previous;

    public DefineInputsState(GameServices services, ControlSettingsStore controlStore)
    {
        _services = services;
        _sprites = services.Sprites;
        _settings = services.Controls;
        _controlStore = controlStore;
        _previous = InputSnapshot.Read();
        RestorePalette();

        if (_sprites.Palette is { } palette)
        {
            _highlight.Start(palette);
        }
    }

    /// <summary>
    /// Puts this page's three entries back on their CRTAB values (notes §108). They are what the page
    /// draws with, and the screen it came from may have left them holding anything at all — see
    /// <see cref="OwnedSlots"/>.
    /// </summary>
    private void RestorePalette()
    {
        if (_sprites.Palette is not { } palette)
        {
            return;
        }

        foreach (int slot in OwnedSlots)
        {
            palette.SetSlot(slot, GamePalette.DefaultSlots[slot]);
        }
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (_sprites.Palette is { } palette)
        {
            _highlight.Update(palette);
        }

        InputSnapshot now = InputSnapshot.Read();

        if (_model.IsArmed)
        {
            CaptureInput(now);
        }
        else
        {
            Navigate(now, manager);
        }

        _previous = now;
    }

    /// <summary>The armed half of the page: the next input pressed becomes the binding.</summary>
    private void CaptureInput(InputSnapshot now)
    {
        if (KeyboardPressed(now, Keys.Back) || PadPressed(now, Buttons.B))
        {
            _model.CancelArm();
            return;
        }

        InputBinding captured = ControlCapture.NewlyPressed(_previous, now);
        if (captured.Kind != InputBindingKind.None && _model.Assign(_settings, captured))
        {
            _controlStore.Save(_settings);
        }
    }

    /// <summary>The idle half: scrolling, arming, clearing, defaults, and leaving.</summary>
    private void Navigate(InputSnapshot now, GameStateManager manager)
    {
        if (KeyboardPressed(now, Keys.Up) || PadPressed(now, Buttons.DPadUp))
        {
            _model.MoveUp();
        }

        if (KeyboardPressed(now, Keys.Down) || PadPressed(now, Buttons.DPadDown))
        {
            _model.MoveDown();
        }

        if (KeyboardPressed(now, Keys.Enter) || PadPressed(now, Buttons.A))
        {
            _model.Arm();
        }

        if (KeyboardPressed(now, Keys.Delete) || PadPressed(now, Buttons.X))
        {
            _model.ClearHighlighted(_settings);
            _controlStore.Save(_settings);
        }

        if (KeyboardPressed(now, Keys.R))
        {
            _model.ResetAll(_settings);
            _controlStore.Save(_settings);
        }

        if (KeyboardPressed(now, Keys.F10))
        {
            _controlStore.Save(_settings);
            manager.TransitionTo(new TitleScreenState(_services));
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawHeading(spriteBatch, Title, TitleRow);

        int last = Math.Min(_model.FirstVisibleLine + DefineInputsModel.VisibleLines, DefineInputsModel.LineCount);
        int row = 0;
        for (int line = _model.FirstVisibleLine; line < last; line++, row++)
        {
            DrawLine(spriteBatch, line, FirstLineRow + (row * LineStep));
        }

        DrawInstruction(spriteBatch, Instructions, InstructionsRow);
        DrawInstruction(spriteBatch, SetAndClear, SetAndClearRow);
        DrawInstruction(spriteBatch, ExitLine, ExitRow);
    }

    /// <summary>
    /// The heading: the arcade's LARGE font, centred, in the page's WHITE.
    /// </summary>
    private void DrawHeading(SpriteBatch spriteBatch, string text, int y) =>
        _sprites.DrawLargeFontText(spriteBatch, text, CenteredX(text, large: true), y, HeadingSlot);

    /// <summary>
    /// An instruction line under the list: the arcade's SMALL font, centred, in the page's WHITE —
    /// the author's ask of 2026-09-20 (the heading keeps the large font; these do not).
    /// </summary>
    private void DrawInstruction(SpriteBatch spriteBatch, string text, int y) =>
        DrawText(spriteBatch, text, CenteredX(text), y, HeadingSlot);

    /// <summary>
    /// One line: the arcade's arrow if the cursor is on it, then its label and its value — or the
    /// armed prompt. Spacers stay blank.
    /// </summary>
    private void DrawLine(SpriteBatch spriteBatch, int line, int y)
    {
        if (DefineInputsModel.IsSpacer(line))
        {
            return;
        }

        if (_model.IsCursorOn(line))
        {
            DrawCursor(spriteBatch, y);
        }

        // The arrow hides itself while armed (IsCursorOn): the next input pressed becomes the
        // binding rather than moving the cursor, so the armed line is identified by its prompt.
        bool armed = _model.IsArmed && _model.Line == line;

        // The selected line's label is the page's one cycling thing (notes §115): it strobes in the
        // highlight's slot — while armed as well — and the line's value stays on the page's green.
        int labelSlot = _model.Line == line ? DefineInputsHighlight.Slot : InputSlot;
        DrawText(spriteBatch, LabelOf(line), LabelColumn, y, labelSlot);
        DrawValue(spriteBatch, line, armed, InputSlot, y);
    }

    /// <summary>
    /// The cursor on the selected line: the arcade's own — the GAME ADJUSTMENT page prints the small
    /// font's "->" glyph at column $0C on the row the move lever is on, in palette entry 9, and this
    /// page puts the same glyph in the same relation to its rows (notes §108.4/§108.5). Entry 9 is
    /// this page's WHITE — the arcade's `04 99` sets exactly that colour before printing the cursor.
    /// </summary>
    private void DrawCursor(SpriteBatch spriteBatch, int y) =>
        _sprites.DrawGlyphStatic(spriteBatch, _sprites.CursorArrow, CursorColumn, y, HeadingSlot);

    /// <summary>
    /// The value column: the keyboard binding, then the word OR in its own colour when the line
    /// has both devices, then the gamepad binding — or the armed prompt, or NONE.
    /// </summary>
    private void DrawValue(SpriteBatch spriteBatch, int line, bool armed, int slot, int y)
    {
        if (armed)
        {
            DrawText(spriteBatch, ArmedPrompt, ValueColumn, y, slot);
            return;
        }

        ActionBinding binding = BindingOf(line);
        bool key = binding.Key.Kind != InputBindingKind.None;
        bool pad = binding.Pad.Kind != InputBindingKind.None;

        if (!key && !pad)
        {
            DrawText(spriteBatch, "NONE", ValueColumn, y, slot);
            return;
        }

        int x = ValueColumn;
        if (key)
        {
            x = DrawText(spriteBatch, binding.Key.DisplayName, x, y, slot);
        }

        if (key && pad)
        {
            x = DrawText(spriteBatch, " OR ", x, y, SeparatorSlot);
        }

        if (pad)
        {
            DrawText(spriteBatch, binding.Pad.DisplayName, x, y, slot);
        }
    }

    /// <summary>
    /// One line's LABEL — always with its player on it, so a window that leaves the tail of
    /// one block and the head of the next on screen together can never be misread (the
    /// author: *"there's no separation between player 1's controls and player 2's"*).
    /// </summary>
    private static string LabelOf(int line)
    {
        if (line == DefineInputsModel.PauseLine)
        {
            return "PAUSE";
        }

        return $"P{DefineInputsModel.PlayerOf(line) + 1} {DefineInputsModel.ActionOf(line)!.Value.Label()}";
    }

    private ActionBinding BindingOf(int line) => line == DefineInputsModel.PauseLine
        ? new ActionBinding(_settings.Pause, InputBinding.None)
        : _settings[DefineInputsModel.PlayerOf(line)][DefineInputsModel.ActionOf(line)!.Value];

    private int DrawText(SpriteBatch spriteBatch, string text, int x, int y, int slot) =>
        _sprites.DrawSmallFontText(spriteBatch, text, x, y, slot);

    /// <summary>The X that centres a line of the given font on the canvas.</summary>
    private int CenteredX(string text, bool large = false)
    {
        int width = ScreenSize.Scaled(large ? _sprites.MeasureLargeText(text) : _sprites.MeasureSmallText(text));
        return (ScreenSize.Width - width) / 2;
    }

    private bool KeyboardPressed(InputSnapshot now, Keys key) =>
        now.Keys.IsKeyDown(key) && !_previous.Keys.IsKeyDown(key);

    private bool PadPressed(InputSnapshot now, Buttons button) =>
        (now.PadOne.IsButtonDown(button) && !_previous.PadOne.IsButtonDown(button))
        || (now.PadTwo.IsButtonDown(button) && !_previous.PadTwo.IsButtonDown(button));
}
