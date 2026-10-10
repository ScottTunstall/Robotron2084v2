using Robotron2084.Core;
using Robotron2084.Level;

namespace Robotron2084.Tests;

/// <summary>
///     Works out how many port ticks the start of a wave lasts, for tests that are about what happens once the game
///     is live.
/// </summary>
internal static class WaveStartTicks
{
    /// <summary>
    ///     The port ticks from a field being made to the first tick on which the game is live: the arcade's
    ///     <c>CLR STATUS</c> at <c>PLS2</c> (R5 $289A). The robots first act on that tick.
    /// </summary>
    public static int UntilLive(PlayField field)
    {
        return ArcadeClock.ToPortTicksRoundedUp(field.GetLiveRomFrames());
    }

    /// <summary>The port ticks from a field being made to the first tick on which the player is drawn: <c>PLS1</c> (R5 $2874).</summary>
    public static int UntilPlayerAppears(PlayField field)
    {
        return ArcadeClock.ToPortTicksRoundedUp(field.GetPlayerAppearRomFrames());
    }
}
