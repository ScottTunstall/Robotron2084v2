using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>Keeps a player's score, and says when it has earned a spare man.</summary>
/// <remarks>A spare man is earned each time the score passes a multiple of the extra man setting (notes §131). <see cref="Add"/> reports it, so the caller can give the man.</remarks>
public sealed class ScoreBoard
{
    private readonly int _extraLifeEveryPoints;
    private int _nextExtraLifeThreshold;

    /// <summary>Makes a score board that carries on from the score a player already has.</summary>
    /// <param name="startingScore">The score carried in from the last wave. Spare men it has already earned are not given again.</param>
    /// <param name="extraLifeEveryPoints">How many points earn a spare man: the EXTRA MAN EVERY setting. Nothing earns one when it is 0.</param>
    public ScoreBoard(int startingScore, int extraLifeEveryPoints = GameSettings.FactoryExtraManEveryPoints)
    {
        Score = startingScore;
        _extraLifeEveryPoints = extraLifeEveryPoints;
        _nextExtraLifeThreshold = _extraLifeEveryPoints > 0
            ? (startingScore / _extraLifeEveryPoints + 1) * _extraLifeEveryPoints
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
            _nextExtraLifeThreshold += _extraLifeEveryPoints;
            return true;
        }

        return false;
    }
}
