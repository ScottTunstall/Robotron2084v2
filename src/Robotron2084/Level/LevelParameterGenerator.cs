using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Works out what each wave contains: how many of each robot, and how fast and how often they act.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the wave tables when a wave begins</item>
/// <item>Disassembly: <c>$2B7C</c>, which reads the counts at <c>$2E24</c> and the settings at <c>$2C20</c></item>
/// </list>
/// By default it uses the arcade's own wave tables (<see cref="WaveTable"/>, notes §11), where the waves after the fortieth repeat the second half of the table.
/// If a <c>LevelTable.csv</c> file is found, either at a path it is given or in the game's <c>Content</c> folder, its rows are used instead and
/// start again from the top after the last row. A missing or damaged file is ignored.
/// </remarks>
public sealed class LevelParameterGenerator
{
    /// <summary>The rows of the table that replaces the arcade's wave tables, or null to use the arcade's.</summary>
    private readonly LevelTableRow[]? _table;

    /// <summary>Makes a generator that looks for a <c>LevelTable.csv</c> in the game's <c>Content</c> folder.</summary>
    public LevelParameterGenerator()
        : this(null)
    {
    }

    /// <summary>Makes a generator that looks for a table that replaces the arcade's wave tables.</summary>
    /// <param name="levelTablePath">The path of a CSV file whose header reads Level, GruntCount, HulkCount, SpheroidCount, QuarkCount, ElectrodeCount, MaxEnforcersPerSpheroid, MaxTanksPerQuark. When it is null, the game's <c>Content</c> folder is looked in.</param>
    public LevelParameterGenerator(string? levelTablePath)
    {
        _table = LoadTable(levelTablePath);
    }

    /// <summary>Works out what a wave contains.</summary>
    /// <param name="levelNumber">The wave number, starting at 1.</param>
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

    /// <summary>Reads the replacement table.</summary>
    /// <param name="path">The CSV file's path, or null for the one in the game's <c>Content</c> folder.</param>
    /// <returns>The rows, or null when there is no file, it is damaged or it has no rows.</returns>
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

    /// <summary>Reads one line of the replacement table.</summary>
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

    /// <summary>One row of the replacement table. The wave number is not kept, because a row's place in the table is its wave.</summary>
    private sealed record LevelTableRow(
        int GruntCount,
        int HulkCount,
        int SpheroidCount,
        int QuarkCount,
        int ElectrodeCount,
        int MaxEnforcersPerSpheroid,
        int MaxTanksPerQuark);
}
