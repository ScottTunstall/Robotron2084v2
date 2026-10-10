using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The hand-authored patterns must stay correct at ANY PortPixelsPerArcadePixel: each Build*
/// returns PatternSize×PatternSize pixels (the 16 by 16 arcade pixel entity box ×
/// PortPixelsPerArcadePixel) with the shape centred — centre pixel filled, corners empty —
/// so a resolution change (PortPixelsPerArcadePixel 2 → 3 → 4…) never mis-renders them.
/// </summary>
public sealed class SpriteFactoryPatternTests
{
    public static IEnumerable<object[]> AllPatterns()
    {
        yield return new object[] { "Player", (Func<Color, Color[]>)SpriteFactory.BuildPlayerPattern };
        yield return new object[] { "Electrode", (Func<Color, Color[]>)SpriteFactory.BuildElectrodePattern };
        yield return new object[] { "Grunt", (Func<Color, Color[]>)SpriteFactory.BuildGruntPattern };
        yield return new object[] { "Hulk", (Func<Color, Color[]>)SpriteFactory.BuildHulkPattern };
        yield return new object[] { "Spheroid", (Func<Color, Color[]>)SpriteFactory.BuildSpheroidPattern };
        yield return new object[] { "Enforcer", (Func<Color, Color[]>)SpriteFactory.BuildEnforcerPattern };
        yield return new object[] { "Quark", (Func<Color, Color[]>)SpriteFactory.BuildQuarkPattern };
        yield return new object[] { "Tank", (Func<Color, Color[]>)SpriteFactory.BuildTankPattern };
    }

    [Theory]
    [MemberData(nameof(AllPatterns))]
    public void Pattern_IsSizedToSpecBox_Centred_AndCornerTransparent(string name, Func<Color, Color[]> build)
    {
        int size = ScreenSize.ToPortPixels(CollisionSizes.EntitySizeArcadePixels);
        Color[] pattern = build(Color.White);

        Assert.True(pattern.Length == size * size, $"{name} pattern must be {size}x{size} at the current PortPixelsPerArcadePixel");
        Assert.True(pattern[(size / 2) * size + size / 2] == Color.White, $"{name} centre pixel must be filled");
        Assert.True(pattern[0] == Color.Transparent, $"{name} corner must be transparent");
    }
}
