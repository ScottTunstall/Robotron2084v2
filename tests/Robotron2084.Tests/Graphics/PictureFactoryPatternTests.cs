using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The hand-authored patterns must stay correct at ANY SpecScale: each Build*
/// returns PatternSize×PatternSize pixels (the 16 spec-px entity box ×
/// SpecScale) with the shape centred — centre pixel filled, corners empty —
/// so a resolution change (SpecScale 2 → 3 → 4…) never mis-renders them.
/// </summary>
public sealed class PictureFactoryPatternTests
{
    public static IEnumerable<object[]> AllPatterns()
    {
        yield return new object[] { "Player", (Func<Color, Color[]>)PictureFactory.BuildPlayerPattern };
        yield return new object[] { "Electrode", (Func<Color, Color[]>)PictureFactory.BuildElectrodePattern };
        yield return new object[] { "Grunt", (Func<Color, Color[]>)PictureFactory.BuildGruntPattern };
        yield return new object[] { "Hulk", (Func<Color, Color[]>)PictureFactory.BuildHulkPattern };
        yield return new object[] { "Spheroid", (Func<Color, Color[]>)PictureFactory.BuildSpheroidPattern };
        yield return new object[] { "Enforcer", (Func<Color, Color[]>)PictureFactory.BuildEnforcerPattern };
        yield return new object[] { "Quark", (Func<Color, Color[]>)PictureFactory.BuildQuarkPattern };
        yield return new object[] { "Tank", (Func<Color, Color[]>)PictureFactory.BuildTankPattern };
    }

    [Theory]
    [MemberData(nameof(AllPatterns))]
    public void Pattern_IsSizedToSpecBox_Centred_AndCornerTransparent(string name, Func<Color, Color[]> build)
    {
        int size = ScreenSize.Scaled(CollisionSizes.EntitySizeSpecPixels);
        Color[] pattern = build(Color.White);

        Assert.True(pattern.Length == size * size, $"{name} pattern must be {size}x{size} at the current SpecScale");
        Assert.True(pattern[(size / 2) * size + size / 2] == Color.White, $"{name} centre pixel must be filled");
        Assert.True(pattern[0] == Color.Transparent, $"{name} corner must be transparent");
    }
}
