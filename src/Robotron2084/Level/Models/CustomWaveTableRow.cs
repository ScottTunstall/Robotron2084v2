namespace Robotron2084.Level.Models;

/// <summary>One row of the replacement table. The wave number is not kept, because a row's place in the table is its wave.</summary>
/// <param name="GruntCount">How many grunts the wave has.</param>
/// <param name="HulkCount">How many hulks.</param>
/// <param name="SpheroidCount">How many spheroids.</param>
/// <param name="QuarkCount">How many quarks.</param>
/// <param name="ElectrodeCount">How many electrodes.</param>
/// <param name="MaxEnforcersPerSpheroid">The most enforcers a spheroid may drop.</param>
/// <param name="MaxTanksPerQuark">The most tanks a quark may drop.</param>
internal sealed record CustomWaveTableRow(
    int GruntCount,
    int HulkCount,
    int SpheroidCount,
    int QuarkCount,
    int ElectrodeCount,
    int MaxEnforcersPerSpheroid,
    int MaxTanksPerQuark);
