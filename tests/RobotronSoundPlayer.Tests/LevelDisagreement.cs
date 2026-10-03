namespace RobotronSoundPlayer.Tests;

/// <summary>How far two boards' outputs disagreed over the same stretch of time.</summary>
/// <param name="DisagreeingCycles">The clock cycles during which their levels differed.</param>
/// <param name="ComparedCycles">The clock cycles compared.</param>
/// <param name="LongestCycles">The longest single stretch during which they differed.</param>
internal readonly record struct LevelDisagreement(long DisagreeingCycles, long ComparedCycles, long LongestCycles)
{
    /// <summary>The share of the time their levels differed, from 0 to 1.</summary>
    public double Share => (double)DisagreeingCycles / ComparedCycles;
}
