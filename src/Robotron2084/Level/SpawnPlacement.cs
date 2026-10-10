using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Chooses where on the playfield the things at the start of a wave are put.</summary>
public sealed class SpawnPlacement
{
    /// <summary>
    ///     How many port pixels the search for a spot moves on by each time when it has to check them all. It is small
    ///     enough to find a gap between two obstacles.
    /// </summary>
    private const int GridScanStep = 4;

    /// <summary>The number of sides on the roll that is checked against a percentage.</summary>
    private const int PercentSides = 100;

    /// <summary>How big a spot to look for when the thing being put down does not say, in port pixels.</summary>
    private static readonly int EntitySize =
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EntitySizeArcadePixels);

    /// <summary>The play area inside the wall, in port pixels.</summary>
    private readonly Rectangle _playfieldBounds;

    /// <summary>The field's random source.</summary>
    private readonly Random _random;

    /// <summary>Makes a placement that chooses spots inside a play area.</summary>
    /// <param name="random">The field's random source.</param>
    /// <param name="playfieldBounds">The play area inside the wall.</param>
    public SpawnPlacement(Random random, Rectangle playfieldBounds)
    {
        _random = random;
        _playfieldBounds = playfieldBounds;
    }

    /// <summary>Finds a random spot that a standard-sized thing could be put on.</summary>
    /// <param name="isAcceptable">Says whether a spot's box is acceptable.</param>
    public IntVector2 FindSpawnPoint(Func<Rectangle, bool> isAcceptable)
    {
        return FindSpawnPoint(isAcceptable, EntitySize);
    }

    /// <summary>Finds a random spot, wholly inside the play area, that is acceptable for a square thing of a given size.</summary>
    /// <param name="isAcceptable">Says whether a spot's box is acceptable.</param>
    /// <param name="size">The side of the square, in port pixels.</param>
    /// <param name="candidateSource">Offers a spot to try each time, or null to try any spot.</param>
    /// <remarks>
    ///     It tries random spots up to <see cref="SpawnTuning.SpawnPlacementMaxAttempts" /> times, then checks the whole play
    ///     area spot by spot. Either way it
    ///     only returns a spot that <paramref name="isAcceptable" /> accepts, so that nothing is put on top of an electrode
    ///     (notes §88). Only if no spot at all is
    ///     acceptable does it return a random one.
    /// </remarks>
    public IntVector2 FindSpawnPoint(Func<Rectangle, bool> isAcceptable, int size,
        Func<IntVector2?>? candidateSource = null)
    {
        for (var attempt = 0; attempt < SpawnTuning.SpawnPlacementMaxAttempts; attempt++)
        {
            var candidate = candidateSource?.Invoke() ?? PickRandomPointInside(size);
            if (isAcceptable(new Rectangle(candidate.X, candidate.Y, size, size))) return candidate;
        }

        // Last resort: one pixel at a time so a gap between two obstacles cannot be stepped over.
        for (var y = _playfieldBounds.Y; y + size <= _playfieldBounds.Bottom; y += GridScanStep)
        for (var x = _playfieldBounds.X; x + size <= _playfieldBounds.Right; x += GridScanStep)
            if (isAcceptable(new Rectangle(x, y, size, size)))
                return new IntVector2(x, y);

        // Nothing fits (a degenerate field): a predicate failure here must still return SOMETHING.
        return PickRandomPointInside(size);
    }

    /// <summary>Finds a random spot that is far enough from a point, such as where the player starts.</summary>
    /// <param name="point">The point to keep away from, in port pixels.</param>
    /// <param name="minDistanceArcadePixels">How far away the spot must be, in arcade pixels.</param>
    /// <param name="isAlsoAcceptable">A further test of a candidate's box; null when being far enough is all that is asked.</param>
    public IntVector2 FindSpawnPointAwayFrom(IntVector2 point, int minDistanceArcadePixels,
        Func<Rectangle, bool>? isAlsoAcceptable = null)
    {
        return FindSpawnPoint(box =>
            IsFarEnough(box, point, minDistanceArcadePixels) && (isAlsoAcceptable?.Invoke(box) ?? true));
    }

    /// <summary>Finds a spot for a spheroid (see <see cref="FindSpheroidSpawnPoint" />) that is far enough from a point.</summary>
    /// <param name="point">The point to keep away from, in port pixels.</param>
    /// <param name="minDistanceArcadePixels">How far away the spot must be, in arcade pixels.</param>
    public IntVector2 FindSpheroidSpawnPointAwayFrom(IntVector2 point, int minDistanceArcadePixels)
    {
        return FindSpheroidSpawnPoint(box => IsFarEnough(box, point, minDistanceArcadePixels));
    }

    /// <summary>Finds a spot for a spheroid. Spheroids like to start near a wall, so most of the spots tried are close to one.</summary>
    /// <param name="isAcceptable">Says whether a spot's box is acceptable.</param>
    /// <remarks>
    ///     The share of spots tried near a wall is <see cref="SpheroidTuning.NearWallBiasPercent" />, and how near is
    ///     <see cref="SpheroidTuning.NearWallBiasDistance" />. The rest are anywhere.
    /// </remarks>
    public IntVector2 FindSpheroidSpawnPoint(Func<Rectangle, bool> isAcceptable)
    {
        return FindSpawnPoint(isAcceptable, EntitySize, () => PickRandomSpheroidCandidate());
    }

    /// <summary>Says whether a candidate box's top-left corner is more than the given distance from a point.</summary>
    /// <param name="box">The candidate's box, in port pixels.</param>
    /// <param name="point">The point to keep away from.</param>
    /// <param name="minDistanceArcadePixels">The distance, in arcade pixels.</param>
    private static bool IsFarEnough(Rectangle box, IntVector2 point, int minDistanceArcadePixels)
    {
        return new IntVector2(box.X, box.Y).IsFartherThan(point,
            ScreenSize.ToPortPixelsFromArcadePixels(minDistanceArcadePixels));
    }

    /// <summary>Picks a random spot where a square of the given size fits inside the play area.</summary>
    /// <param name="size">The side of the square, in port pixels.</param>
    private IntVector2 PickRandomPointInside(int size)
    {
        return new IntVector2(
            _random.Next(_playfieldBounds.X, _playfieldBounds.Right - size),
            _random.Next(_playfieldBounds.Y, _playfieldBounds.Bottom - size));
    }

    /// <summary>Picks a spot to try for a spheroid. Usually it is near a wall.</summary>
    private IntVector2 PickRandomSpheroidCandidate()
    {
        if (_random.Next(PercentSides) >= SpheroidTuning.NearWallBiasPercent) return PickRandomPointInside(EntitySize);

        var bias = ScreenSize.ToPortPixelsFromArcadePixels(SpheroidTuning.NearWallBiasDistance);
        var inner = _playfieldBounds;
        var x = _random.Next(inner.X, inner.Right - EntitySize);
        var y = _random.Next(inner.Y, inner.Bottom - EntitySize);
        switch ((Edge)_random.Next(Enum.GetValues<Edge>().Length))
        {
            case Edge.Top:
                y = _random.Next(inner.Y, inner.Y + bias);
                break;

            case Edge.Bottom:
                y = _random.Next(inner.Bottom - bias - EntitySize, inner.Bottom - EntitySize);
                break;

            case Edge.Left:
                x = _random.Next(inner.X, inner.X + bias);
                break;

            default:
                x = _random.Next(inner.Right - bias - EntitySize, inner.Right - EntitySize);
                break;
        }

        return new IntVector2(x, y);
    }

    /// <summary>The four sides of the play area that a spheroid may be put against.</summary>
    private enum Edge
    {
        Top,
        Bottom,
        Left,
        Right
    }
}
