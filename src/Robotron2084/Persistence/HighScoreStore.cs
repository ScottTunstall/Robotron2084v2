using System.Text.Json;
using System.Text.Json.Serialization;

namespace Robotron2084.Persistence;

/// <summary>
/// Persists the arcade's high score table (notes §98): the ALL-TIME list
/// (ROM <c>CMSCOR</c>) and the operator's top "GOD" entry — the two things the
/// arcade keeps in its battery-backed CMOS. TODAY'S list is NOT saved: the ROM
/// reloads it from its own factory table at every power-up (<c>CKHS</c>
/// <c>LDX #TODTAB / LDY #TODAYS / CMSMVV</c>), so the port does too
/// (<see cref="HighScoreTable.FromSaved"/>).
///
/// A missing or corrupt file yields the ROM's factory defaults rather than
/// throwing, the same way a fresh cabinet comes up with RRTESTC's default table.
/// </summary>
public sealed class HighScoreStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Robotron2084",
        "highscores.json");

    /// <summary>The table: the saved all-time list + top entry, and the ROM's factory today's list.</summary>
    public HighScoreTable Load() => Load(FilePath);

    /// <summary>Loads from a given file — the seam the tests use.</summary>
    public static HighScoreTable Load(string path)
    {
        if (!File.Exists(path))
        {
            return HighScoreTable.CreateFactory();
        }

        try
        {
            SavedTable? saved = JsonSerializer.Deserialize<SavedTable>(File.ReadAllText(path));
            return HighScoreTable.FromSaved(saved?.Top, saved?.AllTime);
        }
        catch (Exception)
        {
            return HighScoreTable.CreateFactory();
        }
    }

    /// <summary>Saves the all-time list and the top entry (the two CMOS things).</summary>
    public void Save(HighScoreTable table) => Save(FilePath, table);

    /// <summary>Saves to a given file — the seam the tests use.</summary>
    public static void Save(string path, HighScoreTable table)
    {
        var saved = new SavedTable(
            table.Top,
            [.. table.AllTime.Take(HighScoreTable.AllTimeCapacity)]);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>The on-disk shape: the two things the arcade keeps in CMOS.</summary>
    private sealed record SavedTable(
        [property: JsonPropertyName("top")] TopScoreEntry? Top,
        [property: JsonPropertyName("allTime")] List<HighScoreEntry>? AllTime);
}
