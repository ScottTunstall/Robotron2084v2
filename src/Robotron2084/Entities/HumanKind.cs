namespace Robotron2084.Entities;

/// <summary>Which of the three rescuable family members a human is.</summary>
/// <remarks>ROM: KID, MOM and DAD (RRH11.ASM); this port calls them Mikey, Mommy and Daddy.</remarks>
public enum HumanKind
{
    /// <summary>Mikey — the smallest of the three (8x11 px).</summary>
    Mikey,

    /// <summary>Mommy.</summary>
    Mommy,

    /// <summary>Daddy.</summary>
    Daddy,
}
