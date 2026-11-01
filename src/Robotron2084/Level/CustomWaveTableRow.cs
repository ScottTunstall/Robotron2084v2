namespace Robotron2084.Level;

/// <summary>One row of the replacement table. The wave number is not kept, because a row's place in the table is its wave.</summary>
internal sealed record CustomWaveTableRow(
    int GruntCount,
    int HulkCount,
    int SpheroidCount,
    int QuarkCount,
    int ElectrodeCount,
    int MaxEnforcersPerSpheroid,
    int MaxTanksPerQuark);
