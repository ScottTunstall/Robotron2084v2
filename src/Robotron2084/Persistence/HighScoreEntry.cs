namespace Robotron2084.Persistence;

/// <summary>One saved high-score row: up-to-three-letter initials and the score.</summary>
public sealed record HighScoreEntry(string Initials, int Score);
