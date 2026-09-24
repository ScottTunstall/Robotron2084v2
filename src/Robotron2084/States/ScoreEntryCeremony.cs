using Robotron2084.Level;
using Robotron2084.Persistence;

namespace Robotron2084.States;

/// <summary>
/// The end-game score ceremony (notes §98/§116) — ROM RRTESTC's <c>ENDGAM</c>: every player's final
/// score is offered to the high score table in turn. A score that qualifies gets the CONG initials
/// screen and is written into the table under the initials its player typed (the arcade's CMOS
/// write); the next score follows, and when none is left the table itself comes up with the session's
/// scores highlighted. A score that no longer qualifies by the time its turn comes is passed over,
/// which is the ROM's own re-check (<c>GET333</c>).
/// </summary>
public sealed class ScoreEntryCeremony
{
    private readonly GameServices _services;
    private readonly HighScoreStore _store;
    private readonly HighScoreTable _table;
    private readonly Queue<FinalScore> _pending;
    private readonly int[] _sessionScores;

    /// <summary>Builds the ceremony for a finished game, loading the table the scores are offered to.</summary>
    /// <param name="services">The attract screens' bundle: sprites, the store, the controls and player 1's input.</param>
    /// <param name="scoresHighestFirst">The game's final scores with their player numbers.</param>
    public ScoreEntryCeremony(GameServices services, IReadOnlyList<FinalScore> scoresHighestFirst)
    {
        _services = services;
        _store = services.HighScores;
        _table = _store.Load();
        _pending = new Queue<FinalScore>(scoresHighestFirst);
        _sessionScores = [.. scoresHighestFirst.Select(score => score.Score)];
    }

    /// <summary>The next screen: the next score's initials screen while one still qualifies, otherwise the high score table.</summary>
    public IGameState NextScreen()
    {
        while (_pending.Count > 0)
        {
            FinalScore score = _pending.Dequeue();
            if (_table.Qualifies(score.Score))
            {
                return new InitialsEntryState(_services, this, score);
            }
        }

        return new HighScoreTableState(_services, _sessionScores, _table);
    }

    /// <summary>Writes one finished score into the table under the initials its player typed and saves it (ROM `EGSUB`'s insertion and CMOS write).</summary>
    /// <param name="score">The score the initials screen has just collected a name for.</param>
    /// <param name="initials">The initials the player entered.</param>
    /// <returns>What the table did with the score, which decides whether the ONLY5P page follows.</returns>
    public HighScoreTable.SubmitResult Submit(FinalScore score, string initials)
    {
        HighScoreTable.SubmitResult result = _table.Submit(score.Score, initials);
        _store.Save(_table);
        return result;
    }
}
