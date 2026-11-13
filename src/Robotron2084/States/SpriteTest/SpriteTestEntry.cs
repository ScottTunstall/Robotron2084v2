using Robotron2084.Core;

namespace Robotron2084.States.SpriteTest;

/// <summary>One sprite on the SPRITE TEST page: its name, how it animates, and how it is drawn.</summary>
/// <param name="Name">The name shown in the list, in characters the arcade's small font has.</param>
/// <param name="GetCycle">
///     Gets the cycle the sprite shows when it goes a way, or when no way is held (null). The same way always gets the
///     same cycle. A null cycle means the sprite stays on the frame it is showing, as the player does when it stands
///     still.
/// </param>
/// <param name="IsSteerable">True when the way held changes what the sprite shows.</param>
public sealed record SpriteTestEntry(string Name, Func<Direction8?, SpriteTestCycle?> GetCycle, bool IsSteerable)
{
    /// <summary>How the sprite is drawn.</summary>
    public SpriteTestDrawStyle DrawStyle { get; init; } = SpriteTestDrawStyle.Normal;

    /// <summary>
    ///     The palette index of the shape, for <see cref="SpriteTestDrawStyle.Solid" /> and
    ///     <see cref="SpriteTestDrawStyle.SolidWithBackground" />.
    /// </summary>
    public int ShapePaletteIndex { get; init; }

    /// <summary>The palette index of the box behind the shape, for <see cref="SpriteTestDrawStyle.SolidWithBackground" />.</summary>
    public int BackgroundPaletteIndex { get; init; }

    /// <summary>How the sprite walks to the left in the game, or null for a sprite that does not walk.</summary>
    public SpriteTestWalkOn? WalkOn { get; init; }

    /// <summary>Makes an entry that animates the same way whichever way is held.</summary>
    /// <param name="name">The name shown in the list.</param>
    /// <param name="cycle">The cycle it shows.</param>
    public static SpriteTestEntry CreateUnsteerable(string name, SpriteTestCycle cycle)
    {
        return new SpriteTestEntry(name, _ => cycle, false);
    }
}
