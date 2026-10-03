using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Chooses where the wave's entities are placed on the playfield.</summary>
public sealed class SpawnPlacement
{
    /// <summary>Step, in port pixels, of the last-resort placement scan — fine enough to find a gap between obstacles.</summary>
    private const int GridScanStep = 4;

    /// <summary>The percentage rolls are out of this many.</summary>
    private const int PercentSides = 100;

    private static readonly int EntitySize = ScreenSize.ToPortPixels(CollisionSizes.EntitySizeSpecPixels);

    private readonly Rectangle _playfieldBounds;

    private readonly Random _random;

    /// <summary>Places entities inside <paramref name="playfieldBounds"/>, drawing from <paramref name="random"/>.</summary>
    /// <param name="random">The field's random source.</param>
    /// <param name="playfieldBounds">The inner play area.</param>
    public SpawnPlacement(Random random, Rectangle playfieldBounds)
    {
        _random = random;
        _playfieldBounds = playfieldBounds;
    }

    /// <summary>The four inner edges a spheroid's near-wall candidate can hug.</summary>
    private enum Edge
    {
        Top,
        Bottom,
        Left,
        Right,
    }

    /// <summary>A random spot whose entity-sized box passes <paramref name="isAcceptable"/>.</summary>
    /// <param name="isAcceptable">Tests a candidate's box.</param>
    public IntVector2 FindSpawnPoint(Func<Rectangle, bool> isAcceptable) => FindSpawnPoint(isAcceptable, EntitySize);

    /// <summary>
    /// A random spot whose <paramref name="size"/>-square box passes <paramref name="isAcceptable"/>: a candidate
    /// fully contained in the play area (that IS the "no wall overlap" check), up to
    /// <see cref="SpawnTuning.SpawnPlacementMaxAttempts"/> attempts, then a grid scan.
    ///
    /// The LAST-RESORT scan is a FINE grid and, like the random attempts, it only ever returns a spot the predicate
    /// accepts — an unchecked fallback could hand back a point inside an electrode, which is what put humans on top
    /// of one every so often (notes §88).
    /// </summary>
    /// <param name="isAcceptable">Tests a candidate's box.</param>
    /// <param name="size">The side of the box, in port pixels.</param>
    /// <param name="candidateSource">Offers a candidate per attempt; null (or a null result) means a uniform point.</param>
    public IntVector2 FindSpawnPoint(Func<Rectangle, bool> isAcceptable, int size, Func<IntVector2?>? candidateSource = null)
    {
        for (int attempt = 0; attempt < SpawnTuning.SpawnPlacementMaxAttempts; attempt++)
        {
            IntVector2 candidate = candidateSource?.Invoke() ?? RandomPointInside(size);
            if (isAcceptable(new Rectangle(candidate.X, candidate.Y, size, size)))
            {
                return candidate;
            }
        }

        // Last resort: one pixel at a time so a gap between two obstacles cannot be stepped over.
        for (int y = _playfieldBounds.Y; y + size <= _playfieldBounds.Bottom; y += GridScanStep)
        {
            for (int x = _playfieldBounds.X; x + size <= _playfieldBounds.Right; x += GridScanStep)
            {
                if (isAcceptable(new Rectangle(x, y, size, size)))
                {
                    return new IntVector2(x, y);
                }
            }
        }

        // Nothing fits (a degenerate field): a predicate failure here must still return SOMETHING.
        return RandomPointInside(size);
    }

    /// <summary>
    /// A spheroid's spot: <see cref="SpheroidTuning.NearWallBiasPercent"/> percent of the time a point
    /// within <see cref="SpheroidTuning.NearWallBiasDistance"/> of a random inner edge ("spheroids do like
    /// to start near walls"), else a uniform one.
    /// </summary>
    /// <param name="isAcceptable">Tests a candidate's box.</param>
    public IntVector2 FindSpheroidSpawnPoint(Func<Rectangle, bool> isAcceptable) =>
        FindSpawnPoint(isAcceptable, EntitySize, () => RandomSpheroidCandidate());

    private IntVector2 RandomPointInside(int size) => new(
        _random.Next(_playfieldBounds.X, _playfieldBounds.Right - size),
        _random.Next(_playfieldBounds.Y, _playfieldBounds.Bottom - size));

    private IntVector2 RandomSpheroidCandidate()
    {
        if (_random.Next(PercentSides) >= SpheroidTuning.NearWallBiasPercent)
        {
            return RandomPointInside(EntitySize);
        }

        int bias = ScreenSize.ToPortPixels(SpheroidTuning.NearWallBiasDistance);
        Rectangle inner = _playfieldBounds;
        int x = _random.Next(inner.X, inner.Right - EntitySize);
        int y = _random.Next(inner.Y, inner.Bottom - EntitySize);
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
}
