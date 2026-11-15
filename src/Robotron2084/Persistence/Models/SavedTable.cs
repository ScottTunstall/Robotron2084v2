using System.Text.Json.Serialization;

namespace Robotron2084.Persistence.Models;

/// <summary>The on-disk shape: the two things the arcade keeps in CMOS.</summary>
/// <param name="TopScoreEntry">The operator's top score, or null when the file has none.</param>
/// <param name="AllTime">The all-time high score table, best first, or null when the file has none.</param>
internal sealed record SavedTable(
    [property: JsonPropertyName("top")] TopScoreEntry? TopScoreEntry,
    [property: JsonPropertyName("allTime")]
    List<HighScoreEntry>? AllTime);
