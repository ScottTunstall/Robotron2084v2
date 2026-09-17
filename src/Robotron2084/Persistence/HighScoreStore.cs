using System.Text.Json;
using Robotron2084.Tuning;

namespace Robotron2084.Persistence;

/// <summary>
/// Persists the top-10 high-score table as JSON under the user's
/// LocalApplicationData (Phase 11.7). Corrupt/missing files degrade to an
/// empty list rather than throwing.
/// </summary>
public sealed class HighScoreStore
{
    /// <summary>How many entries the table holds.</summary>
    public const int Capacity = GameplayConstants.HighScoreCapacity;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Robotron2084",
        "highscores.json");

    /// <summary>Loads the table; missing or corrupt file yields an empty list (never throws).</summary>
    public IReadOnlyList<HighScoreEntry> Load()
    {
        if (!File.Exists(FilePath))
        {
            return Array.Empty<HighScoreEntry>();
        }

        try
        {
            List<HighScoreEntry>? entries = JsonSerializer.Deserialize<List<HighScoreEntry>>(File.ReadAllText(FilePath));
            return entries ?? new List<HighScoreEntry>();
        }
        catch (Exception)
        {
            return Array.Empty<HighScoreEntry>();
        }
    }

    /// <summary>Saves, truncating to the top <see cref="Capacity"/> entries sorted by score descending.</summary>
    public void Save(IReadOnlyList<HighScoreEntry> entries)
    {
        List<HighScoreEntry> top = entries.OrderByDescending(e => e.Score).Take(Capacity).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(top, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>True when <paramref name="score"/> would make the table (fewer than full, or beats the lowest entry).</summary>
    public bool QualifiesForTopTen(int score, IReadOnlyList<HighScoreEntry> current) =>
        current.Count < Capacity || score > current.Min(e => e.Score);
}
