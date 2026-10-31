using Robotron2084.Persistence;

namespace Robotron2084.Level;

/// <summary>Keeps a player's score, and says when it has earned a spare life.</summary>
/// <remarks>
///     A spare life is earned each time the score passes a multiple of the extra life setting (notes §131).
///     <see cref="Add" /> reports it, so the caller can give the life.
/// </remarks>
public sealed class ScoreBoard
{
    private readonly int _extraLifeEveryPoints;
    private int _nextExtraLifeThreshold;

    /// <summary>Makes a score board that carries on from the score a player already has.</summary>
    /// <param name="startingScore">
    ///     The score carried in from the last wave. Spare lives it has already earned are not given
    ///     again.
    /// </param>
    /// <param name="extraLifeEveryPoints">
    ///     How many points earn a spare life: the EXTRA MAN EVERY setting. Nothing earns one
    ///     when it is 0.
    /// </param>
    public ScoreBoard(int startingScore, int extraLifeEveryPoints = GameSettings.FactoryExtraLifeEveryPoints)
    {
        Score = startingScore;
        _extraLifeEveryPoints = extraLifeEveryPoints;
        _nextExtraLifeThreshold = int.MaxValue;
        if (_extraLifeEveryPoints > 0)
            _nextExtraLifeThreshold = (startingScore / _extraLifeEveryPoints + 1) * _extraLifeEveryPoints;
    }

    /// <summary>The player's score.</summary>
    public int Score { get; private set; }

    /// <summary>Adds points to the score.</summary>
    /// <param name="points">The points to add.</param>
    /// <returns>True when the new score has earned a spare life.</returns>
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
