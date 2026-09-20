using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The DEFINE INPUTS page (notes §101) — port-only: the cabinet's two sticks are wired
/// to the board, so there is nothing here to be faithful to, only the author's request
/// for a page where each player can lay their own controls out.
///
/// One column of lines with player 2's eight beneath player 1's — with blank lines between
/// the sections — and eight of them on screen at a time: the cursor keys scroll between the
/// sections (the author's ask, after the first side-by-side version felt too full). Each line
/// is one of the arcade's two sticks — MOVE UP away through SHOOT RIGHT — and the shared
/// PAUSE line is last. Unselected names sit in the palette's WHITE; the highlighted line
/// cycles (the author kept that, and lost only the flashing of the unselected names).
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
    private const string Hint = "USE UP AND DOWN TO MOVE BETWEEN P1 AND P2";
    private const string FooterOne = "ENTER SET THE INPUT   DEL CLEAR";
    private const string FooterTwo = "R DEFAULTS   F10 TITLE";

    private const int TitleRow = 12;
    private const int HintRow = 44;
    private const int FirstLineRow = 70;
    private const int LineStep = 26;
    private const int LabelColumn = 40;
    private const int ValueColumn = 300;
    private const int FooterRow = 302;

    // $99 is the palette's WHITE and no colour process drives it, so an unselected line sits
    // still (the author: the flashing names were the thing to lose, the entered input's
    // flashing is the thing to keep). The highlight is the title's $CC, which cycles.
    private const int LabelSlot = 9;
    private const int HighlightSlot = GameplayConstants.TitleWallSlot;

    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly ControlSettingsStore _controlStore;
    private readonly GameServices _services;
    private readonly IPlayerInputSource _input;
    private readonly ControlSettings _settings;
    private readonly DefineInputsModel _model = new();
    private InputSnapshot _previous;

    public DefineInputsState(GameServices services, ControlSettingsStore controlStore)
    {
        _services = services;
        _sprites = services.Sprites;
        _highScores = services.HighScores;
        _input = services.Input;
        _settings = services.Controls;
        _controlStore = controlStore;
        _previous = InputSnapshot.Read();
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
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
        DrawText(spriteBatch, Title, CenteredX(Title), TitleRow, HighlightSlot);
        DrawText(spriteBatch, Hint, CenteredX(Hint), HintRow, LabelSlot);

        int last = Math.Min(_model.FirstVisibleLine + DefineInputsModel.VisibleLines, DefineInputsModel.LineCount);
        int row = 0;
        for (int line = _model.FirstVisibleLine; line < last; line++, row++)
        {
            DrawLine(spriteBatch, line, FirstLineRow + (row * LineStep));
        }

        DrawText(spriteBatch, FooterOne, CenteredX(FooterOne), FooterRow, LabelSlot);
        DrawText(spriteBatch, FooterTwo, CenteredX(FooterTwo), FooterRow + 16, LabelSlot);
    }

    /// <summary>One line: its label, and its value — or the armed prompt. Spacers stay blank.</summary>
    private void DrawLine(SpriteBatch spriteBatch, int line, int y)
    {
        if (DefineInputsModel.IsSpacer(line))
        {
            return;
        }

        bool cursor = _model.IsCursorOn(line);
        // The highlight hides itself while armed (IsCursorOn), so the armed line is
        // identified by its prompt rather than by its colour.
        bool armed = _model.IsArmed && _model.Line == line;
        int slot = cursor ? HighlightSlot : LabelSlot;

        DrawText(spriteBatch, LabelOf(line), LabelColumn, y, slot);
        DrawText(spriteBatch, ValueText(armed, ValueOf(line)), ValueColumn, y, slot);
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

    private string ValueOf(int line) => line == DefineInputsModel.PauseLine
        ? _settings.Pause.DisplayName
        : _settings[DefineInputsModel.PlayerOf(line)][DefineInputsModel.ActionOf(line)!.Value].DisplayName;

    /// <summary>The value column, or the armed prompt while the line is waiting for its input.</summary>
    private static string ValueText(bool armed, string value) => armed ? ArmedPrompt : value;

    private void DrawText(SpriteBatch spriteBatch, string text, int x, int y, int slot) =>
        _sprites.DrawSmallFontText(spriteBatch, text, x, y, slot);

    private static int CenteredX(string text) => (ScreenSize.Width - ScreenSize.Scaled((text.Length * 5) - 1)) / 2;

    private bool KeyboardPressed(InputSnapshot now, Keys key) =>
        now.Keys.IsKeyDown(key) && !_previous.Keys.IsKeyDown(key);

    private bool PadPressed(InputSnapshot now, Buttons button) =>
        (now.PadOne.IsButtonDown(button) && !_previous.PadOne.IsButtonDown(button))
        || (now.PadTwo.IsButtonDown(button) && !_previous.PadTwo.IsButtonDown(button));
}
