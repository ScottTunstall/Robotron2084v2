using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// Initials entry after a top-10 score (Phase 11.7). Reads the keyboard
/// directly — this screen needs raw key-press edges, not the 8-way
/// IPlayerInputSource abstraction: A–Z appends (max 3), Back removes the last
/// character, Enter/Space confirms (padding with 'A'), saves, and moves on to
/// the next pending score (a 2-player game can have two) or the title screen.
///
/// Note (deviation D-4): also carries the shared <see cref="SpriteSet"/> so
/// the TitleScreenState it transitions to can be constructed with the full
/// Appendix-B signature (input, sprites, highScores).
/// </summary>
public sealed class HighScoreEntryState : IGameState
{
    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly int _score;
    private readonly List<int> _pendingScores;
    private readonly StringBuilder _initials = new();
    private KeyboardState _previousKeyboard;
    private int _blinkTicks;

    public HighScoreEntryState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, int score)
        : this(input, sprites, highScores, score, [])
    {
    }

    /// <param name="pendingScores">Any other scores still to be offered (2-player game), highest first.</param>
    public HighScoreEntryState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, int score, IReadOnlyList<int> pendingScores)
    {
        _input = input;
        _sprites = sprites;
        _highScores = highScores;
        _score = score;
        _pendingScores = [.. pendingScores];
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        _blinkTicks++;
        KeyboardState state = Keyboard.GetState();
        KeyboardState previous = _previousKeyboard;
        _previousKeyboard = state;

        // Fresh keydowns only (edge-detected against the previous frame's state).
        foreach (Keys key in state.GetPressedKeys())
        {
            if (previous.IsKeyDown(key))
            {
                continue;
            }

            if (key >= Keys.A && key <= Keys.Z && _initials.Length < GameplayConstants.HighScoreInitialsLength)
            {
                _initials.Append((char)key);
            }
            else if (key == Keys.Back && _initials.Length > 0)
            {
                _initials.Remove(_initials.Length - 1, 1);
            }
            else if (key == Keys.Enter || key == Keys.Space)
            {
                Confirm(manager);
            }
        }
    }

    private void Confirm(GameStateManager manager)
    {
        while (_initials.Length < GameplayConstants.HighScoreInitialsLength)
        {
            _initials.Append('A');
        }

        List<HighScoreEntry> all = _highScores.Load().ToList();
        all.Add(new HighScoreEntry(_initials.ToString(), _score));
        _highScores.Save(all); // sorts descending + truncates to Capacity

        // A 2-player game can have a second qualifying score waiting.
        while (_pendingScores.Count > 0)
        {
            int next = _pendingScores[0];
            _pendingScores.RemoveAt(0);
            if (_highScores.QualifiesForTopTen(next, _highScores.Load()))
            {
                manager.TransitionTo(new HighScoreEntryState(_input, _sprites, _highScores, next, _pendingScores));
                return;
            }
        }

        manager.TransitionTo(new TitleScreenState(_input, _sprites, _highScores));
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        const float bigScale = 1.5f;
        const float smallScale = 1.0f;

        string title = "NEW HIGH SCORE";
        spriteBatch.DrawString(font, title, Centered(font.MeasureString(title), bigScale, ScreenSize.Scaled(40)), Color.White, 0f, Vector2.Zero, bigScale, SpriteEffects.None, 0f);

        string score = _score.ToString("D6");
        spriteBatch.DrawString(font, score, Centered(font.MeasureString(score), bigScale, ScreenSize.Scaled(60)), Color.LightGray, 0f, Vector2.Zero, bigScale, SpriteEffects.None, 0f);

        string prompt = "ENTER INITIALS";
        spriteBatch.DrawString(font, prompt, Centered(font.MeasureString(prompt), smallScale, ScreenSize.Scaled(90)), Color.LightGray, 0f, Vector2.Zero, smallScale, SpriteEffects.None, 0f);

        // The initials typed so far, with a blinking cursor block after them.
        string initials = _initials.ToString();
        Vector2 initialsPosition = Centered(font.MeasureString(initials), smallScale, ScreenSize.Scaled(105));
        if (initials.Length > 0)
        {
            spriteBatch.DrawString(font, initials, initialsPosition, Color.White, 0f, Vector2.Zero, smallScale, SpriteEffects.None, 0f);
        }

        if ((_blinkTicks / GameplayConstants.HighScoreCursorBlinkTicks) % 2 == 0)
        {
            float cursorX = initialsPosition.X + (initials.Length > 0 ? font.MeasureString(initials).X : 0f);
            spriteBatch.Draw(
                _sprites.WallPixel,
                new Rectangle((int)cursorX, (int)initialsPosition.Y, ScreenSize.Scaled(3), ScreenSize.Scaled(10)),
                Color.White);
        }
    }

    private static Vector2 Centered(Vector2 unscaledSize, float scale, float y) =>
        new((ScreenSize.Width - unscaledSize.X * scale) / 2, y);
}
