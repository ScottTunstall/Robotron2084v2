using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     Each shape of electrode is its own size, as in the arcade (disassembly <c>$3B05</c> onwards). The "2084" electrode
///     of every tenth wave is 18 by 7 arcade pixels, not the usual 10 by 9.
/// </summary>
public sealed class ElectrodeSizeTests
{
    [Theory]
    [InlineData(1, 10, 9)]
    [InlineData(5, 6, 9)] // the thin one
    [InlineData(8, 10, 10)]
    [InlineData(9, 10, 9)]
    [InlineData(10, 18, 7)] // "2084"
    [InlineData(20, 18, 7)]
    public void AnElectrodesBox_IsTheSizeOfItsWavesShape(int wave, int arcadeWidth, int arcadeHeight)
    {
        var electrode = new Electrode(TestSprites.Shared, new IntVector2(100, 100), wave);

        var bounds = electrode.GetBounds();

        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(arcadeWidth), bounds.Width);
        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(arcadeHeight), bounds.Height);
    }

    [Fact]
    public void ThereIsASizeForEveryShape()
    {
        Assert.Equal(
            WavePaletteTables.ElectrodeVariantByWaveMod10.Max() + 1,
            CollisionSizes.ElectrodeCollisionSizeByVariant.Length);
    }

    [Fact]
    public void TheWide2084Electrodes_AreAllPutInsideTheWall()
    {
        var field = new PlayFieldBuilder()
            .WithParameters(new LevelParameters(10, ElectrodeCount: 60))
            .Build();
        var bounds = field.GetPlayfieldBounds();

        Assert.All(field.Entities.Electrodes, electrode =>
        {
            var box = electrode.GetBounds();
            Assert.True(box.X >= bounds.X && box.Right <= bounds.Right, $"electrode at x {box.X} to {box.Right}");
            Assert.True(box.Y >= bounds.Y && box.Bottom <= bounds.Bottom, $"electrode at y {box.Y} to {box.Bottom}");
        });
    }
}
