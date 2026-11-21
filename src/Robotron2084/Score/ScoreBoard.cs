using Robotron2084.Persistence;

namespace Robotron2084.Score;

/// <summary>Keeps a player's score, and says when it has earned a spare life.</summary>
/// <remarks>
///     A spare life is earned each time the score passes a multiple of the extra life setting (notes §131).
///     <see cref="Add" /> reports it, so the caller can give the life. A score never goes below 0 or above the biggest
///     number a score can hold.
/// </remarks>
public sealed class ScoreBoard
{
    private readonly int _extraLifeEveryPoints;
    private int _nextExtraLifeThreshold;

    /// <summary>Makes a player's score board.</summary>
    /// <param name="startingScore">
    ///     The score the player starts with: 0 at the start of a game. Spare lives it has already earned are not
    ///     given again.
    /// </param>
    /// <param name="extraLifeEveryPoints">
    ///     How many points earn a spare life: the EXTRA MAN EVERY setting. Nothing earns one
    ///     when it is 0.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="startingScore" /> or <paramref name="extraLifeEveryPoints" /> is below 0.
    /// </exception>
    public ScoreBoard(int startingScore, int extraLifeEveryPoints = GameSettings.FactoryExtraLifeEveryPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(extraLifeEveryPoints);

        _extraLifeEveryPoints = extraLifeEveryPoints;
        SetScore(startingScore);
    }

    /// <summary>The player's score.</summary>
    public int Score { get; private set; }

    /// <summary>Adds points to the score. A score that would go past the biggest number it can hold stays at that number.</summary>
    /// <param name="points">The points to add.</param>
    /// <returns>True when the new score has earned a spare life.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="points" /> is below 0.</exception>
    /// <remarks>
    ///     A score is only ever added to, and the most it is given at once is far below the extra life setting, so one
    ///     call earns one spare life at most. If it did pass more than one multiple, the later ones are not given.
    /// </remarks>
    public bool Add(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);

        Score = (int)Math.Min(int.MaxValue, (long)Score + points);
        if (Score < _nextExtraLifeThreshold) return false;

        _nextExtraLifeThreshold = GetNextThreshold(Score);
        return true;
    }

    /// <summary>
    ///     Puts the score at a new value, as if the player had just reached it. Spare lives it has already passed are
    ///     not given; the next one comes at the next multiple of the extra life setting above it.
    /// </summary>
    /// <param name="score">The new score.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score" /> is below 0.</exception>
    public void SetScore(int score)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);

        Score = score;
        _nextExtraLifeThreshold = GetNextThreshold(score);
    }

    /// <summary>Gets the score that earns the next spare life: the first multiple of the extra life setting above a score.</summary>
    /// <param name="score">The score.</param>
    /// <returns>The next multiple, or the biggest number a score can hold when no spare life is to be had.</returns>
    private int GetNextThreshold(int score)
    {
        if (_extraLifeEveryPoints == 0) return int.MaxValue;

        var nextThreshold = ((long)score / _extraLifeEveryPoints + 1) * _extraLifeEveryPoints;
        return (int)Math.Min(int.MaxValue, nextThreshold);
    }
}
