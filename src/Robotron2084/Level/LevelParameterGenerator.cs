using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// Produces the per-level parameters.
///
/// The source of truth is the arcade's own wave tables —
/// <see cref="WaveTable"/> (ROM $2E24 counts + $2C20 difficulty settings,
/// verified byte-exact; arcade-fidelity-notes §11). Waves 1-40 are unique;
/// waves 41+ repeat waves 21-40 (the ROM's rule, $2B7C).
///
/// Optional designer-supplied table: if a LevelTable.csv is found (injected path, or
/// Content/LevelTable.csv next to the app), its count rows are used
/// verbatim, cycling once past the last row. Any missing/malformed file
/// falls back to the ROM tables.
/// </summary>
public sealed class LevelParameterGenerator
{
    private readonly LevelTableRow[]? _table;

    public LevelParameterGenerator()
        : this(null)
    {
    }

    /// <summary>Creates a generator that reads an optional designer table.</summary>
    /// <param name="levelTablePath">
    /// Optional CSV table (header: Level,GruntCount,HulkCount,SpheroidCount,
    /// QuarkCount,ElectrodeCount,MaxEnforcersPerSpheroid,MaxTanksPerQuark).
    /// <see langword="null"/> checks the default Content/LevelTable.csv location.
    /// </param>
    public LevelParameterGenerator(string? levelTablePath)
    {
        _table = LoadTable(levelTablePath);
    }

    public LevelParameters Generate(int levelNumber)
    {
        if (_table is { Length: > 0 } table)
        {
            LevelTableRow row = table[(levelNumber - 1) % table.Length];
            return new LevelParameters(
                levelNumber,
                GruntCount: row.GruntCount,
                HulkCount: row.HulkCount,
                SpheroidCount: row.SpheroidCount,
                QuarkCount: row.QuarkCount,
                ElectrodeCount: row.ElectrodeCount,
                MaxEnforcersPerSpheroid: row.MaxEnforcersPerSpheroid,
                MaxTanksPerQuark: row.MaxTanksPerQuark,
                EnemySpeedBonus: Math.Min(levelNumber - 1, SpawnTuning.EnemySpeedBonusCapPerLevel));
        }

        return LevelParameters.CreateFromWave(levelNumber, WaveTable.GetParameters(levelNumber));
    }

    private static LevelTableRow[]? LoadTable(string? path)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "Content", "LevelTable.csv");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            List<LevelTableRow> rows = new();
            foreach (string rawLine in File.ReadAllLines(path))
            {
                if (!TryParseRow(rawLine.Trim(), out LevelTableRow? row))
                {
                    return null; // malformed data row -> treat the whole table as unusable
                }

                if (row is not null)
                {
                    rows.Add(row);
                }
            }

            return rows.Count > 0 ? rows.ToArray() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Parses one LevelTable.csv line.</summary>
    /// <param name="line">The trimmed line.</param>
    /// <param name="row">The parsed row, or null for a line to skip (blank, the header, or no usable level number).</param>
    /// <returns>False when the line is a data row with a malformed count.</returns>
    private static bool TryParseRow(string line, out LevelTableRow? row)
    {
        row = null;
        string[] fields = line.Split(',');
        if (line.Length == 0 || fields.Length != 8 || !int.TryParse(fields[0], out int level) || level <= 0)
        {
            return true;
        }

        int[] counts = new int[fields.Length - 1];
        for (int i = 0; i < counts.Length; i++)
        {
            if (!int.TryParse(fields[i + 1], out counts[i]))
            {
                return false;
            }
        }

        row = new LevelTableRow(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], counts[6]);
        return true;
    }

    /// <summary>One parsed LevelTable.csv row (level number dropped — position in the array is the level).</summary>
    private sealed record LevelTableRow(
        int GruntCount,
        int HulkCount,
        int SpheroidCount,
        int QuarkCount,
        int ElectrodeCount,
        int MaxEnforcersPerSpheroid,
        int MaxTanksPerQuark);
}
