namespace Robotron2084.Entities;

/// <summary>
/// The three family members the player rescues, and the art and collision box each one
/// uses.
/// </summary>
/// <remarks>
/// The ROM's own names for them are KID, MOM and DAD (RRH11.ASM, the humans-and-hulks
/// file); the arcade's messages and this port call them Mikey, Mummy and Daddy.
/// </remarks>
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
