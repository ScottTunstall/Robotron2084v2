using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// The run's score. Crosses an extra-life threshold every
/// <see cref="GameplayConstants.ExtraLifeThresholdStep"/> points, repeating;
/// <see cref="Add"/> reports the crossing so the caller can award a life.
/// </summary>
public sealed class ScoreBoard
{
    private int _nextExtraLifeThreshold;

    public ScoreBoard()
        : this(0)
    {
    }

    /// <summary>Creates a board that carries a score over from a previous level.</summary>
    /// <param name="startingScore">
    /// Score carried over from a previous level (level restarts / wave clear
    /// hand the score across); thresholds already passed are skipped.
    /// </param>
    public ScoreBoard(int startingScore)
    {
        Score = startingScore;
        _nextExtraLifeThreshold =
            (startingScore / GameplayConstants.ExtraLifeThresholdStep + 1) * GameplayConstants.ExtraLifeThresholdStep;
    }

    public int Score { get; private set; }

    /// <summary>Adds points; returns true when an extra-life threshold was crossed (caller awards a life).</summary>
    public bool Add(int points)
    {
        Score += points;
        if (Score >= _nextExtraLifeThreshold)
        {
            _nextExtraLifeThreshold += GameplayConstants.ExtraLifeThresholdStep;
            return true;
        }

        return false;
    }
}
