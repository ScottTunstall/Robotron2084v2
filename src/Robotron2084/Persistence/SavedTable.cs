using System.Text.Json.Serialization;

namespace Robotron2084.Persistence;

/// <summary>The on-disk shape: the two things the arcade keeps in CMOS.</summary>
internal sealed record SavedTable(
    [property: JsonPropertyName("top")] TopScoreEntry? TopScoreEntry,
    [property: JsonPropertyName("allTime")]
    List<HighScoreEntry>? AllTime);
