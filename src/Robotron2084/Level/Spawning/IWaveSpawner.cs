namespace Robotron2084.Level.Spawning;

/// <summary>Puts one kind of thing on the field for a wave.</summary>
public interface IWaveSpawner
{
    /// <summary>Puts this wave's share of the kind on the field.</summary>
    /// <param name="context">The field being filled and what is needed to choose spots on it.</param>
    void Spawn(WaveSpawnContext context);
}
