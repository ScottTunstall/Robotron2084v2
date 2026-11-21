using Microsoft.Xna.Framework;
using Robotron2084.Core.Models;

namespace Robotron2084.Entities.Models;

/// <summary>What a tank shell is aimed with: where the player is, how fast shells go on this wave, and where the walls are.</summary>
public sealed record TankShellAimParams
{
    /// <summary>Makes the aim for one shell.</summary>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting. A bigger number is a faster shell.</param>
    /// <param name="playfieldBounds">The inside of the playfield wall, in port pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="shellSpeed" /> is below 0, or <paramref name="playfieldBounds" /> has no width or no height.
    /// </exception>
    public TankShellAimParams(IntVector2 playerPosition, int shellSpeed, Rectangle playfieldBounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shellSpeed);
        EntityArguments.ThrowIfEmpty(playfieldBounds);

        PlayerPosition = playerPosition;
        ShellSpeed = shellSpeed;
        PlayfieldBounds = playfieldBounds;
    }

    /// <summary>The top-left corner of the player, in port pixels.</summary>
    public IntVector2 PlayerPosition { get; }

    /// <summary>The wave's shell setting. A bigger number is a faster shell.</summary>
    public int ShellSpeed { get; }

    /// <summary>The inside of the playfield wall, in port pixels.</summary>
    public Rectangle PlayfieldBounds { get; }
}
