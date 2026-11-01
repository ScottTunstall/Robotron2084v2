namespace Robotron2084.Tuning;

/// <summary>Where the wave places its entities, how the wave generator scales them, and how many may exist at once.</summary>
public static class SpawnTuning
{
    /// <summary>How far from the player an electrode must start, in arcade pixels.</summary>
    /// <remarks>The distance comes from the arcade spec, not from a ROM instruction the port has traced.</remarks>
    public const int ElectrodeMinDistanceFromPlayer = 40;

    /// <summary>The most speed bonus that wave numbers can add to the robots of a wave.</summary>
    /// <remarks>Port only: the wave generator's own limit.</remarks>
    public const int EnemySpeedBonusMaxPerWave = 5;

    /// <summary>The most enforcers dropped by spheroids that may be alive at once.</summary>
    /// <remarks>
    ///     Original source: the ROM counter <c>ENFCNT</c> (notes §11; the notes do not record its <c>.ASM</c> file). Disassembly: <c>asm/robomame.asm</c> <c>DROP_ENFORCER</c>
    ///     (<c>$11DB</c>), <c>CMPA #$08</c> at <c>$11F8</c>.
    /// </remarks>
    public const int MaxEnforcers = 8;

    /// <summary>How far from the player a grunt must start, in arcade pixels.</summary>
    /// <remarks>The distance comes from the arcade spec, not from a ROM instruction the port has traced.</remarks>
    public const int GruntMinDistanceFromPlayer = 20;

    /// <summary>How far from the player a hulk must start, in arcade pixels.</summary>
    /// <remarks>
    ///     The arcade spec gives a range of 30 to 40, and this is the middle of it. It is not from a ROM instruction the
    ///     port has traced.
    /// </remarks>
    public const int HulkMinDistanceFromPlayer = 35;

    /// <summary>
    ///     A tank may fire while the wave's shell count is no higher than this, so twenty-one shells can be fired. Only a
    ///     laser KILL gives one back (the fizzle bug, notes §53), so late in the wave the tanks stop firing.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>CMPA #20 / LBHI TNKFX</c>. Disassembly:
    ///     <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E59</c>.
    /// </remarks>
    public const int MaxShellCount = 20;

    /// <summary>How many random spots are tried for one entity before the whole playfield is searched for a free one.</summary>
    /// <remarks>Port only: the arcade has no such search.</remarks>
    public const int SpawnPlacementMaxAttempts = 100;

    /// <summary>The most tanks dropped by quarks that may be alive at once.</summary>
    /// <remarks>
    ///     Original source: the ROM counter <c>TNKCNT</c> (notes §11; the notes do not record its <c>.ASM</c> file). Disassembly: <c>asm/robomame.asm</c>
    ///     <c>SET_TANK_SPAWN_COUNTDOWN</c> (<c>$4C24</c>), <c>CMPA #$14</c> at <c>$4C43</c>.
    /// </remarks>
    public const int MaxTanks = 20;
}
