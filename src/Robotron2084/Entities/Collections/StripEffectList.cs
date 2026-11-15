using Robotron2084.Entities.Models;
using Robotron2084.Tuning;

namespace Robotron2084.Entities.Collections;

/// <summary>The explosions and appear effects on the field, each cut into strips.</summary>
public sealed class StripEffectList : EntityList<StripEffect>
{
    /// <summary>Says whether one of the arcade's strip routines has a record free for another explosion or appear effect.</summary>
    /// <param name="engine">The strip routine that would run the effect.</param>
    /// <returns>True when the routine can run one more effect.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>GETBLK</c> and <c>GETAP</c> in <c>RRX7.ASM</c>, <c>RRHX4.ASM</c> and
    ///             <c>RRDX2.ASM</c>, each with its own free list.
    ///         </item>
    ///         <item>Disassembly: <c>$5B6C</c>, <c>$F03A</c> and the diagonal routine's at <c>$46B2</c> onwards.</item>
    ///     </list>
    /// </remarks>
    public bool HasRoomFor(StripEngine engine)
    {
        return StripEffectTuning.GetPoolSize(engine) is not { } poolSize ||
               this.Count(stripEffect => stripEffect.GetEngine() == engine) < poolSize;
    }
}
