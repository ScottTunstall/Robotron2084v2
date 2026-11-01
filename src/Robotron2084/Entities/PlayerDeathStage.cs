using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The stages of the death animation: a white flash, a coloured flash, then the fade to black.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRF.ASM</c> <c>PDEATH</c>.</item>
///         <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
///     </list>
///     The white flash and the coloured flash are repeated several times before the fade. The fade is made by changing
///     the colour of one palette index (<see cref="PlayerTuning.PlayerDeathFadePaletteIndex" />).
/// </remarks>
internal enum PlayerDeathStage
{
    /// <summary>The white flash.</summary>
    White,

    /// <summary>A flash in a colour picked at random.</summary>
    Colour,

    /// <summary>The fade to black.</summary>
    Fade
}
