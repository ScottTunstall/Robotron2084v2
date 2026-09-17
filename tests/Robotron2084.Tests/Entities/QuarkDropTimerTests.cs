using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
/// The quark's tank-drop TIMER (notes §87). The drop countdown is only advanced on the pass
/// that wraps the quark's idle animation — five pictures, so six bodies — which is why the
/// first tank arrives seconds after the quark does rather than on its second body.
/// </summary>
public sealed class QuarkDropTimerTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int seed) =>
        new(
            new LevelParameters(LevelNumber: 7, SpheroidCount: 0, MaxDropsX2: 10, SpheroidDropDelay: 18,
                QuarkDropDelay: 16),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(seed),
            startingLives: 3);

    [Fact]
    public void TheFirstTankWaitsForWholeIdleAnimationCycles()
    {
        // SQUARE reads `ADDD #4 / CMPD #SQP4 / BLS SQ1` and puts `DEC PD2,U` on the WRAP
        // branch, so PD2 counts CYCLES of the idle animation: five pictures to walk plus the
        // pass that wraps = six bodies. With TDPTIM = 1 (the shortest first timer the ROM can
        // roll) the first tank is therefore due six bodies after the quark starts running —
        // never on the next body, which is what counting bodies did and why the author saw
        // "the Quarks are dropping tanks WAY too quickly".
        PlayField field = CreateField(2);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        var quark = new Quark(
            new IntVector2(bounds.X + 100, bounds.Y + 60), new Random(4),
            maxDropsX2: 10, dropDelayRomTicks: 1, quarkSpeedRom: 50);
        field.AddQuark(quark);

        int tick = 0;
        for (; tick < 800 && field.TankCount == 0; tick++)
        {
            field.Update(Frame());
        }

        int bodyTicks = (int)Math.Ceiling(GameplayConstants.QuarkBodyRomTicks * 1.2); // 6/5 a frame
        int oneCycle = (GameplayConstants.QuarkTravelFrames + 1) * bodyTicks;

        Assert.True(field.TankCount > 0, "the quark must eventually drop a tank");
        Assert.True(tick > oneCycle,
            $"the first tank came {tick} ticks after spawn; one idle cycle is {oneCycle}");
    }

    [Fact]
    public void TheIdlePhaseSurvivesManyBodiesWithoutDroppingAnything()
    {
        // The corollary: with the ROM's own first timer for a wave (TDPTIM 16 at wave 7) the
        // quark can idle for a long time. Measured over five seeds before §87 the FIRST drop
        // landed within 0-47 ticks; it now needs whole cycles of six bodies, so a quark that
        // spawns mid-field cannot have dropped a tank after its first body.
        PlayField field = CreateField(1);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        var quark = new Quark(
            new IntVector2(bounds.X + 100, bounds.Y + 60), new Random(11),
            maxDropsX2: 10, dropDelayRomTicks: 16, quarkSpeedRom: 50);
        field.AddQuark(quark);

        for (int tick = 0; tick < GameplayConstants.PortTicksCeil(GameplayConstants.QuarkBodyRomTicks) + 2; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(0, field.TankCount);
        Assert.Equal(EntityLifeState.Alive, quark.LifeState);
    }
}
