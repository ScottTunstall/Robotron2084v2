namespace Robotron2084.Tuning;

/// <summary>Where the wave places its entities and how the level generator scales them.</summary>
public static class SpawnTuning
{
    // Level generation
    public const int EnemySpeedBonusCapPerLevel = 5;

    public const int SpawnPlacementMaxAttempts = 100;

    public const int ElectrodeMinDistanceFromPlayer = 40; // spec-px (apply ScreenSize.Scaled at the use site)

    // Grunt
    public const int GruntMinDistanceFromPlayer = 20; // spec-px, spec-stated

    // Hulk — ROM RRH11: step period comes from the
    // wave table (HLKSPD, in ROM ticks); step sizes are fixed by the ROM
    // animation table (horizontal 3/4 arcade px alternating, vertical 2).
    // Laser knockback is the ROM RRH11 HULKIL per-axis random
    // push — X ±1/±2 arcade px (50/50), Y ±1/±4 (25/75); see
    // Hulk.ApplyKnockback (the magnitudes are ROM-intrinsic, no constant).
    public const int HulkMinDistanceFromPlayer = 35; // spec-px (spec gives a 30–40 range; 35 = midpoint)

    /// <summary>ROM `ENFCNT`: at most this many spheroid-dropped enforcers live at once (notes §11).</summary>
    public const int EnforcerCap = 8;

    /// <summary>ROM `TNKCNT`: at most this many quark-dropped tanks live at once (notes §11).</summary>
    public const int TankCap = 20;

    /// <summary>
    /// The shells a wave's tanks may fire. Only a laser KILL gives one back (the fizzle bug, notes §53), so late in
    /// the wave the tanks stop firing.
    /// </summary>
    public const int ShellsPerWave = 20;
}
