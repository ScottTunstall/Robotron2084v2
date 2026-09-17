using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// Game-over screen (spec): black background, "GAME OVER" in a large font,
/// pause at least 3 seconds, then the fire button returns to the title — or, when
/// a score qualifies for the top 10, to the initials-entry screen (Phase 11.7).
///
/// A 2-player game hands every player's score to the high-score table, highest
/// first (the arcade's high-score check examines both <c>ZP1SCR</c> and
/// <c>ZP2SCR</c> — RRTESTC), so this state walks the list and lets each
/// qualifying score be entered in turn.
/// </summary>
public sealed class GameOverState : IGameState
{
    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly List<int> _scores;
    private readonly TimeSpan _minPause = TimeSpan.FromSeconds(GameplayConstants.GameOverMinPauseSeconds);
    private TimeSpan _elapsed;
    private bool _previousFire;

    public GameOverState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, int score)
        : this(input, sprites, highScores, [score])
    {
    }

    /// <param name="scores">
    /// Every player's final score, highest first. Each one is offered to the
    /// top-10 table in turn.
    /// </param>
    public GameOverState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, IReadOnlyList<int> scores)
    {
        _input = input;
        _sprites = sprites;
        _highScores = highScores;
        _scores = [.. scores];
    }

    public static GameOverState FromSession(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, GameSession session) =>
        new(input, sprites, highScores, session.ScoresHighestFirst());

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        _elapsed += gameTime.ElapsedGameTime;
        if (_elapsed < _minPause)
        {
            return; // fire is ignored during the minimum pause (spec)
        }

        PlayerInputState input = _input.Poll();
        bool firePressed = input.FirePressed;

        if (firePressed && !_previousFire)
        {
            // Offer each score in turn; a player whose score does not qualify is
            // simply skipped (the arcade checks both scores against the table).
            while (_scores.Count > 0)
            {
                int score = _scores[0];
                _scores.RemoveAt(0);
                if (_highScores.QualifiesForTopTen(score, _highScores.Load()))
                {
                    manager.TransitionTo(new HighScoreEntryState(_input, _sprites, _highScores, score, _scores));
                    return;
                }
            }

            manager.TransitionTo(new TitleScreenState(_input, _sprites, _highScores));
        }

        _previousFire = firePressed;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        const float scale = 2.0f;
        spriteBatch.DrawString(
            font,
            "GAME OVER",
            new Vector2((ScreenSize.Width - font.MeasureString("GAME OVER").X * scale) / 2, ScreenSize.Scaled(80)),
            Color.White,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }
}
