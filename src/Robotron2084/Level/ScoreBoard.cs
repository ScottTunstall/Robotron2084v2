using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>
/// The run's score. Crosses an extra-life threshold every
/// <c>extraLifeEveryPoints</c> points, repeating; <see cref="Add"/> reports the
/// crossing so the caller can award a life.
/// </summary>
public sealed class ScoreBoard
{
    private readonly int _step;
    private int _nextExtraLifeThreshold;

    /// <summary>Creates a board that carries a score over from a previous level.</summary>
    /// <param name="startingScore">
    /// Score carried over from a previous level (level restarts / wave clear
    /// hand the score across); thresholds already passed are skipped.
    /// </param>
    /// <param name="extraLifeEveryPoints">
    /// The GAME ADJUSTMENT page's EXTRA MAN EVERY in points (ROM <c>extra_man_every</c>, notes §131);
    /// the factory value by default. The arcade allows 0 there, which turns extra men off.
    /// </param>
    public ScoreBoard(int startingScore, int extraLifeEveryPoints = GameSettings.FactoryExtraManEveryPoints)
    {
        Score = startingScore;
        _step = extraLifeEveryPoints;
        _nextExtraLifeThreshold = _step > 0
            ? (startingScore / _step + 1) * _step
            : int.MaxValue;
    }

    public int Score { get; private set; }

    /// <summary>Adds points; returns true when an extra-life threshold was crossed (caller awards a life).</summary>
    public bool Add(int points)
    {
        Score += points;
        if (Score >= _nextExtraLifeThreshold)
        {
            _nextExtraLifeThreshold += _step;
            return true;
        }

        return false;
    }
}
