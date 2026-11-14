using Microsoft.Xna.Framework;

namespace Robotron2084.Entities.Models;

/// <summary>The playfield as a Gorf needs to know it: where the walls are, and where the player is so that Gorf can stay clear.</summary>
public sealed record GorfPlayfield
{
    /// <summary>Makes the playfield a Gorf is built for.</summary>
    /// <param name="bounds">The inside of the wall, in port pixels. Gorf starts and ends outside it.</param>
    /// <param name="playerBoxToAvoid">
    ///     Where the player is, or null. Gorf is put at a height where its whole body, hops included, stays clear of this
    ///     box, so it does not appear on a collision course with the player.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="bounds" />, or <paramref name="playerBoxToAvoid" /> when there is one, has no width or no height.
    /// </exception>
    public GorfPlayfield(Rectangle bounds, Rectangle? playerBoxToAvoid = null)
    {
        EntityArguments.ThrowIfEmpty(bounds);
        if (playerBoxToAvoid is { } box) EntityArguments.ThrowIfEmpty(box, nameof(playerBoxToAvoid));

        Bounds = bounds;
        PlayerBoxToAvoid = playerBoxToAvoid;
    }

    /// <summary>The inside of the wall, in port pixels.</summary>
    public Rectangle Bounds { get; }

    /// <summary>Where the player is, or null when there is no box to keep clear of.</summary>
    public Rectangle? PlayerBoxToAvoid { get; }
}
