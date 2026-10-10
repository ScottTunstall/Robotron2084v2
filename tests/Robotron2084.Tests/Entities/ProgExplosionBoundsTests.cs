using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Where a prog's explosion starts. The arcade moves it left or up when the prog is close to the right wall or the
/// bottom wall (disassembly <c>PROG_COLLISION_DETECTION</c>, <c>$1F45</c> to <c>$1F53</c>).
/// </summary>
public sealed class ProgExplosionBoundsTests
{
    [Fact]
    public void AwayFromTheWalls_TheExplosionStartsAtTheProgsOwnCorner()
    {
        PlayField field = new PlayFieldBuilder().Build();
        field.SkipWaveStart();
        Rectangle bounds = field.GetPlayfieldBounds();
        var spot = new IntVector2(bounds.X + 100, bounds.Y + 100);
        var prog = new Prog(TestSprites.Shared, spot, HumanKind.Mikey, new Random(3));
        field.Entities.Progs.Add(prog);
        field.Update(new GameTime());

        Rectangle explosion = prog.GetExplosionBounds();

        Assert.Equal(prog.Position.X, explosion.X);
        Assert.Equal(prog.Position.Y, explosion.Y);
    }

    [Fact]
    public void InTheBottomRightCorner_TheExplosionIsMovedLeftAndUp()
    {
        PlayField field = new PlayFieldBuilder().Build();
        field.SkipWaveStart();
        Rectangle bounds = field.GetPlayfieldBounds();
        Rectangle mikey = new Human(TestSprites.Shared, IntVector2.Zero, HumanKind.Mikey, new Random(1)).GetBounds();
        var corner = new IntVector2(bounds.Right - mikey.Width, bounds.Bottom - mikey.Height);
        var prog = new Prog(TestSprites.Shared, corner, HumanKind.Mikey, new Random(3));
        field.Entities.Progs.Add(prog);
        field.Player.TeleportTo(new IntVector2(bounds.X + 4, bounds.Y + 4));
        field.Update(new GameTime());

        Rectangle explosion = prog.GetExplosionBounds();

        // Disassembly: the corner is held at column $8A and row $DB, against walls at $8F and $EA.
        Assert.Equal(bounds.Right - ScreenSize.ToPortPixelsFromColumns(0x8F - 0x8A), explosion.X);
        Assert.Equal(bounds.Bottom - ScreenSize.ToPortPixelsFromArcadePixels(0xEA - 0xDB), explosion.Y);
    }
}
