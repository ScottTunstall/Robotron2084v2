namespace Robotron2084.Entities.Collections;

/// <summary>The shells the tanks have fired.</summary>
public sealed class TankShellList : EntityList<TankShell>
{
    /// <summary>Gets the shells that bounced off a wall in the last tick.</summary>
    /// <returns>Those shells, in the list's order.</returns>
    public IEnumerable<TankShell> GetBouncedThisUpdate()
    {
        return this.Where(shell => shell.BouncedThisUpdate);
    }
}
