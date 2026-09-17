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
/// Title screen (spec): black background, "ScottOTron 2084" in a large font,
/// and a blinking "Press 1 or 2 to start" prompt. **1** starts a one-player
/// game and **2** a two-player game (the arcade's START 1 / START 2 buttons,
/// ROM RRG23 START1/START2); fire is kept as a one-player alias for the
/// port's existing muscle memory. Phase 11.7: every 5 seconds the prompt is
/// swapped for the saved top-10 high-score list.
/// </summary>
public sealed class TitleScreenState : IGameState
{
    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly TimeSpan _blinkDuration = TimeSpan.FromSeconds(GameplayConstants.TitleBlinkIntervalSeconds);
    private readonly TimeSpan _cycleDuration = TimeSpan.FromSeconds(GameplayConstants.TitleHighScoreCycleSeconds);
    private TimeSpan _blinkElapsed;
    private TimeSpan _cycleElapsed;
    private bool _showPrompt = true;
    private bool _showHighScores;
    private bool _previousFire;
    private bool _previousStartOne;
    private bool _previousStartTwo;
    private IReadOnlyList<HighScoreEntry> _cachedEntries = Array.Empty<HighScoreEntry>();

    public TitleScreenState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores)
    {
        _input = input;
        _sprites = sprites;
        _highScores = highScores;
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        PlayerInputState input = _input.Poll();

        // The arcade's coin-door buttons: START 1 / START 2 pick the number of
        // players (ROM RRG23 START1/START2 → PLRCNT), and the ROM then runs the
        // alternating 2-player game. Fire is kept as a 1-player alias (a port
        // convention — spec.txt's "press fire", which the arcade does not have).
        int playerCount = 0;
        if (input.StartOnePlayerPressed && !_previousStartOne)
        {
            playerCount = 1;
        }
        else if (input.StartTwoPlayersPressed && !_previousStartTwo)
        {
            playerCount = 2;
        }
        else if (input.FirePressed && !_previousFire)
        {
            playerCount = 1;
        }

        if (playerCount > 0)
        {
            manager.TransitionTo(new PlayingState(_sprites, _highScores, GameSession.NewGame(_input, playerCount)));
            return;
        }

        _previousFire = input.FirePressed;
        _previousStartOne = input.StartOnePlayerPressed;
        _previousStartTwo = input.StartTwoPlayersPressed;

        // Blink the prompt every second (spec).
        _blinkElapsed += gameTime.ElapsedGameTime;
        while (_blinkElapsed >= _blinkDuration)
        {
            _blinkElapsed -= _blinkDuration;
            _showPrompt = !_showPrompt;
        }

        // Every N seconds: swap the prompt for the saved top-10 list (Phase 11.7).
        _cycleElapsed += gameTime.ElapsedGameTime;
        while (_cycleElapsed >= _cycleDuration)
        {
            _cycleElapsed -= _cycleDuration;
            _showHighScores = !_showHighScores;
            if (_showHighScores)
            {
                _cachedEntries = _highScores.Load();
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The caller clears to black; this state only draws its content (spec).
        const float logoScale = 2.0f;
        spriteBatch.DrawString(
            font,
            "ScottOTron 2084",
            CenteredHorizontal(font.MeasureString("ScottOTron 2084"), logoScale, ScreenSize.Scaled(40)),
            Color.White,
            0f,
            Vector2.Zero,
            logoScale,
            SpriteEffects.None,
            0f);

        if (_showHighScores)
        {
            DrawTopTen(spriteBatch, font);
            return;
        }

        if (_showPrompt)
        {
            const float promptScale = 1.0f;
            spriteBatch.DrawString(
                font,
                "Press 1 or 2 to start",
                CenteredHorizontal(font.MeasureString("Press 1 or 2 to start"), promptScale, ScreenSize.Scaled(110)),
                Color.LightGray,
                0f,
                Vector2.Zero,
                promptScale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawTopTen(SpriteBatch spriteBatch, SpriteFont font)
    {
        const float scale = 0.75f;
        string header = "HIGH SCORES";
        spriteBatch.DrawString(font, header, CenteredHorizontal(font.MeasureString(header), scale, ScreenSize.Scaled(60)), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        if (_cachedEntries.Count == 0)
        {
            string empty = "no scores yet";
            spriteBatch.DrawString(font, empty, CenteredHorizontal(font.MeasureString(empty), scale, ScreenSize.Scaled(75)), Color.LightGray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        float y = ScreenSize.Scaled(75);
        for (int i = 0; i < _cachedEntries.Count; i++)
        {
            string line = $"{i + 1,2}   {_cachedEntries[i].Initials}   {_cachedEntries[i].Score:D6}";
            spriteBatch.DrawString(font, line, CenteredHorizontal(font.MeasureString(line), scale, y), Color.LightGray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            y += ScreenSize.Scaled(12);
        }
    }

    private static Vector2 CenteredHorizontal(Vector2 unscaledSize, float scale, float y) =>
        new((ScreenSize.Width - unscaledSize.X * scale) / 2, y);
}
