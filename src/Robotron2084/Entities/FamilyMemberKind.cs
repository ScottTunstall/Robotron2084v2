namespace Robotron2084.Entities;

/// <summary>Which family member this is: Mikey, Mommy or Daddy.</summary>
/// <remarks>
///     This version of the game calls them Mikey, Mommy and Daddy.
///     <list type="bullet">
///         <item>Original source: <c>RRH11.ASM</c>, where they are <c>KID</c>, <c>MOM</c> and <c>DAD</c></item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>), which gives Mikey,
///             Mommy and Daddy their handlers at <c>$0330</c>, <c>$0335</c> and <c>$033A</c>
///         </item>
///     </list>
/// </remarks>
public enum FamilyMemberKind
{
    /// <summary>Mikey, the smallest of the three.</summary>
    Mikey,

    /// <summary>The family member the attract screen calls Mommy.</summary>
    Mommy,

    /// <summary>The family member the attract screen calls Daddy.</summary>
    Daddy
}
