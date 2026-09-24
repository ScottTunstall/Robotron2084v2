using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
/// Notes 28 (playtest round 10: "the quarks are WAY too faast and the tanks
/// aren't 'spawned' as they should be") — pins the arcade behaviour. The quark
/// half was re-decoded from the Gospel in notes §51: the R5-based waypoint
/// model (speed proportional to distance, capped) was WRONG and made the quark
/// a dart; <c>SQVEL</c> has no distance term at all.
/// - quark = a random-speed DRIFT (RND(1..SQSPD) per axis, re-rolled every
///   RND(1..32) beats), never a constant-speed chase of the player;
/// - tank birth = quark position + (4, 12) screen px, clamped in-field, and
///   the tank picks its destination immediately (moves on its first frame).
/// </summary>
public sealed class QuarkTankBehaviourTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int seed) =>
        new(TestSprites.Shared, 
            new LevelParameters(
                LevelNumber: 1,
                SpheroidCount: 0,
                MaxDropsX2: 10,
                SpheroidDropDelay: 30),
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(seed),
            startingLives: 3);

    [Fact]
    public void Quark_DriftsSlowly_WithinTheField()
    {
        PlayField field = CreateField(1);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        var quark = new Quark(TestSprites.Shared, new IntVector2(bounds.X + 20, bounds.Y + 8), new Random(4), maxDropsX2: 10, dropDelayRomTicks: 60);
        field.AddQuark(quark);

        IntVector2 prev = quark.Position;
        int maxAxisSeen = 0;
        int aliveTicksSampled = 0;
        int pathLength = 0;
        bool moved = false;

        for (int tick = 0; tick < 400; tick++)
        {
            field.Update(Frame());
            if (quark.LifeState != EntityLifeState.Alive)
            {
                break;
            }

            aliveTicksSampled++;
            int dx = Math.Abs(quark.Position.X - prev.X);
            int dy = Math.Abs(quark.Position.Y - prev.Y);
            moved |= dx + dy > 0;
            pathLength += dx + dy;

            Assert.True(quark.Bounds.X >= bounds.X && quark.Bounds.Right <= bounds.Right,
                $"tick {tick}: quark left the playfield horizontally");
            Assert.True(quark.Bounds.Y >= bounds.Y && quark.Bounds.Bottom <= bounds.Bottom,
                $"tick {tick}: quark left the playfield vertically");
            maxAxisSeen = Math.Max(maxAxisSeen, Math.Max(dx, dy));

            prev = quark.Position;
        }

        Assert.True(aliveTicksSampled >= 50, $"quark died after only {aliveTicksSampled} ticks");
        Assert.True(moved, "quark never moved");

        // ROM SQVEL: RND(1..SQSPD) x 4 (X) / x 8 (Y) in 1/256-COLUMN (X) and
        // 1/256-ROW (Y) per FRAME. A column is 2 px, which is exactly what the x4
        // vs x8 asymmetry cancels, so BOTH axes are the same speed on screen: at
        // SQSPD 60 the peak is 2.6 position units/tick (1.3 arcade px) per axis.
        // Measured over 600 ticks x 5 seeds: 1.05-1.28 units/tick/axis average
        // (31-38 arcade px/s - the quark crosses the field in ~7 s) and a peak
        // step of 3-4 units. 4 is legitimate: the roll can reach SQSPD 60 AND the
        // sub-pixel carry adds up to another whole unit -- so the peak bound is a
        // dart guard, not a precise pin; the AVERAGE is the real assertion.
        // The port used to derive the speed from the DISTANCE to a waypoint (the
        // R5-only decode), which darted at up to +/-16 px/tick -- the author's
        // "The quarks are WAY too fast.", and would blow both bounds below.
        // Both bounds are in port px, so they follow the render scale: the peak step stays
        // under 2 arcade px on one axis, and the average path under 3.5 px a tick at 2x.
        Assert.True(maxAxisSeen <= ScreenSize.Scaled(2), $"axis step {maxAxisSeen} is too large for a sub-pixel drift");
        Assert.True(pathLength <= aliveTicksSampled * 3.5 * ScreenSize.SpecScale,
            $"average path {pathLength / (double)aliveTicksSampled:F2} units/tick — that is a dart, not a drift");
    }

    [Fact]
    public void Quark_DropsTank_AtQuarkPositionPlusBirthOffset()
    {
        PlayField field = CreateField(1);

        // Random(10): first Next(2) == 1 → exactly one tank to drop.
        var quark = new Quark(TestSprites.Shared, new IntVector2(300, 200), new Random(10), maxDropsX2: 1, dropDelayRomTicks: 1);
        field.AddQuark(quark);

        // Robots are frozen during the 2 s player start grace (120 ticks); the
        // first drop is due within 2 ticks after it ends.
        for (int tick = 0; tick < 200 && field.TankCount == 0; tick++)
        {
            field.Update(Frame());
        }

        Tank tank = Assert.Single(field.Tanks);
        // The drop happens after the quark's own move in the same Update, so at
        // loop exit quark.Position IS the position at the moment of the drop.
        // The dropped tank is then BORN in place (MTANK) and does not move while
        // it grows, so the offset is exact — not the +-1 the old "moves on its
        // first frame" behaviour needed.
        // TNKDRP: +2 COLUMNS and +6 ROWS, with the row decremented first unless the
        // quark sits on the top wall — this quark is mid-field, so it is +5 rows.
        IntVector2 spawn = quark.Position + new IntVector2(
            ScreenSize.Columns(GameplayConstants.TankBirthOffsetColumns),
            ScreenSize.Scaled(GameplayConstants.TankBirthOffsetRowsOffTopWall));
        Assert.Equal(spawn, tank.Position);
    }

    [Fact]
    public void Tank_IsBornBeforeItMoves()
    {
        PlayField field = CreateField(1);

        // Robots are frozen during the 2 s player start grace — burn it off so
        // the tank's birth runs unfrozen.
        for (int tick = 0; tick < 130; tick++)
        {
            field.Update(Frame());
        }

        field.SpawnTank(new IntVector2(400, 200)); // 80 px right of the player start
        Tank tank = Assert.Single(field.Tanks);
        IntVector2 start = tank.Position;

        // ROM MTANK ("MINI TANK GROW"): a dropped tank plays four mini-tank
        // pictures at NAP 12 ROM frames each — 48 ROM frames = 4.8x12 = 57.6
        // port ticks — and it does not move, aim or fire until they are done.
        // Author, 2026-09-16: "tanks spawn instantly whereas they are 'born'
        // like the enforcer." The old test asserted movement on the FIRST frame,
        // which was the wave-start TNKSTV path this port never uses.
        int bornTicks = GameplayConstants.TankGrowSteps * GameplayConstants.TankGrowRomFrames * 6 / 5;

        for (int tick = 0; tick < bornTicks - 1; tick++)
        {
            field.Update(Frame());
            Assert.True(tank.IsBeingBorn, $"tick {tick + 1}: the birth ended early");
        }

        // It finishes growing.
        while (tank.IsBeingBorn)
        {
            field.Update(Frame());
        }

        // MTANK walks the mini tank up-left as it grows: the four pictures' own
        // (dx,dy) come to -2 columns and -6 rows (notes §53), so the full 14x16
        // tank ends up centred on the drop point instead of hanging off it. The
        // tolerance is one aim step (the update that ends the birth also lets the
        // tank move itself for the first time).
        IntVector2 grown = start + new IntVector2(-ScreenSize.Scaled(4), -ScreenSize.Scaled(6));
        Assert.InRange(tank.Position.X, grown.X - 2, grown.X + 2);
        Assert.InRange(tank.Position.Y, grown.Y - 2, grown.Y + 2);

        // From here it is a normal tank, and it moves one pixel per axis per BEAT
        // (TNKSPD 2 + 1 = 3 ROM frames ≈ 4 ticks) — not every tick like the port
        // used to.
        IntVector2 beforeStep = tank.Position;
        for (int tick = 0; tick < 8 && tank.Position == beforeStep; tick++)
        {
            field.Update(Frame());
        }

        Assert.NotEqual(beforeStep, tank.Position);
    }

    [Fact]
    public void Tank_BirthIsClampedInsideThePlayfield()
    {
        PlayField field = CreateField(1);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        field.SpawnTank(new IntVector2(bounds.Right + 100, bounds.Bottom + 100));

        Tank tank = Assert.Single(field.Tanks);
        Assert.True(tank.Bounds.X >= bounds.X && tank.Bounds.Right <= bounds.Right, "tank not inside horizontally");
        Assert.True(tank.Bounds.Y >= bounds.Y && tank.Bounds.Bottom <= bounds.Bottom, "tank not inside vertically");
    }

    [Fact]
    public void Tank_TreadAdvancesOnABeatAndNeverOtherwise()
    {
        PlayField field = CreateField(1);

        // Spawned inside the 2 s player start grace, so the tank is frozen: no robot
        // can beat while frozen, so the tread cannot move.
        field.SpawnTank(new IntVector2(400, 200));
        Tank tank = Assert.Single(field.Tanks);

        int treadAtSpawn = tank.TreadFrameIndex;
        for (int tick = 0; tick < 20; tick++)
        {
            field.Update(Frame());
            Assert.Equal(treadAtSpawn, tank.TreadFrameIndex);
        }

        // ROM MTANK: the tank plays four birth pictures before it can move, aim or
        // fire, so no beat happens while it is being born either.
        while (tank.IsBeingBorn)
        {
            field.Update(Frame());
            Assert.Equal(treadAtSpawn, tank.TreadFrameIndex);
        }

        // ROM TANK3 advances the tread picture on a BEAT, and a beat lands every 3rd
        // or 4th tick (TNKSPD 2 + 1 = 18 timer units, earned 5 a tick), so the
        // tread must run again well within 20 ticks. The port also bumped the counter
        // on every tick, which span the tread at twice the arcade rate -- between the
        // beats as well as on them -- and kept running it while the tank was frozen
        // and while it was still being born.
        bool advanced = false;
        for (int tick = 0; tick < 20 && !advanced; tick++)
        {
            field.Update(Frame());
            advanced = tank.TreadFrameIndex != treadAtSpawn;
        }

        Assert.True(advanced, "the tread never advanced after the tank was born");
    }
}