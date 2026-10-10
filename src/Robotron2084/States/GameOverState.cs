using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
///     The end of a game: the arcade's own screen for it (notes §98.1). RRG23's
///     <c>PLEND</c> — the last man gone — clears a block at (60, 126), prints string
///     40 (<c>GOMP</c> = "GAME OVER") in the LARGE font in colour <c>$AA</c> at
///     <c>CURSAB $3E,$80</c> = (62, 128), holds it for <c>NAP 120</c> = 2.4 s and then
///     runs <c>ENDPRC</c>, the high-score processing — with no key wait.
///     So this state no longer waits for fire and no longer draws its own XNA-font
///     text: it holds the ROM's message and hands over to <see cref="ScoreEntryCeremony" />,
///     which offers every player's final score to the table in turn — through the CONG
///     initials screen when a score qualifies (notes §116) — and ends on the table itself,
///     with the entries just posted highlighted.
/// </summary>
public sealed class GameOverState : IGameState
{
    private readonly int _holdTicks = ArcadeClock.ToPortTicks(ScreenTuning.GameOverMessageRomFrames);
    private readonly IReadOnlyList<FinalScore> _scores;
    private readonly GameServices _services;
    private int _elapsedTicks;

    /// <summary>Builds the screen for a finished game's scores.</summary>
    /// <param name="input">Player 1's input, which the states that follow read.</param>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="highScoreStore">The high score store the ceremony writes to.</param>
    /// <param name="scores">Every player's final score, highest first — the ROM's <c>EGSUB</c> runs once per player.</param>
    /// <param name="controlSettings">The port's control definitions.</param>
    /// <param name="gameSettings">The GAME ADJUSTMENT settings (notes §131).</param>
    public GameOverState(
        IPlayerInputSource input,
        SpriteSet sprites,
        HighScoreStore highScoreStore,
        IReadOnlyList<FinalScore> scores,
        ControlSettings? controlSettings = null,
        GameSettings? gameSettings = null)
    {
        _services = new GameServices(sprites, highScoreStore, controlSettings ?? ControlSettings.CreateDefaults(),
            input, gameSettings ?? GameSettings.CreateFactoryDefaults());
        _scores = scores;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // ROM string 40 (GOMP): "GAME OVER" in the LARGE font, colour $AA, at (62, 128).
        _services.Sprites.TextRenderer.DrawLargeFontText(
            spriteBatch,
            "GAME OVER",
            HudLayout.ToPortColumnX(HudLayout.GameOverMessageColumn),
            HudLayout.ToPortY(HudLayout.GameOverMessageRow),
            ScreenTuning.GameOverTextSlot);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (++_elapsedTicks <
            _holdTicks) return; // NAP 120: the message holds for 2.4 s, and the ROM waits for nothing here

        // ENDPRC → ENDGAM → GOV: the scores are offered to the table in turn, and the table itself is
        // the end of the ceremony (notes §98).
        manager.TransitionTo(new ScoreEntryCeremony(_services, _scores).NextScreen());
    }

    /// <summary>Builds the screen from a session that has just ended.</summary>
    /// <param name="input">Player 1's input.</param>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="highScoreStore">The high score store.</param>
    /// <param name="session">The finished game, which supplies the final scores, the controls and the settings.</param>
    public static GameOverState CreateFromSession(IPlayerInputSource input, SpriteSet sprites,
        HighScoreStore highScoreStore, GameSession session)
    {
        return new GameOverState(input, sprites, highScoreStore, session.GetFinalScoresHighestFirst(),
            session.ControlSettings,
            session.GameSettings);
    }
}
