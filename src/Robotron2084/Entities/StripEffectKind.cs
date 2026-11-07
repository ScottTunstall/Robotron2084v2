namespace Robotron2084.Entities;

/// <summary>Which of the two strip effects this is: strips flying apart, or strips closing up.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>EXSTV</c> (explosion) and <c>APSTV</c> (appear) in <c>RRX7.ASM</c>, <c>HEXSTV</c> and
///             <c>HAPSTV</c> in <c>RRHX4.ASM</c>, and <c>EXSTZ</c> and <c>APSTZ</c> in <c>RRDX2.ASM</c>
///         </item>
///         <item>Disassembly: the three strip routines at <c>$5B40</c>, <c>$F000</c> and <c>$4680</c></item>
///     </list>
/// </remarks>
public enum StripEffectKind
{
    /// <summary>An explosion: the strips fly apart.</summary>
    Explode,

    /// <summary>An appear: the strips close up to make the sprite.</summary>
    Appear
}
