namespace Robotron2084.Level;

/// <summary>How many lives one player has left, counting the one being played.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRF.ASM</c> <c>PLAS</c>, each player's count of men.</item>
///         <item>Disassembly: <c>p1_men</c> ($BDEC) and <c>p2_men</c> ($BE28).</item>
///     </list>
/// </remarks>
public sealed class LivesCounter
{
    /// <summary>Makes a player's count of lives.</summary>
    /// <param name="lives">How many lives the player starts with.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lives" /> is below 0.</exception>
    public LivesCounter(int lives)
    {
        SetLives(lives);
    }

    /// <summary>How many lives are left, counting the one being played.</summary>
    public int Lives { get; private set; }

    /// <summary>Gives one more life.</summary>
    public void AddLife()
    {
        Lives += 1;
    }

    /// <summary>Takes one life off. A count that is already 0 stays at 0.</summary>
    public void LoseLife()
    {
        if (Lives > 0) Lives -= 1;
    }

    /// <summary>Puts the count at a new value.</summary>
    /// <param name="lives">The new count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lives" /> is below 0.</exception>
    public void SetLives(int lives)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lives);

        Lives = lives;
    }
}
