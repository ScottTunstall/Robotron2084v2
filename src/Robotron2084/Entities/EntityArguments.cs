using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace Robotron2084.Entities;

/// <summary>The checks the entities' parameter objects make on what they are given, so that nothing that makes no sense gets in.</summary>
internal static class EntityArguments
{
    /// <summary>Throws if a box has no area: a playfield, or a box to keep clear of, must have a width and a height.</summary>
    /// <param name="box">The box to check.</param>
    /// <param name="paramName">The name of the argument. The compiler fills it in.</param>
    /// <exception cref="ArgumentOutOfRangeException">The box is not wider than 0 and taller than 0.</exception>
    public static void ThrowIfEmpty(Rectangle box, [CallerArgumentExpression(nameof(box))] string? paramName = null)
    {
        if (box.Width <= 0 || box.Height <= 0)
            throw new ArgumentOutOfRangeException(paramName, box, "A box must be wider than 0 and taller than 0.");
    }
}
