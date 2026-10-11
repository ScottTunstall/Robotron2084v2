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
///     The DEVELOPER TOOLS page — port-only, and the one page F10 opens from EVERY screen (notes §101).
///     Its LOOK is the arcade's service pages' (notes §108), the same as the GAME ADJUSTMENT and DEFINE
///     INPUTS pages: a centred heading in the LARGE font, the list of options in the SMALL font with the
///     arcade's own "->" cursor at the left of the line the cursor is on, and the instructions under the
///     list. WHITE for the heading, the instructions and the exit line, GREEN for the options, and the
///     highlighted option strobing in the page's own cycling slot (notes §115) exactly as the selected
///     line does on those two pages.
///     The options jump straight to the screens the attract cycle passes through in the order the cycle
///     shows them: the title page on its arcade pane (the ROBOTRON 2084 presentation page as the cabinet
///     prints it), the title page on the pane that carries the port's credit, the STORY movie, the
///     machine playing itself, the HIGH SCORE table, the DEFINE INPUTS page, and the GAME ADJUSTMENT
///     page with the gameplay settings (notes §131).
///     Keys: Up/Down (or the stick) moves the cursor, Enter (or A) chooses, and 1 to 7 choose a line
///     directly. F10 closes the page — back to the title, the one attract screen this page's options
///     do not already reach — which is why the shell's F10 switch leaves this page to the page itself.
/// </summary>
public sealed class DeveloperToolsState : IGameState
{
    private const string ChooseLine = "UP DOWN - SELECT AN OPTION   ENTER - CHOOSE IT";
    private const string DirectLine = "1 TO 7 - CHOOSE AN OPTION DIRECTLY";

    // The cursor sits just left of the label column, exactly where the arcade's GAME ADJUSTMENT page
    // prints its cursor (column $0C), and it is that page's own cursor glyph — the small font's "->".
    private const int CursorColumn = 57;

    private const string ExitLine = "F10 - CLOSE THIS PAGE";
    private const int ExitRow = 342;
    private const int FirstLineRow = 90;

    /// <summary>It is one of the palette slots in <see cref="OwnedSlots" />.</summary>
    private const int HeadingSlot = 9;

    // The page's own colours (notes §108) are the GAME ADJUSTMENT page's: WHITE (slot 9) for the
    // heading, the instructions and the exit line, GREEN (slot 6) for the options themselves. No slot
    // can be assumed to hold its CRTAB value — the page opens from any screen, and the screen it came
    // from may have left them holding anything — so the page writes the two itself on entry.
    /// <summary>It is one of the palette slots in <see cref="OwnedSlots" />.</summary>
    private const int OptionSlot = 6;

    private const int InstructionsRow = 294;
    private const int LabelColumn = 75;
    private const int LineStep = 26;
    private const int SecondInstructionRow = 312;

    private const string Title = "DEVELOPER TOOLS";
    private const int TitleRow = 12;

    /// <summary>
    ///     The two entries this page draws with STATICALLY, written on entry. The highlighted option's
    ///     label sits in <see cref="DefineInputsHighlight.Slot" />, which that process itself puts on
    ///     its GREEN (notes §115). Slots 10-15 are left alone: the in-game animator owns those.
    /// </summary>
    private static readonly int[] OwnedSlots = [OptionSlot, HeadingSlot];

    /// <summary>
    ///     The page's options in list order: the attract cycle's own screens in the order the cycle
    ///     shows them (notes §94/§95), then the two service pages — the port's control definitions
    ///     and the arcade's GAME ADJUSTMENT settings (notes §131).
    /// </summary>
    private static readonly string[] Options =
    [
        "TITLE PAGE 1",
        "TITLE PAGE 2",
        "STORY",
        "DEMO",
        "HIGH SCORES",
        "DEFINE INPUTS",
        "GAME ADJUSTMENT"
    ];

    /// <summary>The number keys that choose a line directly, in list order.</summary>
    private static readonly Keys[] DirectKeys =
    [
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7
    ];

    private readonly ControlSettingsStore _controlSettingsStore;
    private readonly DefineInputsHighlight _highlight = new();
    private readonly GameSettingsStore _gameSettingsStore;
    private readonly GameServices _services;
    private readonly SpriteSet _sprites;

    private int _line;
    private InputSnapshot _previousSnapshot;

    /// <summary>
    ///     Opens the page.
    /// </summary>
    /// <param name="services">The attract screens' shared dependencies.</param>
    /// <param name="controlSettingsStore">
    ///     The store the DEFINE INPUTS option hands to the definitions page itself. The shell passes its own;
    ///     a page opened from outside the shell (the sound test, the settings page) takes the default.
    /// </param>
    /// <param name="gameSettingsStore">
    ///     The store the GAME ADJUSTMENT option hands to that page (notes §131), on the same
    ///     terms as <paramref name="controlSettingsStore" />.
    /// </param>
    public DeveloperToolsState(GameServices services, ControlSettingsStore? controlSettingsStore = null,
        GameSettingsStore? gameSettingsStore = null)
    {
        _services = services;
        _sprites = services.Sprites;
        _controlSettingsStore = controlSettingsStore ?? new ControlSettingsStore();
        _gameSettingsStore = gameSettingsStore ?? new GameSettingsStore();
        _previousSnapshot = InputSnapshot.Read();
        RestorePalette();

        if (_sprites.Blitter.Palette is { } palette) _highlight.Start(palette);
    }

    /// <summary>The option the cursor is on.</summary>
    internal int Line => _line;

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawHeading(spriteBatch, Title, TitleRow);

        for (var line = 0; line < Options.Length; line++)
            DrawOption(spriteBatch, line, FirstLineRow + line * LineStep);

        DrawInstruction(spriteBatch, ChooseLine, InstructionsRow);
        DrawInstruction(spriteBatch, DirectLine, SecondInstructionRow);
        DrawInstruction(spriteBatch, ExitLine, ExitRow);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (_sprites.Blitter.Palette is { } palette) _highlight.Update(palette);

        var now = InputSnapshot.Read();

        if (WasPressed(now, Keys.Up, Buttons.DPadUp)) _line = Move(_line, -1);

        if (WasPressed(now, Keys.Down, Buttons.DPadDown)) _line = Move(_line, 1);

        var chosen = WasPressed(now, Keys.Enter, Buttons.A);
        for (var key = 0; key < DirectKeys.Length; key++)
        {
            if (!WasKeyboardPressed(now, DirectKeys[key])) continue;

            _line = key;
            chosen = true;
        }

        if (WasKeyboardPressed(now, Keys.F10))
        {
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }

        if (chosen) Choose(_line, manager);

        _previousSnapshot = now;
    }

    /// <summary>Leaves the page for the screen the highlighted option names.</summary>
    private void Choose(int line, GameStateManager manager)
    {
        switch (line)
        {
            case 0:
                // The title page as the cabinet prints it: its own pane, held open (notes §107).
                manager.TransitionTo(new TitleScreenState(_services, TitleTextPane.Arcade));
                break;
            case 1:
                // The title page on the pane that carries the port's credit.
                manager.TransitionTo(new TitleScreenState(_services, TitleTextPane.Port));
                break;
            case 2:
                manager.TransitionTo(new StorylineState(_services, new Random()));
                break;
            case 3:
                manager.TransitionTo(new AttractState(_services));
                break;
            case 4:
                manager.TransitionTo(new HighScoreTableState(_services));
                break;
            case 5:
                manager.TransitionTo(new DefineInputsState(_services, _controlSettingsStore));
                break;
            case 6:
                // The arcade's service-mode settings: the gameplay adjustments (notes §131).
                manager.TransitionTo(new SettingsState(_services, _gameSettingsStore));
                break;
        }
    }

    /// <summary>Moves the cursor, wrapping at either end of the list.</summary>
    private static int Move(int line, int step)
    {
        var count = Options.Length;
        return (line % count + count + step) % count;
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
    ///     One option: the arcade's "->" if the cursor is on it, then its words. The highlighted option
    ///     is the page's one cycling thing (notes §115); the rest stay on the page's static GREEN.
    /// </summary>
    private void DrawOption(SpriteBatch spriteBatch, int line, int y)
    {
        if (_line == line) DrawCursor(spriteBatch, y);

        var slot = _line == line ? DefineInputsHighlight.Slot : OptionSlot;
        DrawText(spriteBatch, Options[line], LabelColumn, y, slot);
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
