using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>Keeps a player's score, and says when it has earned a spare man.</summary>
/// <remarks>A spare man is earned each time the score passes a multiple of the extra man setting (notes §131). <see cref="Add"/> reports it, so the caller can give the man.</remarks>
public sealed class ScoreBoard
{
    private readonly int _step;
    private int _nextExtraLifeThreshold;

    /// <summary>Makes a score board that carries on from the score a player already has.</summary>
    /// <param name="startingScore">The score carried in from the last wave. Spare men it has already earned are not given again.</param>
    /// <param name="extraLifeEveryPoints">How many points earn a spare man: the EXTRA MAN EVERY setting. Nothing earns one when it is 0.</param>
    public ScoreBoard(int startingScore, int extraLifeEveryPoints = GameSettings.FactoryExtraManEveryPoints)
    {
        Score = startingScore;
        _step = extraLifeEveryPoints;
        _nextExtraLifeThreshold = _step > 0
            ? (startingScore / _step + 1) * _step
            : int.MaxValue;
    }

    /// <summary>The player's score.</summary>
    public int Score { get; private set; }

    /// <summary>Adds points to the score.</summary>
    /// <param name="points">The points to add.</param>
    /// <returns>True when the new score has earned a spare man.</returns>
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
