using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// PHASE E (arcade-fidelity-notes (18)): brains (ROM BRNORG $1AC0 — nearest
/// human target, 1px/body chase, ABAC, cruise missiles, touch-conversion to
/// progs), progs (straight-line walkers that keep the human's art/box and
/// die in a phony burst), cruise missiles (50/25/25 direction roll,
/// wall-reflected, 25 pts), plus the round-6 autofire cadence and the P
/// "skip level" port key.
/// </summary>
public sealed class PlayFieldPhaseETests
{
    private sealed class PhaseInput : IPlayerInputSource
    {
        public PlayerInputState State = default;

        public PlayerInputState Poll() => State;
    }

    private static GameTime Frame() => new(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000.0 / 60));

    /// <summary>
    /// Expires the player's 2-second start grace (RobotsFrozen) with real
    /// elapsed time — 121 frames, per SpheroidEnforcerTimingTests.
    /// </summary>
    private static void WarmUp(PlayField field)
    {
        for (int tick = 0; tick < 121; tick++)
        {
            field.Update(Frame());
        }
    }

    private static PlayField CreateField(LevelParameters parameters, IPlayerInputSource? input = null) =>
        new(parameters, input ?? new FakeInputSource(), PlayFieldSpawnTests.InnerBounds, new WallColorCycle(), new Random(99), startingLives: 3);

    private static LevelParameters BrainWave(int brains, int moms = 0, int dads = 0, int mikeys = 0) => new(
        LevelNumber: 1,
        BrainCount: brains,
        MomCount: moms,
        DadCount: dads,
        MikeyCount: mikeys);

    [Fact]
    public void Constructor_SpawnsTheWaveBrainCount()
    {
        PlayField field = CreateField(BrainWave(3));
        Assert.Equal(3, field.BrainCount);
    }

    [Fact]
    public void Brain_StepsTowardNearestHuman_OneArcadePxPerAxisPerBody()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;

        field.AddHuman(new Human(new IntVector2(inner.X + 300, inner.Y + 150), HumanKind.Mom, new Random(1)));
        WarmUp(field); // expire the start grace (RobotsFrozen)

        // 30 arcade px left of (where the human is now) → steps right/up.
        IntVector2 humanSpot = field.Humans[0].Position;
        IntVector2 brainSpot = new(humanSpot.X - 60, humanSpot.Y - 40);
        var brain = new Brain(brainSpot, new Random(2), brainSpeedRomTicks: 8, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        // Body period = PortTicks(1 + BRNSPD) = PortTicks(9) = 11 ticks
        // (notes 26: SLEEP(BRNSPD) + one body-execution vblank). In the
        // 19-tick window exactly one body runs = 1 arcade px (2 screen px)
        // per axis toward the target.
        for (int tick = 0; tick < GameplayConstants.PortTicks(16); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X + 2, brain.Position.X);
        Assert.Equal(brainSpot.Y + 2, brain.Position.Y);
    }

    [Fact]
    public void Brain_WithNoLivingHumans_ChasesThePlayer()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        IntVector2 playerSpot = field.Player.Position;

        WarmUp(field); // expire the start grace (RobotsFrozen)

        // 30 arcade px right of the player, on the player's OWN row → the
        // brain steps left at it AND one px DOWN: ROM BRN3A has no dead zone
        // on Y and compares with BHS, so "level with the target" counts as
        // "target is below". That ±1px vertical jitter is the arcade brain's
        // hover; the port used to hold the row perfectly still.
        IntVector2 brainSpot = new(playerSpot.X + 60, playerSpot.Y);
        var brain = new Brain(brainSpot, new Random(3), brainSpeedRomTicks: 8, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        // One body in the 19-tick window (period PortTicks(9) = 10, notes 26).
        for (int tick = 0; tick < GameplayConstants.PortTicks(16); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(playerSpot.X + 58, brain.Position.X);
        Assert.Equal(playerSpot.Y + 2, brain.Position.Y);
    }

    [Fact]
    public void Brain_XApproachHasADeadZone_ButYDoesNot()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        IntVector2 playerSpot = field.Player.Position;

        WarmUp(field);

        // 1 arcade px (2 screen px) to the right of the player: INSIDE the
        // ROM's ±2px X dead zone (BRNL1: dx+2 <= 4), so X must not correct —
        // but Y has no dead zone and must still step down 1 px.
        IntVector2 brainSpot = new(playerSpot.X + 2, playerSpot.Y - 100);
        var brain = new Brain(brainSpot, new Random(21), brainSpeedRomTicks: 8, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        for (int tick = 0; tick < GameplayConstants.PortTicks(12); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X, brain.Position.X);      // dead zone holds X
        Assert.Equal(brainSpot.Y + 2, brain.Position.Y);  // Y keeps closing
    }

    [Fact]
    public void Brain_DoesNotDeadlockOnAWall_SlidesAlongItInstead()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;

        WarmUp(field);

        // Player below and to the RIGHT of where the brain will sit.
        field.Player.TeleportTo(new IntVector2(inner.Right - 10, inner.Y + 260));

        // Brain flush against the RIGHT wall, so its +X step is out of bounds.
        // The Gospel's CKLIM undoes BOTH axes on failure, which pins it there
        // for good (its Y step is unconditional, so it could never even move
        // down) — the author's "the brains seem to get stuck at the bottom
        // wall". The port rejects per axis, like the ROM's own generic mover
        // (RRS22 OPB80), so the brain creeps down the wall instead.
        IntVector2 brainSpot = new(inner.Right - ScreenSize.Scaled(GameplayConstants.BrainCollisionSize.Width), inner.Y + 160);
        var brain = new Brain(brainSpot, new Random(22), brainSpeedRomTicks: 8, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        for (int tick = 0; tick < GameplayConstants.PortTicks(12); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X, brain.Position.X);          // the wall still holds
        Assert.Equal(brainSpot.Y + 2, brain.Position.Y);      // but it is not stuck
    }

    [Fact]
    public void Brain_CatchingHuman_ReprogramsItThenLeavesAProg_NoSkull()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        // The reprogram animation is robot activity, so RobotsFrozen pauses it
        // (the port's "ALL ROBOTS ARE IMMOBILE" rule) — expire the start grace
        // or the loop never advances.
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 200, inner.Y + 200);
        var human = new Human(humanSpot, HumanKind.Mom, new Random(4));
        field.AddHuman(human);

        // Corners coincident — well inside the ROM's ±3px catch reach.
        var brain = new Brain(humanSpot, new Random(5), brainSpeedRomTicks: 0, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        field.Update(Frame());

        // ROM BMUT: the brain STOPS and the pair run the 20-iteration animation
        // — the human is not a prog yet, and is off the human list.
        Assert.True(brain.IsReprogramming);
        Assert.True(human.IsBeingReprogrammed);
        Assert.Equal(0, field.ProgCount);
        Assert.Empty(field.Skulls); // ROM BRNFLG — never a skull on conversion
        Assert.Null(field.NearestHumanPositionTo(humanSpot)); // no longer a target

        // ROM BMUT's placement: just left of the brain, 2px below it.
        Assert.Equal(brain.Position.X - human.Bounds.Width - ScreenSize.Scaled(1), human.Position.X);
        Assert.Equal(brain.Position.Y + ScreenSize.Scaled(2), human.Position.Y);

        // The animation is 20 iterations x 2 redraws x 3 ROM frames = 144 ticks
        // on the exact-6ths clock (notes §52; PortTicks(3) = 3 would have made it
        // 120). Through it the brain must not move (it is a solid block, not a
        // chaser).
        IntVector2 brainSpot = brain.Position;
        for (int tick = 0; tick < 130; tick++)
        {
            field.Update(Frame());
        }

        Assert.True(brain.IsReprogramming);
        Assert.Equal(brainSpot, brain.Position);
        Assert.Equal(0, field.ProgCount);

        for (int tick = 0; tick < 25; tick++)
        {
            field.Update(Frame());
        }

        // Done: the human is gone, a PROG stands at its last position and keeps
        // its art/box, and the brain is free to move again.
        Assert.False(brain.IsReprogramming);
        Assert.NotEqual(EntityLifeState.Alive, human.LifeState);
        Assert.Equal(1, field.ProgCount);
        Assert.Equal(HumanKind.Mom, field.Progs[0].Kind);
        Assert.Empty(field.Skulls);
    }

    [Fact]
    public void Brain_KilledMidReprogram_ReleasesTheHumanAndLeavesASkull()
    {
        // ROM BRNKIL: past BMUT3 the brain frees the human and drops a skull —
        // no prog. Without this the victim would be stranded mid-animation
        // (frozen, un-rescuable, never a prog).
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 200, inner.Y + 200);
        var human = new Human(humanSpot, HumanKind.Dad, new Random(31));
        field.AddHuman(human);
        var brain = new Brain(humanSpot, new Random(32), brainSpeedRomTicks: 0, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        field.Update(Frame());
        Assert.True(brain.IsReprogramming);

        brain.Kill(); // laser hit, mid-animation
        field.Update(Frame());

        Assert.False(human.IsBeingReprogrammed);
        Assert.Equal(EntityLifeState.Dead, human.LifeState);
        Assert.Equal(0, field.ProgCount); // the conversion never completed
        Assert.Single(field.Skulls);
        Assert.False(brain.IsReprogramming);
    }

    [Fact]
    public void Brain_CatchNeedsTheCornersWithinThreePx_NotAPictureOverlap()
    {
        // ROM BRNL1 compares TOP-LEFT CORNERS within ±3px on both axes. The old
        // Bounds.Overlaps test fired whenever the 14x16 brain box touched the
        // human box — far too eager.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 200, inner.Y + 200);
        var human = new Human(humanSpot, HumanKind.Mom, new Random(14));
        field.AddHuman(human);

        // Boxes overlap (the human is well inside the brain's frame) but the
        // corners are 20px apart → the ROM's reach does not cover it.
        IntVector2 offset = new(ScreenSize.Scaled(20), 0);
        var brain = new Brain(humanSpot - offset, new Random(15), brainSpeedRomTicks: 0, fireDelayRomTicks: 40);
        field.AddBrain(brain);

        field.Update(Frame());

        Assert.False(brain.IsReprogramming);
    }

    [Fact]
    public void Prog_WalksStraightCardinalLines_InsideTheField()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        // NOT the field center: the player stands there, and prog contact
        // kills the player (RobotsFrozen would stop the simulation).
        IntVector2 spot = new(inner.X + 100, inner.Y + 100);
        var prog = new Prog(spot, HumanKind.Dad, new Random(6));
        field.AddProg(prog);
        WarmUp(field); // expire the start grace (RobotsFrozen)

        // The prog re-aims on small odds (3%/9% per body) and when blocked,
        // so over a long window it WILL turn — the arcade property is that
        // every step is straight (one axis at a time, never diagonal).
        var path = new List<IntVector2> { prog.Position };
        for (int tick = 0; tick < 600; tick++)
        {
            field.Update(new GameTime());
            Assert.True(prog.Bounds.Right <= inner.Right && prog.Bounds.Bottom <= inner.Bottom, $"tick {tick}: prog escapes {inner}");
            path.Add(prog.Position);
        }

        for (int i = 1; i < path.Count; i++)
        {
            IntVector2 step = path[i] - path[i - 1];
            Assert.True(step.X == 0 || step.Y == 0, $"tick {i}: diagonal step {step}");
        }

        Assert.NotEqual(path[0], path[^1]); // it walked somewhere
    }

    [Fact]
    public void Prog_CoversFourPixelsOnEitherAxis_PerBody()
    {
        // ROM PRGAL/PRGAR give (±2, 0) and PRGAD/PRGAU give (0, ±4) — but the X byte moves
        // OX16, a screen address, so it is TWO COLUMNS, not two pixels: a column is 2 px
        // (§55). Both axes therefore cover the same 4 px a body, and the port's old 2 px
        // horizontal step walked at half the arcade's speed (notes §87).
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        WarmUp(field);

        int sawHorizontal = 0, sawVertical = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            IntVector2 spot = new(inner.X + 60, inner.Y + 60);
            var prog = new Prog(spot, HumanKind.Dad, new Random(seed));
            field.AddProg(prog);

            for (int tick = 0; tick < GameplayConstants.PortTicksCeil(3); tick++)
            {
                field.Update(Frame());
            }

            int dx = Math.Abs(prog.Position.X - spot.X);
            int dy = Math.Abs(prog.Position.Y - spot.Y);

            // NAP 3 body (3.6 ticks): exactly one step per body, one axis only, 4 px either way.
            Assert.True(dx == 0 || dy == 0, $"seed {seed}: diagonal step {dx},{dy}");
            Assert.Equal(ScreenSize.Scaled(4), dx + dy);

            if (dx != 0)
            {
                sawHorizontal++;
            }
            else
            {
                sawVertical++;
            }

            prog.Kill();
        }

        Assert.True(sawHorizontal > 0 && sawVertical > 0, "never saw both axes exercised");
    }

    [Fact]
    public void CruiseMissile_MovesTwicePerBody_AndNeverStalls()
    {
        // ROM CMISL: one body = NAP 2, doing TWO CMMOVs of 1px each. And
        // GCMDIR's `BPL GCMDY` jumps INTO the Y block, so a missile whose X
        // is not armed always seeks on Y — there is no "both axes zero" case.
        Rectangle inner = PlayFieldSpawnTests.InnerBounds;

        int xArmed = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            var missile = new CruiseMissile(
                new IntVector2(inner.X + 200, inner.Y + 150),
                new IntVector2(inner.X + 300, inner.Y + 150),
                new Random(seed));

            Assert.NotEqual(IntVector2.Zero, missile.Velocity); // never stalls
            if (missile.Velocity.X != 0)
            {
                xArmed++;
            }
        }

        // X is armed on 50% of re-aims (SEED bit 7); Y on 75%. The port's old
        // 50/25/25 "roll" armed X on 75% — the axis bias was backwards.
        double fraction = xArmed / 400.0;
        Assert.InRange(fraction, 0.42, 0.58);
    }

    [Fact]
    public void CruiseMissile_MovesOneColumnOnXAndOneRowOnYPerCMMOV()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        WarmUp(field);

        IntVector2 spot = new(inner.X + 200, inner.Y + 150);
        var missile = new CruiseMissile(spot, field.Player.Position, new Random(7));
        field.AddCruiseMissile(missile);

        // The first body (2 x 1px CMMOV) lands on tick 4 (3 ROM frames = 3.6),
        // and the re-aim timer cannot fire that early (RND(1..7) >= 1 decrement
        // per body, re-aim on reaching 0).
        for (int tick = 0; tick < GameplayConstants.PortTicksCeil(3); tick++)
        {
            field.Update(Frame());
        }

        int dx = Math.Abs(missile.Position.X - spot.X);
        int dy = Math.Abs(missile.Position.Y - spot.Y);

        // Two CMMOVs per body: `ADDA PD4` steps the X COLUMN byte (1 column = 2
        // arcade px, notes §51) and `ADDB PD5` the Y ROW byte (1 px) — so a body is
        // 2 columns on X but only 2 rows on Y. The missile really is faster
        // horizontally than vertically; there is no halving here as there is for
        // the player and the tank.
        Assert.True(dx is 0 || dx == ScreenSize.Scaled(4), $"dx {dx} is not 0 or 4 arcade px");
        Assert.True(dy is 0 || dy == ScreenSize.Scaled(2), $"dy {dy} is not 0 or 2 arcade px");
        Assert.True(dx != 0 || dy != 0, "missile did not move at all in its first body");
    }

    [Fact]
    public void CruiseMissile_DragsARollingNineMarkTail_ThatDiesWithIt()
    {
        // ROM CMMOV writes video memory directly: `$AAAA` (a 16-BIT write, and the
        // address space is column-major with 2 px per byte, so it paints a 2x2 px
        // block of slot 10) at the new coordinate, and `$DDDD` (slot 13) at the one
        // it left.
        // The apparent "8 point storage" is a RING OF NINE whose oldest entry
        // is erased off the screen EVERY STEP, so the tail is a fixed nine
        // marks, never a snake across the field (author: "the trail is too
        // long"), and CMKIL then wipes the remaining nine.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 200, inner.Y + 150);
        var missile = new CruiseMissile(spot, field.Player.Position, new Random(7));
        field.AddCruiseMissile(missile);

        // Two bodies = 4 CMMOVs = 4 marks (the missile flies on while the
        // player's start grace freezes the robots). Bodies land on ticks 4 and 8.
        for (int tick = 0; tick < GameplayConstants.PortTicksCeil(3) * 2; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(4, missile.Trail.Count);
        Assert.Equal(spot, missile.Trail[0]); // the fire position is vacated first
        for (int i = 1; i < missile.Trail.Count; i++)
        {
            // Marks are one CMMOV apart: one COLUMN on X (Scaled(2)) or one ROW on
            // Y (Scaled(1)), except across a BODY boundary, where the re-aim may
            // turn the missile and the offset becomes diagonal (both axes).
            // Nothing may be skipped entirely.
            int gap = Math.Abs(missile.Trail[i].X - missile.Trail[i - 1].X)
                + Math.Abs(missile.Trail[i].Y - missile.Trail[i - 1].Y);
            Assert.InRange(gap, ScreenSize.Scaled(1), ScreenSize.Scaled(3));
        }

        // A long flight must not grow the tail past the ring.
        for (int tick = 0; tick < GameplayConstants.PortTicksCeil(3) * 40; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(GameplayConstants.MissileTrailMarks, missile.Trail.Count);

        missile.Destroy(); // CMKIL wipes the remaining marks
        Assert.Empty(missile.Trail);
    }

    [Fact]
    public void Prog_LeavesTheRomsSevenGhostTrail_FrozenInItsCreationPose()
    {
        // ROM PROG3: the entry under the shadow-ring index is erased (`PCTOFF`)
        // and overwritten with the position being vacated. `PD+8` is the index
        // and the entries run PD+10, PD+12 ... wrapping at `SPSIZE` — with
        // `PD` = 7 and `SPSIZE` = 31 that is byte offsets 17,19,...29, so SEVEN
        // ghosts trail the prog. Each is blitted once and never again, so it
        // keeps the pose it was born in.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        WarmUp(field);

        IntVector2 spot = new(inner.X + 200, inner.Y + 100);
        var prog = new Prog(spot, HumanKind.Dad, new Random(6));
        field.AddProg(prog);

        for (int tick = 0; tick < GameplayConstants.PortTicksCeil(3) * 3; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(3, prog.GhostTrail.Count);   // one per body
        Assert.Equal(spot, prog.GhostTrail[^1]);  // newest first, so the spawn spot is last
        Assert.Equal(3, prog.GhostFrames.Count);

        // Capped at the ROM's seven — the ring never grows.
        for (int tick = 0; tick < GameplayConstants.PortTicksCeil(3) * 12; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(GameplayConstants.ProgGhostCount, prog.GhostTrail.Count);
        Assert.Equal(GameplayConstants.ProgGhostCount, prog.GhostFrames.Count);

        // The poses are frozen at birth, so the ABAC walk cycle shows through
        // the trail — if every ghost shared one frame the trail would animate
        // as a single snake instead of strobing.
        Assert.Contains(prog.GhostFrames, f => f != prog.GhostFrames[0]);
    }

    [Fact]
    public void CruiseMissile_ReflectsOffWalls_AndStaysInside_OverManyTicks()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        // Off the player's center spot so the simulation stays live.
        IntVector2 spot = new(inner.X + 100, inner.Y + 300);
        var missile = new CruiseMissile(spot, field.Player.Position, new Random(7));
        field.AddCruiseMissile(missile);

        for (int tick = 0; tick < 1200; tick++)
        {
            field.Update(new GameTime());
            Assert.InRange(missile.Bounds.X, inner.X, inner.Right - missile.Bounds.Width);
            Assert.InRange(missile.Bounds.Y, inner.Y, inner.Bottom - missile.Bounds.Height);
        }
    }

    [Fact]
    public void LaserKillsCruiseMissile_Scores25()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 250, inner.Y + 120);
        var missile = new CruiseMissile(spot, field.Player.Position, new Random(8));
        field.AddCruiseMissile(missile);

        Assert.True(field.PlayerLasers.TryFire(new IntVector2(spot.X, spot.Y - 12), Direction8.Down, out PlayerLaser? laser));
        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, missile.LifeState);
        Assert.Equal(25, field.Score.Score); // ScoreValues.CruiseMissile
    }

    [Fact]
    public void LaserKillsBrain_Scores500_AndLaserKillsProg_Scores100()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;

        IntVector2 brainSpot = new(inner.X + 150, inner.Y + 120);
        var brain = new Brain(brainSpot, new Random(9), brainSpeedRomTicks: 0, fireDelayRomTicks: 40);
        field.AddBrain(brain);
        Assert.True(field.PlayerLasers.TryFire(new IntVector2(brainSpot.X + 7, brainSpot.Y - 12), Direction8.Down, out PlayerLaser? l1));
        field.Update(new GameTime());
        Assert.Equal(EntityLifeState.Dead, brain.LifeState); // BRNKIL: explode, then off — no blink
        Assert.Equal(500, field.Score.Score);

        IntVector2 progSpot = new(inner.X + 300, inner.Y + 120);
        var prog = new Prog(progSpot, HumanKind.Dad, new Random(10));
        field.AddProg(prog);
        Assert.True(field.PlayerLasers.TryFire(new IntVector2(progSpot.X + 5, progSpot.Y - 12), Direction8.Down, out PlayerLaser? l2));
        field.Update(new GameTime());
        Assert.Equal(EntityLifeState.Dying, prog.LifeState);
        Assert.Equal(600, field.Score.Score); // +100
    }

    [Fact]
    public void IsLevelCleared_RequiresBrainsProgsAndMissilesGone()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        Assert.True(field.IsLevelCleared); // empty wave

        var brain = new Brain(new IntVector2(inner.X + 100, inner.Y + 100), new Random(11), 0, 40);
        field.AddBrain(brain);
        var prog = new Prog(new IntVector2(inner.X + 200, inner.Y + 100), HumanKind.Dad, new Random(12));
        field.AddProg(prog);
        var missile = new CruiseMissile(new IntVector2(inner.X + 300, inner.Y + 100), field.Player.Position, new Random(13));
        field.AddCruiseMissile(missile);
        Assert.False(field.IsLevelCleared);

        // Kill them: brain 2-second blink-off (real elapsed time), prog
        // burst, missile instant off.
        brain.Kill();
        prog.Kill();
        missile.Destroy();
        for (int tick = 0; tick < 200 && !field.IsLevelCleared; tick++)
        {
            field.Update(Frame());
        }

        Assert.True(field.IsLevelCleared);
    }

    [Fact]
    public void Player_HoldingFire_AutoReFires_AfterTheCooldown_AndCapsAtThreeLasers()
    {
        var input = new PhaseInput { State = new PlayerInputState(IntVector2.Zero, IntVector2.Zero, FirePressed: true) };
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1), input);
        Rectangle inner = field.Wall.PlayfieldBounds;

        // The player aims down by default; park near the bottom wall so each
        // laser dies within a few ticks and its slot frees up.
        field.Player.TeleportTo(new IntVector2(inner.X + inner.Width / 2, inner.Bottom - 60));

        int aliveLasers() => field.PlayerLasers.Slots.Count(l => l is { LifeState: EntityLifeState.Alive });

        for (int tick = 0; tick < 60; tick++)
        {
            field.Update(new GameTime());
            Assert.True(aliveLasers() <= LaserSlots.Capacity, $"tick {tick}: {aliveLasers()} lasers > {LaserSlots.Capacity}");
        }

        // If firing were press-once (the round-6 complaint), the single
        // laser would be long dead by now and no laser would be alive.
        Assert.True(aliveLasers() >= 1, "no laser alive after 60 held-fire ticks — autofire not repeating");
    }

    [Fact]
    public void InputSource_SkipLevel_FlagsOnThePKey()
    {
        // The P key mapping lives in the KeyboardPlayerInputSource (needs a
        // live device) — here: the state struct carries the flag, and
        // PlayingState's wave-clear check reads it (smoke-tested live).
        Assert.False(default(PlayerInputState).SkipLevelPressed);
        Assert.True(new PlayerInputState(IntVector2.Zero, IntVector2.Zero, false, true).SkipLevelPressed);
    }
}
