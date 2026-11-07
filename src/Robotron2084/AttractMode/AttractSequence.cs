namespace Robotron2084.AttractMode;

/// <summary>
///     A part of the attract movie that the page script can wait for, however long it takes, so that the next part
///     never starts before it is done (<see cref="PageScriptOpcodes.WaitUntilDone" />). What "done" means for each is
///     in <see cref="AttractSequenceDoneRules" />.
/// </summary>
/// <remarks>Port only: the arcade's page script waits by time alone (notes §147).</remarks>
public enum AttractSequence
{
    /// <summary>The family page: Mommy, Daddy and Mikey walk on.</summary>
    Family,

    /// <summary>The grunts page: the hero shoots 14 grunts.</summary>
    Grunts,

    /// <summary>The hulk page: the hulk catches Daddy and Mikey.</summary>
    Hulk,

    /// <summary>The spheroid page: the spheroid, the quark, and the tank and enforcer they make.</summary>
    SpheroidsAndQuarks,

    /// <summary>The electrodes page: the hero shoots the score posts, and walks into the last one.</summary>
    Electrodes
}
