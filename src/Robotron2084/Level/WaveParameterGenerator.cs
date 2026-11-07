using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Works out what each wave contains: how many of each robot, and how fast and how often they act.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>GETWV</c>, which reads the wave
///             tables when a wave begins
///         </item>
///         <item>
///             Disassembly: <c>$2B7C</c>, which reads the counts at <c>$2E24</c> and the settings at
///             <c>$2C20</c>
///         </item>
///     </list>
///     By default it uses the arcade's own wave tables (
///     <see
///         cref="WaveTable" />
///     , notes §11), where the waves after the fortieth repeat the second half of the
///     table. If a <c>CustomWaveTable.csv</c> file is found, either at a path it is given or in the game's
///     <c>Content</c> folder, its rows are used instead and start again from the top after the last row.
///     A missing or damaged file is ignored.</item>
///     </list>
/// </remarks>
public sealed class WaveParameterGenerator
{
    /// <summary>The rows of the table that replaces the arcade's wave tables, or null to use the arcade's.</summary>
    private readonly CustomWaveTableRow[]? _customWaveTable;

    /// <summary>Makes a generator that looks for a <c>CustomWaveTable.csv</c> in the game's <c>Content</c> folder.</summary>
    public WaveParameterGenerator()
        : this(null)
    {
    }

    /// <summary>Makes a generator that looks for a table that replaces the arcade's wave tables.</summary>
    /// <param name="customWaveTablePath">
    ///     The path of a CSV file whose header reads Wave, GruntCount, HulkCount, SpheroidCount,
    ///     QuarkCount, ElectrodeCount, MaxEnforcersPerSpheroid, MaxTanksPerQuark. When it is null, the game's <c>Content</c>
    ///     folder is looked in.
    /// </param>
    public WaveParameterGenerator(string? customWaveTablePath)
    {
        _customWaveTable = LoadTable(customWaveTablePath);
    }

    /// <summary>Works out what a wave of the port's numbering contains (<see cref="WaveNumbering" />).</summary>
    /// <param name="waveNumber">The port's wave number, starting at 1.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Wave 1 is the introduction to Gorf. It is a placeholder until the author specifies it: Gorf alone,
    ///             and none of the arcade's robots, which drops as many grunts as the wave after it has (notes §150).
    ///         </item>
    ///         <item>Every other wave is the arcade's wave one number lower.</item>
    ///     </list>
    /// </remarks>
    public WaveParameters Generate(int waveNumber)
    {
        if (WaveNumbering.IsGorfIntroWave(waveNumber)) return CreateGorfIntroWave();

        return GenerateArcadeWave(WaveNumbering.ToArcadeWave(waveNumber));
    }

    /// <summary>Works out what one of the arcade's waves contains.</summary>
    /// <param name="arcadeWave">The arcade's wave number, starting at 1.</param>
    public WaveParameters GenerateArcadeWave(int arcadeWave)
    {
        if (_customWaveTable is { Length: > 0 } table)
        {
            var customWaveTableRow = table[(arcadeWave - 1) % table.Length];
            return new WaveParameters(
                arcadeWave,
                customWaveTableRow.GruntCount,
                HulkCount: customWaveTableRow.HulkCount,
                SpheroidCount: customWaveTableRow.SpheroidCount,
                QuarkCount: customWaveTableRow.QuarkCount,
                ElectrodeCount: customWaveTableRow.ElectrodeCount,
                MaxEnforcersPerSpheroid: customWaveTableRow.MaxEnforcersPerSpheroid,
                MaxTanksPerQuark: customWaveTableRow.MaxTanksPerQuark,
                EnemySpeedBonus: Math.Min(arcadeWave - 1, SpawnTuning.EnemySpeedBonusMaxPerWave));
        }

        return WaveParameters.CreateFromWave(arcadeWave, WaveTable.GetParameters(arcadeWave));
    }

    /// <summary>
    ///     Makes the wave that introduces Gorf: a placeholder with Gorf alone, which drops as many grunts in all as the
    ///     wave after it has (the arcade's first wave). It looks like that wave too (its wall and electrode colours).
    /// </summary>
    private WaveParameters CreateGorfIntroWave()
    {
        var gruntsOfTheNextWave = GenerateArcadeWave(WaveNumbering.ToArcadeWave(WaveNumbering.FirstArcadeWave)).GruntCount;
        return new WaveParameters(
            WaveNumbering.ToArcadeWave(WaveNumbering.GorfIntroWave),
            GorfCount: 1,
            GorfGruntDropCount: gruntsOfTheNextWave);
    }

    /// <summary>Reads the replacement table.</summary>
    /// <param name="path">The CSV file's path, or null for the one in the game's <c>Content</c> folder.</param>
    /// <returns>The rows, or null when there is no file, it is damaged or it has no rows.</returns>
    private static CustomWaveTableRow[]? LoadTable(string? path)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "Content", "CustomWaveTable.csv");
        if (!File.Exists(path)) return null;

        try
        {
            List<CustomWaveTableRow> rows = new();
            foreach (var rawLine in File.ReadAllLines(path))
            {
                if (!TryParseRow(rawLine.Trim(),
                        out var customWaveTableRow)) return null; // malformed data row -> treat the whole table as unusable

                if (customWaveTableRow is not null) rows.Add(customWaveTableRow);
            }

            if (rows.Count == 0) return null;

            return rows.ToArray();
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Reads one line of the replacement table.</summary>
    /// <param name="line">The trimmed line.</param>
    /// <param name="customWaveTableRow">The parsed row, or null for a line to skip (blank, the header, or no usable wave number).</param>
    /// <returns>False when the line is a data row with a malformed count.</returns>
    private static bool TryParseRow(string line, out CustomWaveTableRow? customWaveTableRow)
    {
        customWaveTableRow = null;
        var fields = line.Split(',');
        if (line.Length == 0 || fields.Length != 8 || !int.TryParse(fields[0], out var wave) || wave <= 0)
            return true;

        var counts = new int[fields.Length - 1];
        for (var i = 0; i < counts.Length; i++)
            if (!int.TryParse(fields[i + 1], out counts[i]))
                return false;

        customWaveTableRow = new CustomWaveTableRow(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], counts[6]);
        return true;
    }
}
