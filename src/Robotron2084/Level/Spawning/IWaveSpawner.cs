namespace Robotron2084.Level.Spawning;

/// <summary>One way of putting a kind of thing on the field at the start of a wave. Each kind has its own.</summary>
/// <remarks>
///     The arcade gives every kind its own start routine (<c>BRNSTV</c> for the brains, <c>HUMSTV</c> for the
///     family, and so on); a kind's row in <see cref="RobotKinds" /> names the one it uses.
/// </remarks>
public interface IWaveSpawner
{
    /// <summary>Puts this wave's share of the kind on the field.</summary>
    /// <param name="context">The field being filled and what is needed to choose spots on it.</param>
    void Spawn(WaveSpawnContext context);
}
