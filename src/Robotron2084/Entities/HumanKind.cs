namespace Robotron2084.Entities;

/// <summary>
/// Which of the three rescuable family members a human entity is. The three kinds differ
/// only in art, collision box size, and on-screen name — rescue behaviour is identical.
/// See <see cref="SkullMarker"/> for what happens when one is killed, and
/// <see cref="RescueScoreMarker"/> for what happens when one is saved.
/// </summary>
/// <remarks>ROM: KID, MOM and DAD (RRH11.ASM); this port calls them Mikey, Mummy and Daddy.</remarks>
public enum HumanKind
{
    /// <summary>Mikey — the smallest of the three (8x11 px).</summary>
    /// <remarks>KID in the ROM.</remarks>
    Mikey,

    /// <summary>Mummy.</summary>
    /// <remarks>MOM in the ROM.</remarks>
    Mom,

    /// <summary>Daddy.</summary>
    /// <remarks>DAD in the ROM.</remarks>
    Dad,
}
