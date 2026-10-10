using System.Text.Json;
using System.Text.Json.Serialization;

namespace Robotron2084.Persistence;

/// <summary>
/// Persists the arcade's high score table (notes §98): the ALL-TIME list
/// (ROM <c>CMSCOR</c>) and the operator's top "GOD" entry — the two things the
/// arcade keeps in its battery-backed CMOS. TODAY'S list is NOT saved: the ROM
/// reloads it from its own factory table at every power-up (<c>CKHS</c>
/// <c>LDX #TODTAB / LDY #TODAYS / CMSMVV</c>), so the port does too
/// (<see cref="HighScoreTable.CreateFromSaved"/>).
///
/// A missing file yields the ROM's factory defaults, the same way a fresh cabinet
/// comes up with RRTESTC's default table. A file that EXISTS but cannot be read or
/// parsed throws a <see cref="PersistenceException"/> naming it (ERR-1): silently
/// falling back would let the next save overwrite the user's table.
/// </summary>
public sealed class HighScoreStore
{
    private static readonly string FilePath = AppDataPaths.GetFilePath("highscores.json");

    /// <summary>Loads from a given file — the seam the tests use.</summary>
    public static HighScoreTable Load(string path)
    {
        if (!File.Exists(path))
        {
            return HighScoreTable.CreateWithFactoryScores();
        }

        try
        {
            SavedTable? saved = JsonSerializer.Deserialize<SavedTable>(File.ReadAllText(path));
            return HighScoreTable.CreateFromSaved(saved?.Top, saved?.AllTime);
        }
        catch (FileNotFoundException)
        {
            return HighScoreTable.CreateWithFactoryScores(); // removed between the check and the read
        }
        catch (JsonException exception)
        {
            throw new PersistenceException($"The high score table file {path} is not valid JSON: {exception.Message}", exception);
        }
        catch (IOException exception)
        {
            throw new PersistenceException($"The high score table file {path} could not be read: {exception.Message}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PersistenceException($"The high score table file {path} could not be opened: {exception.Message}", exception);
        }
    }

    /// <summary>Saves to a given file — the seam the tests use.</summary>
    public static void Save(string path, HighScoreTable table)
    {
        var saved = new SavedTable(
            table.Top,
            [.. table.AllTime.Take(HighScoreTable.AllTimeCapacity)]);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>The table: the saved all-time list + top entry, and the ROM's factory today's list.</summary>
    public HighScoreTable Load() => Load(FilePath);

    /// <summary>Saves the all-time list and the top entry (the two CMOS things).</summary>
    public void Save(HighScoreTable table) => Save(FilePath, table);

    /// <summary>The on-disk shape: the two things the arcade keeps in CMOS.</summary>
    private sealed record SavedTable(
        [property: JsonPropertyName("top")] TopScoreEntry? Top,
        [property: JsonPropertyName("allTime")] List<HighScoreEntry>? AllTime);
}
