namespace Robotron2084.AttractMode;

/// <summary>How a character in the attract movie walks when it is told to move.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             <see cref="FamilyMember" /> and <see cref="Hulk" /> use the arcade's ANA* walker: a step every 8 arcade
///             frames, taken from the table HUMANA or HLKANA. Each entry has four animation frames, a sideways distance
///             and an up-and-down distance. <c>DYDX</c> halves the sideways distance into columns.
///         </item>
///         <item>
///             <see cref="BrainStep" /> uses the arcade's BR* walker: a fixed step size and wait taken from the
///             character's descriptor, cycling the four-entry animation frame table ANATAB.
///         </item>
///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>ANAL</c> to <c>ANASUB</c>, <c>BRL</c> to <c>BRWALK</c>, <c>ANATAB</c> and <c>DYDX</c>, with <c>RRF.ASM</c> <c>HUMANA</c> and <c>HLKANA</c> (notes §95.3 and §95.7)</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public enum MovieWalk
{
    None,
    FamilyMember,
    Hulk,
    BrainStep
}
