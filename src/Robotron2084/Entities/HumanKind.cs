namespace Robotron2084.Entities;

/// <summary>Which family member this is: Mikey, Mommy or Daddy.</summary>
/// <remarks>ROM: KID, MOM and DAD (RRH11.ASM). This version of the game calls them Mikey, Mommy and Daddy.</remarks>
public enum HumanKind
{
    /// <summary>Mikey, the smallest of the three.</summary>
    Mikey,

    /// <summary>Mommy.</summary>
    Mommy,

    /// <summary>Daddy.</summary>
    Daddy,
}
