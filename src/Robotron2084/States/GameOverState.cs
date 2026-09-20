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
/// text: it holds the ROM's message, offers every player's score to the table
/// (highest first — the ROM's <c>EGSUB</c> loop over <c>ZP1SCR</c>/<c>ZP2SCR</c>)
/// and hands over to the table, which highlights the entries just posted.
///
/// The initials/name ENTRY screens the arcade interleaves here (<c>GODMSP</c>,
/// <c>CONG</c>, <c>NOWMSP</c> + <c>GETLT</c>) are NOT implemented yet — notes
/// §98.4. A posted score therefore carries the ROM's own blank name (`NULSCR`).
/// </summary>
public sealed class GameOverState : IGameState
{
    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly List<int> _scores;
    private readonly int _holdTicks = GameplayConstants.PortTicks(GameplayConstants.GameOverMessageRomFrames);
    private int _elapsedTicks;

    public GameOverState(IPlayerInputSource input, SpriteSet sprites, HighScoreStore highScores, int score)
        : this(input, sprites, highScores, [score])
    {
    }

    /// <param name="scores">
    /// Every player's final score, highest first — the ROM's <c>EGSUB</c> runs once
    /// per player, and each score is offered to the table in turn.
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
        if (++_elapsedTicks < _holdTicks)
        {
            return; // NAP 120: the message holds for 2.4 s, and the ROM waits for nothing here
        }

        // ENDPRC → ENDGAM: offer each score, then the table (GOV → LOGG1 → TABORG).
        HighScoreTable table = _highScores.Load();
        foreach (int score in _scores)
        {
            table.Submit(score);
        }

        _highScores.Save(table);
        manager.TransitionTo(new HighScoreTableState(_input, _sprites, _highScores, _scores));
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // ROM string 40 (GOMP): "GAME OVER" in the LARGE font, colour $AA, at (62, 128).
        _sprites.DrawLargeFontText(
            spriteBatch,
            "GAME OVER",
            GameplayConstants.ArcadeX(GameplayConstants.GameOverTextColumn * 2),
            GameplayConstants.ArcadeY(GameplayConstants.GameOverTextRow),
            GameplayConstants.GameOverTextSlot);
    }
}
