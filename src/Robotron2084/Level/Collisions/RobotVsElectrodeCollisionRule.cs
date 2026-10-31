using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>
///     A grunt, a BerzerkRobot or a hulk walking onto an electrode. The robot and the electrode both die; a hulk
///     destroys the electrode and walks on.
/// </summary>
/// <remarks>
///     Original source: <c>RRP8.ASM</c> <c>PSTKIL</c>. The electrode shrivels and never bursts, and every grunt that
///     dies speeds up the grunts that are left.
/// </remarks>
internal sealed class RobotVsElectrodeCollisionRule : ICollisionRule
{
    /// <inheritdoc />
    public IEnumerable<CollisionResult> Detect(ICollisionScene collisionScene, FieldEntities fieldEntities)
    {
        foreach (var grunt in fieldEntities.Grunts)
            if (grunt.IsAlive() && FindTouchedElectrode(collisionScene, fieldEntities, grunt) is { } electrode)
                yield return new GruntHitElectrodeResult(grunt, electrode);

        foreach (var robot in fieldEntities.BerzerkRobots)
            if (robot.IsAlive() && FindTouchedElectrode(collisionScene, fieldEntities, robot) is { } electrode)
                yield return new BerzerkRobotHitElectrodeResult(robot, electrode);

        foreach (var hulk in fieldEntities.Hulks)
            if (FindTouchedElectrode(collisionScene, fieldEntities, hulk) is { } electrode)
                yield return new HulkHitElectrodeResult(hulk, electrode);
    }

    /// <summary>Finds the first living electrode an entity is touching.</summary>
    /// <param name="collisionScene">What the rule may ask about the field.</param>
    /// <param name="fieldEntities">What is on the field.</param>
    /// <param name="walkerEntity">The grunt or hulk.</param>
    /// <returns>The electrode, or null when it touches none.</returns>
    private static Electrode? FindTouchedElectrode(ICollisionScene collisionScene, FieldEntities fieldEntities, IEntity walkerEntity)
    {
        return fieldEntities.Electrodes.FirstOrDefault(electrode => electrode.IsAlive() && collisionScene.Touches(walkerEntity, electrode));
    }
}
