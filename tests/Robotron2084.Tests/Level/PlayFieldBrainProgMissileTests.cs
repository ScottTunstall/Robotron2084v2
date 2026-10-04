using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
/// PHASE E (arcade-fidelity-notes (18)): brains (ROM BRNORG $1AC0 — nearest
/// human target, 1px/beat chase, ABAC, cruise missiles, touch-conversion to
/// progs), progs (straight-line walkers that keep the human's animation frames and box and
/// die in a strip explosion of the phony burst card, notes §90),
/// cruise missiles (50/25/25 direction roll,
/// wall-reflected, 25 pts), plus the round-6 autofire cadence and the P
/// "skip level" port key.
/// </summary>
public sealed class PlayFieldBrainProgMissileTests
{
    private sealed class PhaseInput : IPlayerInputSource
    {
        public PlayerInputState State = default;

        public PlayerInputState Poll() => State;
    }

    private static GameTime Frame() => new(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000.0 / 60));

    /// <summary>
    /// Runs through the start of the wave, until the game is live and the robots are no longer held.
    /// </summary>
    private static void WarmUp(PlayField field)
    {
        for (int tick = 0; tick < WaveStartTicks.UntilLive(field); tick++)
        {
            field.Update(Frame());
        }
    }

    private static PlayField CreateField(LevelParameters parameters, IPlayerInputSource? input = null) =>
        new PlayFieldBuilder().WithParameters(parameters).WithInput(input ?? new FakeInputSource()).WithSeed(99).Build();

    /// <summary>
    /// Ticks past the brain's first beat. ROM <c>BRAIN_AI</c> resolves its target — and tests its catch
    /// reach — inside its body, so a brain that has not had a beat yet has no target at all. With
    /// <c>beatWaitRomFrames: 0</c> the period is one ROM frame (6 clock units) and a tick adds 5, so the
    /// SECOND tick is the beat (notes §18.8).
    /// </summary>
    private static void RunFirstBeat(PlayField field)
    {
        field.Update(Frame());
        field.Update(Frame());
    }

    /// <summary>Plain Manhattan distance in port pixels — used only to set a test's premise up.</summary>
    private static int PixelDistance(IntVector2 a, IntVector2 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static LevelParameters BrainWave(int brains, int mommies = 0, int daddies = 0, int Mikeys = 0) => new(
        LevelNumber: 1,
        BrainCount: brains,
        MommyCount: mommies,
        DaddyCount: daddies,
        MikeyCount: Mikeys);

    [Fact]
    public void Constructor_SpawnsTheWaveBrainCount()
    {
        PlayField field = CreateField(BrainWave(3));
        Assert.Equal(3, field.Entities.Brains.GetLiveCount());
    }

    /// <summary>
    /// The arcade's own bug, reproduced on purpose (notes §18.8): a brain picks its target as it is
    /// created (ROM <c>$1B43</c>) and the brains are created BEFORE <c>HUMSTV</c> fills the family list,
    /// so the search finds every slot empty and returns the list's FIRST slot (<c>$B354</c>). Mikey fills
    /// that slot, because the Mikeys spawn first — so every brain on the wave chases Mikey, however much
    /// nearer another member is standing.
    /// </summary>
    [Fact]
    public void Brains_AllChaseMikey_EvenWhenAnotherFamilyMemberIsNearer()
    {
        LevelParameters wave = new(LevelNumber: 1, BrainCount: 2, DaddyCount: 1, MikeyCount: 1, BrainBeatWaitRomFrames: 0);
        PlayField field = CreateField(wave);
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        Brain first = field.Entities.Brains[0];
        IntVector2 firstStart = first.Position;
        Human mikey = field.Entities.Family.Members.Single(human => human.Kind == HumanKind.Mikey);
        Human daddy = field.Entities.Family.Members.Single(human => human.Kind == HumanKind.Daddy);

        // Mikey far to the right of the field; the daddy right beside the first brain, on its left.
        mikey.MoveTo(new IntVector2(inner.Right - ScreenSize.ToPortPixels(20), firstStart.Y + ScreenSize.ToPortPixels(60)));
        daddy.MoveTo(new IntVector2(firstStart.X - ScreenSize.ToPortPixels(60), firstStart.Y + ScreenSize.ToPortPixels(4)));
        Assert.True(
            PixelDistance(firstStart, daddy.Position) < PixelDistance(firstStart, mikey.Position),
            "the daddy has to be the NEARER member, or this test would pass under the ordinary rule too");

        RunFirstBeat(field);

        // The nearest-member rule would send the first brain left at the daddy; the ROM's slot rule sends
        // every brain at Mikey.
        Assert.All(field.Entities.Brains, brain => Assert.Equal(mikey, brain.Target));
        Assert.True(Math.Abs(first.Position.X - mikey.Position.X) < Math.Abs(firstStart.X - mikey.Position.X));
    }

    /// <summary>
    /// With the GAME ADJUSTMENT page's BRAINS CHASE MIKEY BUG row off, each brain picks again once the family is on
    /// the field, so it starts the wave chasing the member nearest to it.
    /// </summary>
    [Fact]
    public void Brains_StartOnTheNearestMember_WhenTheMikeyBugIsSwitchedOff()
    {
        LevelParameters wave = new(LevelNumber: 1, BrainCount: 6, MommyCount: 2, DaddyCount: 2, MikeyCount: 2);

        PlayField arcade = new PlayFieldBuilder().WithParameters(wave).WithSeed(99).Build();
        Assert.All(arcade.Entities.Brains, brain => Assert.Equal(FamilyList.FirstSlot, brain.TargetFamilySlot));

        PlayField withoutBug = new PlayFieldBuilder().WithParameters(wave).WithSeed(99).WithBrainsChaseMikeyBug(false).Build();
        Assert.All(
            withoutBug.Entities.Brains,
            brain => Assert.Equal(withoutBug.Entities.GetNearestFamilySlot(brain.Position), brain.TargetFamilySlot));
        Assert.Contains(withoutBug.Entities.Brains, brain => brain.TargetFamilySlot != FamilyList.FirstSlot);
    }

    /// <summary>
    /// Once Mikey's slot empties the brains are ordinary again: ROM <c>BRAIN_AI</c> falls back to the
    /// player and, with the family still about, searches for a slot on the spot — so the next beat takes
    /// the nearest member (notes §18.8).
    /// </summary>
    [Fact]
    public void Brains_TargetTheNearestMember_OnceMikeysSlotIsFree()
    {
        LevelParameters wave = new(LevelNumber: 1, BrainCount: 1, DaddyCount: 1, MikeyCount: 1, BrainBeatWaitRomFrames: 0);
        PlayField field = CreateField(wave);
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        Brain brain = field.Entities.Brains[0];
        Human mikey = field.Entities.Family.Members.Single(human => human.Kind == HumanKind.Mikey);
        Human daddy = field.Entities.Family.Members.Single(human => human.Kind == HumanKind.Daddy);

        mikey.MoveTo(new IntVector2(inner.Right - ScreenSize.ToPortPixels(20), brain.Position.Y));
        daddy.MoveTo(new IntVector2(brain.Position.X - ScreenSize.ToPortPixels(60), brain.Position.Y + ScreenSize.ToPortPixels(4)));

        RunFirstBeat(field);
        Assert.Equal(mikey, brain.Target);

        mikey.Kill(); // her slot leaves the family list (ROM: the object is deallocated)
        RunFirstBeat(field);

        Assert.Equal(daddy, brain.Target);
    }

    /// <summary>
    /// Wave 5 is the arcade's FIRST brain wave — 15 brains, 15 mommies and ONE Mikey — and it is where the
    /// bug is at its most obvious: every brain on the wave leaves the set-up holding family slot 0, and
    /// slot 0 is Mikey's, because <c>HUMSTV</c> spawns the Mikeys first (notes §18.8).
    /// </summary>
    [Fact]
    public void WaveFive_EveryBrainStartsOnMikeysFamilySlot()
    {
        LevelParameters wave = LevelParameters.CreateFromWave(5, WaveTable.GetParameters(5));
        Assert.Equal(15, wave.BrainCount);
        Assert.Equal(1, wave.MikeyCount);

        PlayField field = CreateField(wave);

        Human mikey = field.Entities.Family.Members.Single(human => human.Kind == HumanKind.Mikey);
        Assert.Equal(FamilyList.FirstSlot, mikey.FamilySlot);
        Assert.All(field.Entities.Brains, brain => Assert.Equal(FamilyList.FirstSlot, brain.TargetFamilySlot));
    }

    [Fact]
    public void Brain_StepsTowardNearestHuman_OneColumnSidewaysAndOneRowUpAndDownPerBeat()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;

        field.Entities.Family.Add(new Human(TestSprites.Shared, new IntVector2(inner.X + 300, inner.Y + 150), HumanKind.Mommy, new Random(1)));
        WarmUp(field); // run through the start of the wave (RobotsFrozen)

        // 30 arcade px left of (where the human is now) → steps right/up.
        IntVector2 humanSpot = field.Entities.Family.Members[0].Position;
        IntVector2 brainSpot = new(humanSpot.X - ScreenSize.ToPortPixels(30), humanSpot.Y - ScreenSize.ToPortPixels(20));
        var brain = new Brain(TestSprites.Shared, brainSpot, new Random(2), beatWaitRomFrames: 8, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        // Beat period = PortTicks(1 + BRNSPD) = PortTicks(9) = 11 ticks
        // (notes 26: SLEEP(BRNSPD) + one beat-execution vblank). In the
        // 19-tick window exactly one beat runs = 1 column sideways (RRB10 BRNL1
        // steps XTEMP = 1 in column units) and 1 row up or down toward the target.
        for (int tick = 0; tick < ArcadeClock.ToPortTicks(16); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X + ScreenSize.ToPortPixelsFromColumns(1), brain.Position.X);
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixels(1), brain.Position.Y);
    }

    [Fact]
    public void Brain_WithNoLivingHumans_ChasesThePlayer()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        IntVector2 playerSpot = field.Player.Position;

        WarmUp(field); // run through the start of the wave (RobotsFrozen)

        // 30 arcade px right of the player, on the player's OWN row → the
        // brain steps left at it AND one px DOWN: ROM BRN3A has no dead zone
        // on Y and compares with BHS, so "level with the target" counts as
        // "target is below". That ±1px vertical jitter is the arcade brain's
        // hover; the port used to hold the row perfectly still.
        IntVector2 brainSpot = new(playerSpot.X + ScreenSize.ToPortPixels(30), playerSpot.Y);
        var brain = new Brain(TestSprites.Shared, brainSpot, new Random(3), beatWaitRomFrames: 8, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        // One beat in the 19-tick window (period PortTicks(9) = 10, notes 26).
        for (int tick = 0; tick < ArcadeClock.ToPortTicks(16); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X - ScreenSize.ToPortPixelsFromColumns(1), brain.Position.X);
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixels(1), brain.Position.Y);
    }

    [Fact]
    public void Brain_XApproachHasADeadZone_ButYDoesNot()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        IntVector2 playerSpot = field.Player.Position;

        WarmUp(field);

        // 1 arcade px to the right of the player: INSIDE the ROM's ±2 column
        // X dead zone (BRNL1: dx+2 <= 4), so X must not correct —
        // but Y has no dead zone and must still step down 1 px.
        IntVector2 brainSpot = new(playerSpot.X + ScreenSize.ToPortPixels(1), playerSpot.Y - ScreenSize.ToPortPixels(50));
        var brain = new Brain(TestSprites.Shared, brainSpot, new Random(21), beatWaitRomFrames: 8, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        for (int tick = 0; tick < ArcadeClock.ToPortTicks(12); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X, brain.Position.X);      // dead zone holds X
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixels(1), brain.Position.Y);  // Y keeps closing
    }

    [Fact]
    public void Brain_DoesNotDeadlockOnAWall_SlidesAlongItInstead()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;

        WarmUp(field);

        // Player below and to the RIGHT of where the brain will sit.
        field.Player.TeleportTo(new IntVector2(inner.Right - ScreenSize.ToPortPixels(5), inner.Y + ScreenSize.ToPortPixels(130)));

        // Brain flush against the RIGHT wall, so its +X step is out of bounds.
        // The Gospel's CKLIM undoes BOTH axes on failure, which pins it there
        // for good (its Y step is unconditional, so it could never even move
        // down) — the author's "the brains seem to get stuck at the bottom
        // wall". The port rejects per axis, like the ROM's own generic mover
        // (RRS22 OPB80), so the brain creeps down the wall instead.
        IntVector2 brainSpot = new(inner.Right - ScreenSize.ToPortPixels(CollisionSizes.BrainCollisionSize.Width), inner.Y + ScreenSize.ToPortPixels(80));
        var brain = new Brain(TestSprites.Shared, brainSpot, new Random(22), beatWaitRomFrames: 8, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        for (int tick = 0; tick < ArcadeClock.ToPortTicks(12); tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(brainSpot.X, brain.Position.X);          // the wall still holds
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixels(1), brain.Position.Y);      // but it is not stuck
    }

    [Fact]
    public void Brain_CatchingHuman_ReprogramsItThenLeavesAProg_NoSkull()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        // The reprogram animation is robot activity, so RobotsFrozen pauses it
        // (the port's "ALL ROBOTS ARE IMMOBILE" rule) — run through the start of the wave
        // or the loop never advances.
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 200, inner.Y + 200);
        var human = new Human(TestSprites.Shared, humanSpot, HumanKind.Mommy, new Random(4));
        field.Entities.Family.Add(human);

        // Corners coincident — well inside the ROM's ±3px catch reach.
        var brain = new Brain(TestSprites.Shared, humanSpot, new Random(5), beatWaitRomFrames: 0, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        RunFirstBeat(field);

        // ROM BMUT: the brain STOPS and the pair run the 20-iteration animation
        // — the human is not a prog yet, and is off the human list.
        Assert.True(brain.IsReprogramming());
        Assert.True(human.IsBeingReprogrammed);
        Assert.Equal(0, field.Entities.Progs.GetLiveCount());
        Assert.Empty(field.Entities.Skulls); // ROM BRNFLG — never a skull on conversion
        Assert.Null(field.Entities.Family.GetMemberInSlot(human.FamilySlot)); // off the family list now

        // ROM BMUT's placement: just left of the brain (a gap of 1 column), 2 rows below it.
        Assert.Equal(brain.Position.X - human.Bounds.Width - ScreenSize.ToPortPixelsFromColumns(1), human.Position.X);
        Assert.Equal(brain.Position.Y + ScreenSize.ToPortPixels(2), human.Position.Y);

        // …and the placement SETS THE BRAIN'S sprite (BMUT00 `LDD #BRLP1` /
        // BMUT1 `STD OPICT,X`): the human went to its LEFT, so the brain faces
        // LEFT — BRLP1 is BRNAL's frame 0, i.e. the left base's first frame.
        Assert.Equal(0, brain.WalkAnimationFrameIndex);

        // The animation is 20 iterations x 2 redraws x 3 ROM frames = 144 ticks
        // on the clock-unit clock (notes §52; PortTicks(3) = 3 would have made it
        // 120). Through it the brain must not move (it is a solid block, not a
        // chaser).
        IntVector2 brainSpot = brain.Position;
        for (int tick = 0; tick < 130; tick++)
        {
            field.Update(Frame());
        }

        Assert.True(brain.IsReprogramming());
        Assert.Equal(brainSpot, brain.Position);
        Assert.Equal(0, field.Entities.Progs.GetLiveCount());

        for (int tick = 0; tick < 25; tick++)
        {
            field.Update(Frame());
        }

        // Done: the human is gone, a PROG stands at its last position and keeps
        // its animation frames and box, and the brain is free to move again.
        Assert.False(brain.IsReprogramming());
        Assert.NotEqual(EntityLifeState.Alive, human.LifeState);
        Assert.Equal(1, field.Entities.Progs.GetLiveCount());
        Assert.Equal(HumanKind.Mommy, field.Entities.Progs[0].Kind);
        Assert.Empty(field.Entities.Skulls);
    }

    [Fact]
    public void Brain_ProggingAHumanItCannotGetLeftOf_FacesRightInstead()
    {
        // BMUT00 places the human at brainX - humanWidth - 1 (columns) and, when that
        // would cross XMIN, BMUT10 puts it 8 columns to the RIGHT and loads BRRP1
        // instead — the brain faces the human either way (author, 2026-09-20:
        // "the brain does not face the human it is progging").
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 2, inner.Y + 200);
        var human = new Human(TestSprites.Shared, humanSpot, HumanKind.Daddy, new Random(7));
        field.Entities.Family.Add(human);

        var brain = new Brain(TestSprites.Shared, humanSpot, new Random(8), beatWaitRomFrames: 0, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        RunFirstBeat(field);

        Assert.True(brain.IsReprogramming());
        // The human could not fit on the left, so it went right…
        Assert.Equal(brain.Position.X + ScreenSize.ToPortPixelsFromColumns(8), human.Position.X);
        // …and the brain's sprite is BRNAR's frame 0 (BRRP1) — facing RIGHT.
        Assert.Equal(3, brain.WalkAnimationFrameIndex);
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
        var human = new Human(TestSprites.Shared, humanSpot, HumanKind.Daddy, new Random(31));
        field.Entities.Family.Add(human);
        var brain = new Brain(TestSprites.Shared, humanSpot, new Random(32), beatWaitRomFrames: 0, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        RunFirstBeat(field);
        Assert.True(brain.IsReprogramming());

        brain.Kill(); // laser hit, mid-animation
        field.Update(Frame());

        Assert.False(human.IsBeingReprogrammed);
        Assert.Equal(EntityLifeState.Dead, human.LifeState);
        Assert.Equal(0, field.Entities.Progs.GetLiveCount()); // the conversion never completed
        Assert.Single(field.Entities.Skulls);
        Assert.False(brain.IsReprogramming());
    }

    [Fact]
    public void Brain_CatchNeedsTheCornersWithinThreePx_NotASpriteOverlap()
    {
        // ROM BRNL1 compares TOP-LEFT CORNERS within ±3px on both axes. The old
        // Bounds.Overlaps test fired whenever the 14x16 brain box touched the
        // human box — far too eager.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 200, inner.Y + 200);
        var human = new Human(TestSprites.Shared, humanSpot, HumanKind.Mommy, new Random(14));
        field.Entities.Family.Add(human);

        // Boxes overlap (the human is well inside the brain's frame) but the
        // corners are 20px apart → the ROM's reach does not cover it.
        IntVector2 offset = new(ScreenSize.ToPortPixels(20), 0);
        var brain = new Brain(TestSprites.Shared, humanSpot - offset, new Random(15), beatWaitRomFrames: 0, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        RunFirstBeat(field);

        Assert.False(brain.IsReprogramming());
    }

    [Fact]
    public void Brain_CatchReachSideways_IsThreeColumns_SoSixArcadePixels()
    {
        // ROM BRNL1 `ADDA #3 / CMPA #6` compares X in columns: 3 columns is 6 arcade
        // pixels. 4 arcade pixels apart is inside the X dead zone (2 columns), so the
        // brain does not step, and is still within reach.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        WarmUp(field);

        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 humanSpot = new(inner.X + 200, inner.Y + 200);
        var human = new Human(TestSprites.Shared, humanSpot, HumanKind.Mommy, new Random(41));
        field.Entities.Family.Add(human);

        IntVector2 offset = new(ScreenSize.ToPortPixels(4), 0);
        var brain = new Brain(TestSprites.Shared, humanSpot - offset, new Random(42), beatWaitRomFrames: 0, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);

        RunFirstBeat(field);

        Assert.True(brain.IsReprogramming());
    }

    [Fact]
    public void Prog_WalksStraightCardinalLines_InsideTheField()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        // NOT the field center: the player stands there, and prog contact
        // kills the player (RobotsFrozen would stop the simulation).
        IntVector2 spot = new(inner.X + 100, inner.Y + 100);
        var prog = new Prog(TestSprites.Shared, spot, HumanKind.Daddy, new Random(6));
        field.Entities.Progs.Add(prog);
        WarmUp(field); // run through the start of the wave (RobotsFrozen)

        // The prog re-aims on small odds (3%/9% per beat) and when blocked,
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
    public void Prog_CoversFourPixelsOnEitherAxis_PerBeat()
    {
        // ROM PRGAL/PRGAR give (±2, 0) and PRGAD/PRGAU give (0, ±4) — but the X byte moves
        // OX16, a screen address, so it is TWO COLUMNS, not two pixels: a column is 2 px
        // (§55). Both axes therefore cover the same 4 px a beat, and the port's old 2 px
        // horizontal step walked at half the arcade's speed (notes §87).
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        WarmUp(field);

        int sawHorizontal = 0, sawVertical = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            IntVector2 spot = new(inner.X + 60, inner.Y + 60);
            var prog = new Prog(TestSprites.Shared, spot, HumanKind.Daddy, new Random(seed));
            field.Entities.Progs.Add(prog);

            for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3); tick++)
            {
                field.Update(Frame());
            }

            int dx = Math.Abs(prog.Position.X - spot.X);
            int dy = Math.Abs(prog.Position.Y - spot.Y);

            // NAP 3 beat (3.6 ticks): exactly one step per beat, one axis only, 4 px either way.
            Assert.True(dx == 0 || dy == 0, $"seed {seed}: diagonal step {dx},{dy}");
            Assert.Equal(ScreenSize.ToPortPixels(4), dx + dy);

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
    public void CruiseMissile_MovesTwicePerBeat_AndNeverStalls()
    {
        // ROM CMISL: one beat = NAP 2, doing TWO CMMOVs of 1px each. And
        // GCMDIR's `BPL GCMDY` jumps INTO the Y block, so a missile whose X
        // is not armed always seeks on Y — there is no "both axes zero" case.
        Rectangle inner = PlayFieldBuilder.DefaultBounds;

        int xArmed = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            var missile = new CruiseMissile(TestSprites.Shared, 
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
        var missile = new CruiseMissile(TestSprites.Shared, spot, field.Player.Position, new Random(7));
        field.Entities.CruiseMissiles.Add(missile);

        // The first beat (2 x 1px CMMOV) lands on tick 4 (3 ROM frames = 3.6),
        // and the re-aim timer cannot fire that early (RND(1..7) >= 1 decrement
        // per beat, re-aim on reaching 0).
        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3); tick++)
        {
            field.Update(Frame());
        }

        int dx = Math.Abs(missile.Position.X - spot.X);
        int dy = Math.Abs(missile.Position.Y - spot.Y);

        // Two CMMOVs per beat: `ADDA PD4` steps the X COLUMN byte (1 column = 2
        // arcade px, notes §51) and `ADDB PD5` the Y ROW byte (1 px) — so a beat is
        // 2 columns on X but only 2 rows on Y. The missile really is faster
        // horizontally than vertically; there is no halving here as there is for
        // the player and the tank.
        Assert.True(dx is 0 || dx == ScreenSize.ToPortPixels(4), $"dx {dx} is not 0 or 4 arcade px");
        Assert.True(dy is 0 || dy == ScreenSize.ToPortPixels(2), $"dy {dy} is not 0 or 2 arcade px");
        Assert.True(dx != 0 || dy != 0, "missile did not move at all in its first beat");
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
        var missile = new CruiseMissile(TestSprites.Shared, spot, field.Player.Position, new Random(7));
        field.Entities.CruiseMissiles.Add(missile);

        // Two beats = 4 CMMOVs = 4 marks (the missile flies on while the
        // start of the wave holds the robots). Beats land on ticks 4 and 8.
        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 2; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(4, missile.Trail.Count);
        Assert.Equal(spot, missile.Trail[0]); // the fire position is vacated first
        for (int i = 1; i < missile.Trail.Count; i++)
        {
            // Marks are one CMMOV apart: one COLUMN on X (Scaled(2)) or one ROW on
            // Y (Scaled(1)), except across a BEAT boundary, where the re-aim may
            // turn the missile and the offset becomes diagonal (both axes).
            // Nothing may be skipped entirely.
            int gap = Math.Abs(missile.Trail[i].X - missile.Trail[i - 1].X)
                + Math.Abs(missile.Trail[i].Y - missile.Trail[i - 1].Y);
            Assert.InRange(gap, ScreenSize.ToPortPixels(1), ScreenSize.ToPortPixels(3));
        }

        // A long flight must not grow the tail past the ring.
        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 40; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(CruiseMissileTuning.TrailMarks, missile.Trail.Count);

        missile.Kill(); // CMKIL wipes the remaining marks
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
        var prog = new Prog(TestSprites.Shared, spot, HumanKind.Daddy, new Random(6));
        field.Entities.Progs.Add(prog);

        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 3; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(3, prog.GetGhostTrail().Count);   // one per beat
        Assert.Equal(spot, prog.GetGhostTrail()[^1]);  // newest first, so the spawn spot is last
        Assert.Equal(3, prog.GetGhostFrames().Count);

        // Capped at the ROM's seven — the ring never grows.
        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 12; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(ProgTuning.GhostCount, prog.GetGhostTrail().Count);
        Assert.Equal(ProgTuning.GhostCount, prog.GetGhostFrames().Count);

        // The poses are frozen at birth, so the ABAC walk cycle shows through
        // the trail — if every ghost shared one frame the trail would animate
        // as a single snake instead of strobing.
        Assert.Contains(prog.GetGhostFrames(), f => f != prog.GetGhostFrames()[0]);
    }

    [Fact]
    public void ShootingAProg_LeavesNoDyingPop_AndTheFieldShattersTheBurstCard()
    {
        // ROM PRGKIL (RRB10): erase the seven trail images, `JSR KILL`, swap the sprite
        // descriptor to `PGXPIC` ("BLOW PHONY PICT"), clamp the corner to
        // (XMAX-5, YMAX-15), then `JSR EXST` — the EVERYDAY explosion, fed the phony
        // card. The port instead drew the card as a 20-tick static pop in a Dying
        // state, which the author saw as "the explosion effect looks weird"
        // (2026-09-17, notes §90). There is no Dying phase any more.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        field.SkipWaveStart();
        Rectangle inner = field.Wall.PlayfieldBounds;
        var prog = new Prog(TestSprites.Shared, new IntVector2(inner.X + 120, inner.Y + 120), HumanKind.Mommy, new Random(21));
        field.Entities.Progs.Add(prog);
        IntVector2 spot = prog.Position;

        Assert.True(field.PlayerLasers.TryFire(new IntVector2(spot.X + 3, spot.Y - 12), Direction8.Down, out _));
        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, prog.LifeState); // instant off, no pop
        Assert.Equal(0, field.Entities.Progs.GetLiveCount());
        StripEffect explosion = Assert.Single(field.Entities.Explosions);
        // UL = OBJX/OBJY with the sprite's W/H: the 12x16 card's rect at the prog's
        // corner, NOT the smaller human box it was walking in.
        Assert.Equal(spot.X, explosion.Position.X);
        Assert.Equal(spot.Y, explosion.Position.Y);
        Assert.Equal(ScreenSize.ToPortPixels(CollisionSizes.ProgBurstSize.Width), explosion.Bounds.Width);
        Assert.Equal(ScreenSize.ToPortPixels(CollisionSizes.ProgBurstSize.Height), explosion.Bounds.Height);
    }

    [Fact]
    public void CruiseMissile_ReflectsOffWalls_AndStaysInside_OverManyTicks()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        // Off the player's center spot so the simulation stays live.
        IntVector2 spot = new(inner.X + 100, inner.Y + 300);
        var missile = new CruiseMissile(TestSprites.Shared, spot, field.Player.Position, new Random(7));
        field.Entities.CruiseMissiles.Add(missile);

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
        field.SkipWaveStart();
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 250, inner.Y + 120);
        var missile = new CruiseMissile(TestSprites.Shared, spot, field.Player.Position, new Random(8));
        field.Entities.CruiseMissiles.Add(missile);

        Assert.True(field.PlayerLasers.TryFire(new IntVector2(spot.X, spot.Y - 12), Direction8.Down, out PlayerLaser? _));
        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, missile.LifeState);
        Assert.Equal(25, field.ScoreBoard.Score); // ScoreValues.CruiseMissile
    }

    [Fact]
    public void LaserKillsBrain_Scores500_AndLaserKillsProg_Scores100()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        field.SkipWaveStart();
        Rectangle inner = field.Wall.PlayfieldBounds;

        IntVector2 brainSpot = new(inner.X + 150, inner.Y + 120);
        var brain = new Brain(TestSprites.Shared, brainSpot, new Random(9), beatWaitRomFrames: 0, fireIntervalBeats: 40, targetFamilySlot: FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);
        Assert.True(field.PlayerLasers.TryFire(new IntVector2(brainSpot.X + 7, brainSpot.Y - 12), Direction8.Down, out PlayerLaser? _));
        field.Update(new GameTime());
        Assert.Equal(EntityLifeState.Dead, brain.LifeState); // BRNKIL: explode, then off — no blink
        Assert.Equal(500, field.ScoreBoard.Score);

        IntVector2 progSpot = new(inner.X + 300, inner.Y + 120);
        var prog = new Prog(TestSprites.Shared, progSpot, HumanKind.Daddy, new Random(10));
        field.Entities.Progs.Add(prog);
        Assert.True(field.PlayerLasers.TryFire(new IntVector2(progSpot.X + 5, progSpot.Y - 12), Direction8.Down, out PlayerLaser? _));
        field.Update(new GameTime());

        // ROM PRGKIL: `JSR KILL` then the sprite swap, so the prog is gone AT ONCE
        // (no Dying pop) and what remains is the ordinary strip explosion of the
        // 12x16 PGXPIC card it swapped in (notes §90).
        Assert.Equal(EntityLifeState.Dead, prog.LifeState);
        Assert.Equal(600, field.ScoreBoard.Score); // +100
        Assert.Equal(2, field.Entities.Explosions.Count); // the brain's and the prog's
        StripEffect card = field.Entities.Explosions[1];
        Assert.Equal(progSpot.X, card.Position.X);
        Assert.Equal(progSpot.Y, card.Position.Y); // UL = OBJX/OBJY, unchanged by PRGKIL
        Assert.Equal(ScreenSize.ToPortPixels(CollisionSizes.ProgBurstSize.Width), card.Bounds.Width);
        Assert.Equal(ScreenSize.ToPortPixels(CollisionSizes.ProgBurstSize.Height), card.Bounds.Height);
        Assert.Equal(StripFanAxis.Columns, card.Axis); // a vertical shot -> the H family
    }

    [Fact]
    public void IsLevelCleared_RequiresBrainsGone_ButNotProgsOrMissiles()
    {
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1));
        Rectangle inner = field.Wall.PlayfieldBounds;
        Assert.True(field.IsLevelCleared()); // empty wave

        var brain = new Brain(TestSprites.Shared, new IntVector2(inner.X + 100, inner.Y + 100), new Random(11), 0, 40, FamilyList.FirstSlot);
        field.Entities.Brains.Add(brain);
        var prog = new Prog(TestSprites.Shared, new IntVector2(inner.X + 200, inner.Y + 100), HumanKind.Daddy, new Random(12));
        field.Entities.Progs.Add(prog);
        var missile = new CruiseMissile(TestSprites.Shared, new IntVector2(inner.X + 300, inner.Y + 100), field.Player.Position, new Random(13));
        field.Entities.CruiseMissiles.Add(missile);
        Assert.False(field.IsLevelCleared()); // the brain is still there

        // ROM WVCHEK counts only grunts, spheroids, enforcers, brains, tanks and quarks:
        // a prog and a cruise missile left flying do not hold the wave open.
        brain.Kill();
        for (int tick = 0; tick < 200 && !field.IsLevelCleared(); tick++)
        {
            field.Update(Frame());
        }

        Assert.True(field.IsLevelCleared());
        Assert.True(prog.IsAlive());
        Assert.True(missile.IsAlive());
    }

    [Fact]
    public void Player_HoldingFire_AutoReFires_AfterTheCooldown_AndCapsAtThreeLasers()
    {
        var input = new PhaseInput { State = new PlayerInputState(IntVector2.Zero, IntVector2.Zero, FireHeld: true) };
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1), input);
        field.SkipWaveStart();
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
    public void SkipLevelHeld_CarriesTheFlagTheWaveClearPathReads()
    {
        // The KEY itself is read in ControlSettings.ReadPlayer (Insert — P is PAUSE; see
        // SpaceAndInsert_StayAsPortAliases); PlayingState's wave-clear check reads the flag this state
        // carries.
        Assert.False(default(PlayerInputState).SkipLevelHeld);
        Assert.True(new PlayerInputState(IntVector2.Zero, IntVector2.Zero, false, true).SkipLevelHeld);
    }
}
