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
/// The end of a game: the arcade's own screen for it (notes §98.1). RRG23's
/// <c>PLEND</c> — the last man gone — clears a block at (60, 126), prints string
/// 40 (<c>GOMP</c> = "GAME OVER") in the LARGE font in colour <c>$AA</c> at
/// <c>CURSAB $3E,$80</c> = (62, 128), holds it for <c>NAP 120</c> = 2.4 s and then
/// runs <c>ENDPRC</c>, the high-score processing — with no key wait.
///
/// So this state no longer waits for fire and no longer draws its own XNA-font
/// text: it holds the ROM's message and hands over to <see cref="ScoreEntryCeremony"/>,
/// which offers every player's final score to the table in turn — through the CONG
/// initials screen when a score qualifies (notes §116) — and ends on the table itself,
/// with the entries just posted highlighted.
/// </summary>
public sealed class GameOverState : IGameState
{
    private readonly GameServices _services;
    private readonly IReadOnlyList<FinalScore> _scores;
    private readonly int _holdTicks = GameplayConstants.PortTicks(GameplayConstants.GameOverMessageRomFrames);
    private int _elapsedTicks;

    /// <summary>Builds the screen for a finished game's scores.</summary>
    /// <param name="input">Player 1's input, which the states that follow read.</param>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="highScores">The high score store the ceremony writes to.</param>
    /// <param name="scores">Every player's final score, highest first — the ROM's <c>EGSUB</c> runs once per player.</param>
    /// <param name="controls">The port's control definitions.</param>
    public GameOverState(
        IPlayerInputSource input,
        SpriteSet sprites,
        HighScoreStore highScores,
        IReadOnlyList<FinalScore> scores,
        ControlSettings? controls = null)
    {
        _services = new GameServices(sprites, highScores, controls ?? ControlSettings.Defaults(), input);
        _scores = scores;
    }

    /// <summary>Builds the screen from a session that has just ended.</summary>
    /// <param name="input">Player 1's input.</param>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="highScores">The high score store.</param>
    /// <param name="session">The finished game, which supplies the final scores and the controls.</param>
    public static GameOverState FromSession(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, GameSession session) =>
        new(input, sprites, highScores, session.FinalScoresHighestFirst(), session.Controls);

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (++_elapsedTicks < _holdTicks)
        {
            return; // NAP 120: the message holds for 2.4 s, and the ROM waits for nothing here
        }

        // ENDPRC → ENDGAM → GOV: the scores are offered to the table in turn, and the table itself is
        // the end of the ceremony (notes §98).
        manager.TransitionTo(new ScoreEntryCeremony(_services, _scores).NextScreen());
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // ROM string 40 (GOMP): "GAME OVER" in the LARGE font, colour $AA, at (62, 128).
        _services.Sprites.DrawLargeFontText(
            spriteBatch,
            "GAME OVER",
            GameplayConstants.ArcadeColumnX(GameplayConstants.GameOverTextColumn),
            GameplayConstants.ArcadeY(GameplayConstants.GameOverTextRow),
            GameplayConstants.GameOverTextSlot);
    }
}
