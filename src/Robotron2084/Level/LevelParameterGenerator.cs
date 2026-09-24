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
                EnemySpeedBonus: Math.Min(levelNumber - 1, GameplayConstants.EnemySpeedBonusCapPerLevel));
        }

        return LevelParameters.FromWave(levelNumber, WaveTable.ForWave(levelNumber));
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
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                string[] fields = line.Split(',');
                if (fields.Length != 8 || !int.TryParse(fields[0], out int level) || level <= 0)
                {
                    continue; // header row (or a row without a usable level number)
                }

                if (!int.TryParse(fields[1], out int grunts)
                    || !int.TryParse(fields[2], out int hulks)
                    || !int.TryParse(fields[3], out int spheroids)
                    || !int.TryParse(fields[4], out int quarks)
                    || !int.TryParse(fields[5], out int electrodes)
                    || !int.TryParse(fields[6], out int maxEnforcers)
                    || !int.TryParse(fields[7], out int maxTanks))
                {
                    return null; // malformed data row -> treat the whole table as unusable
                }

                rows.Add(new LevelTableRow(grunts, hulks, spheroids, quarks, electrodes, maxEnforcers, maxTanks));
            }

            return rows.Count > 0 ? rows.ToArray() : null;
        }
        catch (IOException)
        {
            return null;
        }
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
