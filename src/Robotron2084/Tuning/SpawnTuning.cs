namespace Robotron2084.Tuning;

/// <summary>Where the wave places its entities and how the level generator scales them.</summary>
public static class SpawnTuning
{
    public const int ElectrodeMinDistanceFromPlayer = 40;

    // Level generation
    public const int EnemySpeedBonusCapPerLevel = 5;

    /// <summary>ROM `ENFCNT`: at most this many spheroid-dropped enforcers live at once (notes §11).</summary>
    public const int EnforcerCap = 8;

    // Grunt
    public const int GruntMinDistanceFromPlayer = 20;

    // Hulk — ROM RRH11: beat interval comes from the
    // wave table (HLKSPD, in ROM ticks); step sizes are fixed by the ROM
    // animation table (horizontal 3/4 arcade px alternating, vertical 2).
    // Laser knockback is the ROM RRH11 HULKIL per-axis random
    // push — X ±1/±2 arcade px (50/50), Y ±1/±4 (25/75); see
    // Hulk.ApplyKnockback (the magnitudes are ROM-intrinsic, no constant).
    public const int HulkMinDistanceFromPlayer = 35;

    /// <summary>
    /// A tank may fire while the wave's shell count is no higher than this, so twenty-one shells can be fired. Only a
    /// laser KILL gives one back (the fizzle bug, notes §53), so late in the wave the tanks stop firing.
    /// </summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>CMPA #20 / LBHI TNKFX</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E59</c>.</remarks>
    public const int ShellCountLimit = 20;

    public const int SpawnPlacementMaxAttempts = 100;

    // spec-px (apply ScreenSize.ToPortPixels at the use site)

    // spec-px, spec-stated

    // spec-px (spec gives a 30–40 range; 35 = midpoint)
    /// <summary>ROM `TNKCNT`: at most this many quark-dropped tanks live at once (notes §11).</summary>
    public const int TankCap = 20;
}
