using Microsoft.Xna.Framework;
using Robotron2084.Rendering;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The four player laser pictures must match the ROM byte-for-byte:
/// R5 $35BE-$35DC (LLPC/ULPC/DLLPC/ULLPC), 4 bits per pixel with the high
/// nibble = the left pixel (notes 2026-09-12 (19); author ROM check
/// 2026-09-13: 13758 3x1, 13761 1x6, 13767 3x6, 13785 3x6). The patterns are
/// authored at arcade-pixel dimensions, so the exact pixels are the fidelity
/// invariant.
/// </summary>
public sealed class PlayerLaserArtTests
{
    private const int N = 6;

    [Fact]
    public void Bar_Is6x1_Solid()
    {
        Color[] pattern = PixelArtFactory.BuildLaserBarPattern(Color.White);

        Assert.Equal(6, pattern.Length);
        Assert.All(pattern, pixel => Assert.Equal(Color.White, pixel));
    }

    [Fact]
    public void Column_Is2x6_LeftColumnOnly()
    {
        Color[] pattern = PixelArtFactory.BuildLaserColumnPattern(Color.White);

        Assert.Equal(2 * N, pattern.Length);
        for (int row = 0; row < N; row++)
        {
            Assert.Equal(Color.White, pattern[row * 2]);
            Assert.Equal(Color.Transparent, pattern[row * 2 + 1]);
        }
    }

    [Fact]
    public void DiagonalMain_Is6x6_TopLeftToBottomRight()
    {
        Color[] pattern = PixelArtFactory.BuildLaserDiagonalMainPattern(Color.White);

        Assert.Equal(N * N, pattern.Length);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                bool lit = x == y;
                Assert.Equal(lit ? Color.White : Color.Transparent, pattern[y * N + x]);
            }
        }
    }

    [Fact]
    public void DiagonalAnti_Is6x6_TopRightToBottomLeft()
    {
        Color[] pattern = PixelArtFactory.BuildLaserDiagonalAntiPattern(Color.White);

        Assert.Equal(N * N, pattern.Length);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                bool lit = x == N - 1 - y;
                Assert.Equal(lit ? Color.White : Color.Transparent, pattern[y * N + x]);
            }
        }
    }
}
