namespace Robotron2084.Level;

/// <summary>How many people are playing, and whether two of them play together or take turns.</summary>
/// <remarks>
///     The arcade has one two-player game, with both players on the field at once. The port plays two players as
///     <see cref="TwoPlayerAlternate" /> and does not yet have the arcade's game; <see cref="TwoPlayerSimultaneous" /> can
///     be chosen
///     but plays as <see cref="TwoPlayerAlternate" /> (notes §101).
/// </remarks>
public enum GameMode
{
    /// <summary>One player.</summary>
    OnePlayer,

    /// <summary>Two players who take turns, each keeping their own score, men and wave.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>PLEND</c>, which passes the
    ///             turn on when a man is lost
    ///         </item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    TwoPlayerAlternate,

    /// <summary>
    ///     Two players on the field at the same time, as in the arcade. It is not built yet, so it plays as
    ///     <see cref="TwoPlayerAlternate" /> (notes §101).
    /// </summary>
    TwoPlayerSimultaneous
}
