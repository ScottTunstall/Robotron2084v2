using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     PHASE E (arcade-fidelity-notes (18)): brains (ROM BRNORG $1AC0 — nearest
///     family member target, 1px/beat chase, ABAC, cruise missiles, touch-conversion to
///     progs), progs (straight-line walkers that keep the family member's animation frames and box and
///     die in a strip explosion of the phony burst card, notes §90),
///     cruise missiles (50/25/25 random direction,
///     wall-reflected, 25 pts), plus the round-6 autofire cadence and the P
///     "skip wave" port key.
/// </summary>
public sealed class PlayFieldBrainProgMissileTests
{
    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000.0 / 60));
    }

    /// <summary>
    ///     Runs through the start of the wave, until the game is live and the robots are no longer held.
    /// </summary>
    /// <summary>
    ///     Makes the game live at once. These tests are of what a brain does in play, not of how long it waits at the
    ///     start of a wave (which <c>RobotFirstMoveTests</c> covers).
    /// </summary>
    private static void WarmUp(PlayField playField)
    {
        playField.SkipWaveStart();
    }

    private static PlayField CreateField(WaveParameters waveParameters, IPlayerInputSource? playerInputSource = null)
    {
        return new PlayFieldBuilder().WithParameters(waveParameters).WithInput(playerInputSource ?? new FakeInputSource()).WithSeed(99)
            .Build();
    }

    /// <summary>
    ///     Ticks past the brain's first beat. ROM <c>BRAIN_AI</c> resolves its target — and tests its catch
    ///     reach — inside its body, so a brain that has not had a beat yet has no target at all. With
    ///     <c>beatWaitArcadeFrames: 0</c> the period is one fiftieth of a second (6 clock units) and a tick adds 5, so the
    ///     SECOND tick is the beat (notes §18.8).
    /// </summary>
    private static void RunFirstBeat(PlayField playField)
    {
        playField.Update(OneTick());
        playField.Update(OneTick());
    }

    /// <summary>Plain Manhattan distance in port pixels — used only to set a test's premise up.</summary>
    private static int PixelDistance(IntVector2 a, IntVector2 b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private static WaveParameters BrainWave(int brains, int mommies = 0, int daddies = 0, int Mikeys = 0)
    {
        return new WaveParameters(
            1,
            BrainCount: brains,
            MommyCount: mommies,
            DaddyCount: daddies,
            MikeyCount: Mikeys);
    }

    [Fact]
    public void Constructor_SpawnsTheWaveBrainCount()
    {
        var playField = CreateField(BrainWave(3));
        Assert.Equal(3, playField.FieldEntities.Brains.Count(entity => !entity.IsDead()));
    }

    /// <summary>
    ///     The arcade's own bug, reproduced on purpose (notes §18.8): a brain picks its target as it is
    ///     created (ROM <c>$1B43</c>) and the brains are created BEFORE <c>HUMSTV</c> fills the family list,
    ///     so the search finds every slot empty and returns the list's FIRST slot (<c>$B354</c>). Mikey fills
    ///     that slot, because the Mikeys spawn first — so every brain on the wave chases Mikey, however much
    ///     nearer another member is standing.
    /// </summary>
    [Fact]
    public void Brains_AllChaseMikey_EvenWhenAnotherFamilyMemberIsNearer()
    {
        WaveParameters waveParameters = new(1, BrainCount: 2, DaddyCount: 1, MikeyCount: 1, BrainBeatWaitArcadeFrames: 0);
        var playField = CreateField(waveParameters);
        WarmUp(playField);

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        var first = playField.FieldEntities.Brains[0];
        var firstStart = first.Position;
        var mikey = playField.FieldEntities.FamilyList.Members.Single(familyMember => familyMember.Kind == FamilyMemberKind.Mikey);
        var daddy = playField.FieldEntities.FamilyList.Members.Single(familyMember => familyMember.Kind == FamilyMemberKind.Daddy);

        // Mikey far to the right of the field; the daddy right beside the first brain, on its left.
        mikey.MoveTo(new IntVector2(inner.Right - ScreenSize.ToPortPixelsFromArcadePixels(20),
            firstStart.Y + ScreenSize.ToPortPixelsFromArcadePixels(60)));
        daddy.MoveTo(new IntVector2(firstStart.X - ScreenSize.ToPortPixelsFromArcadePixels(60),
            firstStart.Y + ScreenSize.ToPortPixelsFromArcadePixels(4)));
        Assert.True(
            PixelDistance(firstStart, daddy.Position) < PixelDistance(firstStart, mikey.Position),
            "the daddy has to be the NEARER member, or this test would pass under the ordinary rule too");

        RunFirstBeat(playField);

        // The nearest-member rule would send the first brain left at the daddy; the ROM's slot rule sends
        // every brain at Mikey.
        Assert.All(playField.FieldEntities.Brains, brain => Assert.Equal(mikey, brain.TargetFamilyMember));
        Assert.True(Math.Abs(first.Position.X - mikey.Position.X) < Math.Abs(firstStart.X - mikey.Position.X));
    }

    /// <summary>
    ///     With the GAME ADJUSTMENT page's BRAINS CHASE MIKEY BUG row off, each brain picks again once the family is on
    ///     the field, so it starts the wave chasing the member nearest to it.
    /// </summary>
    [Fact]
    public void Brains_StartOnTheNearestMember_WhenTheMikeyBugIsSwitchedOff()
    {
        WaveParameters waveParameters = new(1, BrainCount: 6, MommyCount: 2, DaddyCount: 2, MikeyCount: 2);

        var arcadePlayField = new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(99).Build();
        Assert.All(arcadePlayField.FieldEntities.Brains, brain => Assert.Equal(FamilyList.FirstSlot, brain.TargetFamilySlot));

        var withoutBugPlayField = new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(99).WithBrainsChaseMikeyBug(false)
            .Build();
        Assert.All(
            withoutBugPlayField.FieldEntities.Brains,
            brain => Assert.Equal(withoutBugPlayField.FieldEntities.GetNearestFamilySlot(brain.Position), brain.TargetFamilySlot));
        Assert.Contains(withoutBugPlayField.FieldEntities.Brains, brain => brain.TargetFamilySlot != FamilyList.FirstSlot);
    }

    /// <summary>
    ///     Once Mikey's slot empties the brains are ordinary again: ROM <c>BRAIN_AI</c> falls back to the
    ///     player and, with the family still about, searches for a slot on the spot — so the next beat takes
    ///     the nearest member (notes §18.8).
    /// </summary>
    [Fact]
    public void Brains_TargetTheNearestMember_OnceMikeysSlotIsFree()
    {
        WaveParameters waveParameters = new(1, BrainCount: 1, DaddyCount: 1, MikeyCount: 1, BrainBeatWaitArcadeFrames: 0);
        var playField = CreateField(waveParameters);
        WarmUp(playField);

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        var brain = playField.FieldEntities.Brains[0];
        var mikey = playField.FieldEntities.FamilyList.Members.Single(familyMember => familyMember.Kind == FamilyMemberKind.Mikey);
        var daddy = playField.FieldEntities.FamilyList.Members.Single(familyMember => familyMember.Kind == FamilyMemberKind.Daddy);

        mikey.MoveTo(new IntVector2(inner.Right - ScreenSize.ToPortPixelsFromArcadePixels(20), brain.Position.Y));
        daddy.MoveTo(new IntVector2(brain.Position.X - ScreenSize.ToPortPixelsFromArcadePixels(60),
            brain.Position.Y + ScreenSize.ToPortPixelsFromArcadePixels(4)));

        RunFirstBeat(playField);
        Assert.Equal(mikey, brain.TargetFamilyMember);

        mikey.Kill(); // her slot leaves the family list (ROM: the object is deallocated)
        RunFirstBeat(playField);

        Assert.Equal(daddy, brain.TargetFamilyMember);
    }

    /// <summary>
    ///     Wave 5 is the arcade's FIRST brain wave — 15 brains, 15 mommies and ONE Mikey — and it is where the
    ///     bug is at its most obvious: every brain on the wave leaves the set-up holding family slot 0, and
    ///     slot 0 is Mikey's, because <c>HUMSTV</c> spawns the Mikeys first (notes §18.8).
    /// </summary>
    [Fact]
    public void WaveFive_EveryBrainStartsOnMikeysFamilySlot()
    {
        var waveParameters = WaveParameters.CreateFromWave(5, WaveTable.GetParameters(5));
        Assert.Equal(15, waveParameters.BrainCount);
        Assert.Equal(1, waveParameters.MikeyCount);

        var playField = CreateField(waveParameters);

        var mikey = playField.FieldEntities.FamilyList.Members.Single(familyMember => familyMember.Kind == FamilyMemberKind.Mikey);
        Assert.Equal(FamilyList.FirstSlot, mikey.FamilySlot);
        Assert.All(playField.FieldEntities.Brains, brain => Assert.Equal(FamilyList.FirstSlot, brain.TargetFamilySlot));
    }

    [Fact]
    public void Brain_StepsTowardNearestFamilyMember_OneColumnSidewaysAndOneRowUpAndDownPerBeat()
    {
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;

        playField.FieldEntities.FamilyList.Add(new FamilyMember(TestTextures.SharedTextureSet, new IntVector2(inner.X + 300, inner.Y + 150),
            FamilyMemberKind.Mommy, new Random(1)));
        WarmUp(playField); // run through the start of the wave (RobotsFrozen())

        // 30 arcade px left of (where the family member is now) → steps right/up.
        var familyMemberSpot = playField.FieldEntities.FamilyList.Members[0].Position;
        IntVector2 brainSpot = new(familyMemberSpot.X - ScreenSize.ToPortPixelsFromArcadePixels(30),
            familyMemberSpot.Y - ScreenSize.ToPortPixelsFromArcadePixels(20));
        var brain = new Brain(TestTextures.SharedTextureSet, brainSpot, new Random(2), 8, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        // Beat period = PortTicks(1 + BRNSPD) = PortTicks(9) = 11 ticks
        // (notes 26: SLEEP(BRNSPD) + one beat-execution arcade frame). In the
        // 19-tick window exactly one beat runs = 1 column sideways (RRB10 BRNL1
        // steps XTEMP = 1 in column units) and 1 row up or down toward the target.
        for (var tick = 0; tick < ArcadeClock.ToPortTicks(16); tick++) playField.Update(OneTick());

        Assert.Equal(brainSpot.X + ScreenSize.ToPortPixelsFromColumns(1), brain.Position.X);
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixelsFromArcadePixels(1), brain.Position.Y);
    }

    [Fact]
    public void Brain_WithNoLivingFamilyMembers_ChasesThePlayer()
    {
        var playField = CreateField(new WaveParameters(1));
        var playerSpot = playField.Player.Position;

        WarmUp(playField); // run through the start of the wave (RobotsFrozen())

        // 30 arcade px right of the player, on the player's OWN row → the
        // brain steps left at it AND one px DOWN: ROM BRN3A has no dead zone
        // on Y and compares with BHS, so "level with the target" counts as
        // "target is below". That ±1px vertical jitter is the arcade brain's
        // hover; the port used to hold the row perfectly still.
        IntVector2 brainSpot = new(playerSpot.X + ScreenSize.ToPortPixelsFromArcadePixels(30), playerSpot.Y);
        var brain = new Brain(TestTextures.SharedTextureSet, brainSpot, new Random(3), 8, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        // One beat in the 19-tick window (period PortTicks(9) = 10, notes 26).
        for (var tick = 0; tick < ArcadeClock.ToPortTicks(16); tick++) playField.Update(OneTick());

        Assert.Equal(brainSpot.X - ScreenSize.ToPortPixelsFromColumns(1), brain.Position.X);
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixelsFromArcadePixels(1), brain.Position.Y);
    }

    [Fact]
    public void Brain_XApproachHasADeadZone_ButYDoesNot()
    {
        var playField = CreateField(new WaveParameters(1));
        var playerSpot = playField.Player.Position;

        WarmUp(playField);

        // 1 arcade px to the right of the player: INSIDE the ROM's ±2 column
        // X dead zone (BRNL1: dx+2 <= 4), so X must not correct —
        // but Y has no dead zone and must still step down 1 px.
        IntVector2 brainSpot = new(playerSpot.X + ScreenSize.ToPortPixelsFromArcadePixels(1),
            playerSpot.Y - ScreenSize.ToPortPixelsFromArcadePixels(50));
        var brain = new Brain(TestTextures.SharedTextureSet, brainSpot, new Random(21), 8, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        for (var tick = 0; tick < ArcadeClock.ToPortTicks(12); tick++) playField.Update(OneTick());

        Assert.Equal(brainSpot.X, brain.Position.X); // dead zone holds X
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixelsFromArcadePixels(1), brain.Position.Y); // Y keeps closing
    }

    [Fact]
    public void Brain_DoesNotDeadlockOnAWall_SlidesAlongItInstead()
    {
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;

        WarmUp(playField);

        // Player below and to the RIGHT of where the brain will sit.
        playField.Player.MoveTo(new IntVector2(inner.Right - ScreenSize.ToPortPixelsFromArcadePixels(5),
            inner.Y + ScreenSize.ToPortPixelsFromArcadePixels(130)));

        // Brain flush against the RIGHT wall, so its +X step is out of bounds.
        // The Gospel's CKLIM undoes BOTH axes on failure, which pins it there
        // for good (its Y step is unconditional, so it could never even move
        // down) — the author's "the brains seem to get stuck at the bottom
        // wall". The port rejects per axis, like the ROM's own generic mover
        // (RRS22 OPB80), so the brain creeps down the wall instead.
        IntVector2 brainSpot =
            new(inner.Right - ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BrainCollisionSize.Width),
                inner.Y + ScreenSize.ToPortPixelsFromArcadePixels(80));
        var brain = new Brain(TestTextures.SharedTextureSet, brainSpot, new Random(22), 8, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        for (var tick = 0; tick < ArcadeClock.ToPortTicks(12); tick++) playField.Update(OneTick());

        Assert.Equal(brainSpot.X, brain.Position.X); // the wall still holds
        Assert.Equal(brainSpot.Y + ScreenSize.ToPortPixelsFromArcadePixels(1), brain.Position.Y); // but it is not stuck
    }

    [Fact]
    public void Brain_CatchingFamilyMember_ReprogramsItThenLeavesAProg_NoSkull()
    {
        var playField = CreateField(new WaveParameters(1));
        // The reprogram animation is robot activity, so RobotsFrozen() pauses it
        // (the port's "ALL ROBOTS ARE IMMOBILE" rule) — run through the start of the wave
        // or the loop never advances.
        WarmUp(playField);

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 familyMemberSpot = new(inner.X + 200, inner.Y + 200);
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, familyMemberSpot, FamilyMemberKind.Mommy, new Random(4));
        playField.FieldEntities.FamilyList.Add(familyMember);

        // Corners coincident — well inside the ROM's ±3px catch reach.
        var brain = new Brain(TestTextures.SharedTextureSet, familyMemberSpot, new Random(5), 0, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        RunFirstBeat(playField);

        // ROM BMUT: the brain STOPS and the pair run the 20-iteration animation
        // — the family member is not a prog yet, and is off the family member list.
        Assert.True(brain.IsReprogramming());
        Assert.True(familyMember.IsBeingReprogrammed);
        Assert.Equal(0, playField.FieldEntities.Progs.Count(entity => !entity.IsDead()));
        Assert.Empty(playField.FieldEntities.Skulls); // ROM BRNFLG — never a skull on conversion
        Assert.Null(playField.FieldEntities.FamilyList.GetMemberInSlot(familyMember.FamilySlot)); // off the family list now

        // ROM BMUT's placement: just left of the brain (a gap of 1 column), 2 rows below it.
        Assert.Equal(brain.Position.X - familyMember.GetBounds().Width - ScreenSize.ToPortPixelsFromColumns(1),
            familyMember.Position.X);
        Assert.Equal(brain.Position.Y + ScreenSize.ToPortPixelsFromArcadePixels(2), familyMember.Position.Y);

        // …and the placement SETS THE BRAIN'S sprite (BMUT00 `LDD #BRLP1` /
        // BMUT1 `STD OPICT,X`): the family member went to its LEFT, so the brain faces
        // LEFT — BRLP1 is BRNAL's frame 0, i.e. the left base's first frame.
        Assert.Equal(0, brain.GetWalkAnimationFrameIndex());

        // The animation is 20 iterations x 2 redraws x 3 fiftieths of a second = 144 ticks
        // on the clock-unit clock (notes §52; PortTicks(3) = 3 would have made it
        // 120). Through it the brain must not move (it is a solid block, not a
        // chaser).
        var brainSpot = brain.Position;
        for (var tick = 0; tick < 130; tick++) playField.Update(OneTick());

        Assert.True(brain.IsReprogramming());
        Assert.Equal(brainSpot, brain.Position);
        Assert.Equal(0, playField.FieldEntities.Progs.Count(entity => !entity.IsDead()));

        for (var tick = 0; tick < 25; tick++) playField.Update(OneTick());

        // Done: the family member is gone, a PROG stands at its last position and keeps
        // its animation frames and box, and the brain is free to move again.
        Assert.False(brain.IsReprogramming());
        Assert.NotEqual(EntityLifeState.Alive, familyMember.LifeState);
        Assert.Equal(1, playField.FieldEntities.Progs.Count(entity => !entity.IsDead()));
        Assert.Equal(FamilyMemberKind.Mommy, playField.FieldEntities.Progs[0].Kind);
        Assert.Empty(playField.FieldEntities.Skulls);
    }

    [Fact]
    public void Brain_ProggingAFamilyMemberItCannotGetLeftOf_FacesRightInstead()
    {
        // BMUT00 places the family member at brainX - familyMemberWidth - 1 (columns) and, when that
        // would cross XMIN, BMUT10 puts it 8 columns to the RIGHT and loads BRRP1
        // instead — the brain faces the family member either way (author, 2026-09-20:
        // "the brain does not face the human it is progging").
        var playField = CreateField(new WaveParameters(1));
        WarmUp(playField);

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 familyMemberSpot = new(inner.X + 2, inner.Y + 200);
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, familyMemberSpot, FamilyMemberKind.Daddy, new Random(7));
        playField.FieldEntities.FamilyList.Add(familyMember);

        var brain = new Brain(TestTextures.SharedTextureSet, familyMemberSpot, new Random(8), 0, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        RunFirstBeat(playField);

        Assert.True(brain.IsReprogramming());
        // The family member could not fit on the left, so it went right…
        Assert.Equal(brain.Position.X + ScreenSize.ToPortPixelsFromColumns(8), familyMember.Position.X);
        // …and the brain's sprite is BRNAR's frame 0 (BRRP1) — facing RIGHT.
        Assert.Equal(3, brain.GetWalkAnimationFrameIndex());
    }

    [Fact]
    public void Brain_KilledMidReprogram_ReleasesTheFamilyMemberAndLeavesASkull()
    {
        // ROM BRNKIL: past BMUT3 the brain frees the family member and drops a skull —
        // no prog. Without this the victim would be stranded mid-animation
        // (frozen, un-rescuable, never a prog).
        var playField = CreateField(new WaveParameters(1));
        WarmUp(playField);

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 familyMemberSpot = new(inner.X + 200, inner.Y + 200);
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, familyMemberSpot, FamilyMemberKind.Daddy, new Random(31));
        playField.FieldEntities.FamilyList.Add(familyMember);
        var brain = new Brain(TestTextures.SharedTextureSet, familyMemberSpot, new Random(32), 0, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        RunFirstBeat(playField);
        Assert.True(brain.IsReprogramming());

        brain.Kill(); // laser hit, mid-animation
        playField.Update(OneTick());

        Assert.False(familyMember.IsBeingReprogrammed);
        Assert.Equal(EntityLifeState.Dead, familyMember.LifeState);
        Assert.Equal(0, playField.FieldEntities.Progs.Count(entity => !entity.IsDead())); // the conversion never completed
        Assert.Single(playField.FieldEntities.Skulls);
        Assert.False(brain.IsReprogramming());
    }

    [Fact]
    public void Brain_CatchNeedsTheCornersWithinThreePx_NotASpriteOverlap()
    {
        // ROM BRNL1 compares TOP-LEFT CORNERS within ±3px on both axes. The old
        // GetBounds().Overlaps test fired whenever the 14x16 brain box touched the
        // family member box — far too eager.
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 familyMemberSpot = new(inner.X + 200, inner.Y + 200);
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, familyMemberSpot, FamilyMemberKind.Mommy, new Random(14));
        playField.FieldEntities.FamilyList.Add(familyMember);

        // Boxes overlap (the family member is well inside the brain's frame) but the
        // corners are 20px apart → the ROM's reach does not cover it.
        IntVector2 offset = new(ScreenSize.ToPortPixelsFromArcadePixels(20), 0);
        var brain = new Brain(TestTextures.SharedTextureSet, familyMemberSpot - offset, new Random(15), 0, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        RunFirstBeat(playField);

        Assert.False(brain.IsReprogramming());
    }

    [Fact]
    public void Brain_CatchReachSideways_IsThreeColumns_SoSixArcadePixels()
    {
        // ROM BRNL1 `ADDA #3 / CMPA #6` compares X in columns: 3 columns is 6 arcade
        // pixels. 4 arcade pixels apart is inside the X dead zone (2 columns), so the
        // brain does not step, and is still within reach.
        var playField = CreateField(new WaveParameters(1));
        WarmUp(playField);

        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 familyMemberSpot = new(inner.X + 200, inner.Y + 200);
        var familyMember = new FamilyMember(TestTextures.SharedTextureSet, familyMemberSpot, FamilyMemberKind.Mommy, new Random(41));
        playField.FieldEntities.FamilyList.Add(familyMember);

        IntVector2 offset = new(ScreenSize.ToPortPixelsFromArcadePixels(4), 0);
        var brain = new Brain(TestTextures.SharedTextureSet, familyMemberSpot - offset, new Random(42), 0, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);

        RunFirstBeat(playField);

        Assert.True(brain.IsReprogramming());
    }

    [Fact]
    public void Prog_WalksStraightCardinalLines_InsideTheField()
    {
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        // NOT the field centre: the player stands there, and prog contact
        // kills the player (RobotsFrozen() would stop the simulation).
        IntVector2 spot = new(inner.X + 100, inner.Y + 100);
        var prog = new Prog(TestTextures.SharedTextureSet, spot, FamilyMemberKind.Daddy, new Random(6));
        playField.FieldEntities.Progs.Add(prog);
        WarmUp(playField); // run through the start of the wave (RobotsFrozen())

        // The prog re-aims on small odds (3%/9% per beat) and when blocked,
        // so over a long window it WILL turn — the arcade property is that
        // every step is straight (one axis at a time, never diagonal).
        var path = new List<IntVector2> { prog.Position };
        for (var tick = 0; tick < 600; tick++)
        {
            playField.Update(new GameTime());
            Assert.True(prog.GetBounds().Right <= inner.Right && prog.GetBounds().Bottom <= inner.Bottom,
                $"tick {tick}: prog escapes {inner}");
            path.Add(prog.Position);
        }

        for (var i = 1; i < path.Count; i++)
        {
            var step = path[i] - path[i - 1];
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
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        WarmUp(playField);

        int sawHorizontal = 0, sawVertical = 0;
        for (var seed = 0; seed < 12; seed++)
        {
            IntVector2 spot = new(inner.X + 60, inner.Y + 60);
            var prog = new Prog(TestTextures.SharedTextureSet, spot, FamilyMemberKind.Daddy, new Random(seed));
            playField.FieldEntities.Progs.Add(prog);

            for (var tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3); tick++) playField.Update(OneTick());

            var dx = Math.Abs(prog.Position.X - spot.X);
            var dy = Math.Abs(prog.Position.Y - spot.Y);

            // NAP 3 beat (3.6 ticks): exactly one step per beat, one axis only, 4 px either way.
            Assert.True(dx == 0 || dy == 0, $"seed {seed}: diagonal step {dx},{dy}");
            Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(4), dx + dy);

            if (dx != 0)
                sawHorizontal++;
            else
                sawVertical++;

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
        var inner = PlayFieldBuilder.DefaultBounds;

        var xArmed = 0;
        for (var seed = 0; seed < 400; seed++)
        {
            var cruiseMissile = new CruiseMissile(TestTextures.SharedTextureSet,
                new IntVector2(inner.X + 200, inner.Y + 150),
                new IntVector2(inner.X + 300, inner.Y + 150),
                new Random(seed));

            Assert.NotEqual(IntVector2.Zero, cruiseMissile.Velocity); // never stalls
            if (cruiseMissile.Velocity.X != 0) xArmed++;
        }

        // X is armed on 50% of re-aims (SEED bit 7); Y on 75%. The port's old
        // 50/25/25 random pick armed X on 75% — the axis bias was backwards.
        var fraction = xArmed / 400.0;
        Assert.InRange(fraction, 0.42, 0.58);
    }

    [Fact]
    public void CruiseMissile_MovesOneColumnOnXAndOneRowOnYPerCMMOV()
    {
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        WarmUp(playField);

        IntVector2 spot = new(inner.X + 200, inner.Y + 150);
        var cruiseMissile = new CruiseMissile(TestTextures.SharedTextureSet, spot, playField.Player.Position, new Random(7));
        playField.FieldEntities.CruiseMissiles.Add(cruiseMissile);

        // The first beat (2 x 1px CMMOV) lands on tick 4 (3 fiftieths of a second = 3.6),
        // and the re-aim timer cannot fire that early (RND(1..7) >= 1 decrement
        // per beat, re-aim on reaching 0).
        for (var tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3); tick++) playField.Update(OneTick());

        var dx = Math.Abs(cruiseMissile.Position.X - spot.X);
        var dy = Math.Abs(cruiseMissile.Position.Y - spot.Y);

        // Two CMMOVs per beat: `ADDA PD4` steps the X COLUMN byte (1 column = 2
        // arcade px, notes §51) and `ADDB PD5` the Y ROW byte (1 px) — so a beat is
        // 2 columns on X but only 2 rows on Y. The missile really is faster
        // horizontally than vertically; there is no halving here as there is for
        // the player and the tank.
        Assert.True(dx is 0 || dx == ScreenSize.ToPortPixelsFromArcadePixels(4), $"dx {dx} is not 0 or 4 arcade px");
        Assert.True(dy is 0 || dy == ScreenSize.ToPortPixelsFromArcadePixels(2), $"dy {dy} is not 0 or 2 arcade px");
        Assert.True(dx != 0 || dy != 0, "missile did not move at all in its first beat");
    }

    [Fact]
    public void CruiseMissile_DragsARollingNineMarkTail_ThatDiesWithIt()
    {
        // ROM CMMOV writes video memory directly: `$AAAA` (a 16-BIT write, and the
        // address space is column-major with 2 px per byte, so it paints a 2x2 px
        // block of palette index 10) at the new coordinate, and `$DDDD` (palette index 13) at the one
        // it left.
        // The apparent "8 point storage" is a RING OF NINE whose oldest entry
        // is erased off the screen EVERY STEP, so the tail is a fixed nine
        // marks, never a snake across the field (author: "the trail is too
        // long"), and CMKIL then wipes the remaining nine.
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 200, inner.Y + 150);
        var cruiseMissile = new CruiseMissile(TestTextures.SharedTextureSet, spot, playField.Player.Position, new Random(7));
        playField.FieldEntities.CruiseMissiles.Add(cruiseMissile);

        // Two beats = 4 CMMOVs = 4 marks (the missile flies on while the
        // start of the wave holds the robots). Beats land on ticks 4 and 8.
        for (var tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 2; tick++) playField.Update(OneTick());

        Assert.Equal(4, cruiseMissile.Trail.Count);
        Assert.Equal(spot, cruiseMissile.Trail[0]); // the fire position is vacated first
        for (var i = 1; i < cruiseMissile.Trail.Count; i++)
        {
            // Marks are one CMMOV apart: one COLUMN on X (Scaled(2)) or one ROW on
            // Y (Scaled(1)), except across a BEAT boundary, where the re-aim may
            // turn the missile and the offset becomes diagonal (both axes).
            // Nothing may be skipped entirely.
            var gap = Math.Abs(cruiseMissile.Trail[i].X - cruiseMissile.Trail[i - 1].X)
                      + Math.Abs(cruiseMissile.Trail[i].Y - cruiseMissile.Trail[i - 1].Y);
            Assert.InRange(gap, ScreenSize.ToPortPixelsFromArcadePixels(1), ScreenSize.ToPortPixelsFromArcadePixels(3));
        }

        // A long flight must not grow the tail past the ring.
        for (var tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 40; tick++) playField.Update(OneTick());

        Assert.Equal(CruiseMissileTuning.TrailMarks, cruiseMissile.Trail.Count);

        cruiseMissile.Kill(); // CMKIL wipes the remaining marks
        Assert.Empty(cruiseMissile.Trail);
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
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        WarmUp(playField);

        IntVector2 spot = new(inner.X + 200, inner.Y + 100);
        var prog = new Prog(TestTextures.SharedTextureSet, spot, FamilyMemberKind.Daddy, new Random(6));
        playField.FieldEntities.Progs.Add(prog);

        for (var tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 3; tick++) playField.Update(OneTick());

        Assert.Equal(3, prog.GetGhostTrail().Count); // one per beat
        Assert.Equal(spot, prog.GetGhostTrail()[^1]); // newest first, so the spawn spot is last
        Assert.Equal(3, prog.GetGhostFrames().Count);

        // Capped at the ROM's seven — the ring never grows.
        for (var tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(3) * 12; tick++) playField.Update(OneTick());

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
        var playField = CreateField(new WaveParameters(1));
        playField.SkipWaveStart();
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        var prog = new Prog(TestTextures.SharedTextureSet, new IntVector2(inner.X + 120, inner.Y + 120), FamilyMemberKind.Mommy,
            new Random(21));
        playField.FieldEntities.Progs.Add(prog);
        var spot = prog.Position;

        playField.PlayerLaserSlots.Fire(new IntVector2(spot.X + 3, spot.Y - 12), Direction8.Down);
        playField.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, prog.LifeState); // instant off, no pop
        Assert.Equal(0, playField.FieldEntities.Progs.Count(entity => !entity.IsDead()));
        var explosion = Assert.Single(playField.FieldEntities.Explosions);
        // UL = OBJX/OBJY with the sprite's W/H: the 12x16 card's rect at the prog's
        // corner, NOT the smaller family member box it was walking in.
        Assert.Equal(spot.X, explosion.Position.X);
        Assert.Equal(spot.Y, explosion.Position.Y);
        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.ProgBurstSize.Width),
            explosion.GetBounds().Width);
        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.ProgBurstSize.Height),
            explosion.GetBounds().Height);
    }

    [Fact]
    public void CruiseMissile_ReflectsOffWalls_AndStaysInside_OverManyTicks()
    {
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        // Off the player's centre spot so the simulation stays live.
        IntVector2 spot = new(inner.X + 100, inner.Y + 300);
        var cruiseMissile = new CruiseMissile(TestTextures.SharedTextureSet, spot, playField.Player.Position, new Random(7));
        playField.FieldEntities.CruiseMissiles.Add(cruiseMissile);

        for (var tick = 0; tick < 1200; tick++)
        {
            playField.Update(new GameTime());
            Assert.InRange(cruiseMissile.GetBounds().X, inner.X, inner.Right - cruiseMissile.GetBounds().Width);
            Assert.InRange(cruiseMissile.GetBounds().Y, inner.Y, inner.Bottom - cruiseMissile.GetBounds().Height);
        }
    }

    [Fact]
    public void LaserKillsCruiseMissile_Scores25()
    {
        var playField = CreateField(new WaveParameters(1));
        playField.SkipWaveStart();
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 250, inner.Y + 120);
        var cruiseMissile = new CruiseMissile(TestTextures.SharedTextureSet, spot, playField.Player.Position, new Random(8));
        playField.FieldEntities.CruiseMissiles.Add(cruiseMissile);

        playField.PlayerLaserSlots.Fire(new IntVector2(spot.X, spot.Y - 12), Direction8.Down);
        playField.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, cruiseMissile.LifeState);
        Assert.Equal(25, playField.ScoreBoard.Score); // ScoreValues.CruiseMissile
    }

    [Fact]
    public void LaserKillsBrain_Scores500_AndLaserKillsProg_Scores100()
    {
        var playField = CreateField(new WaveParameters(1));
        playField.SkipWaveStart();
        var inner = playField.PlayfieldWall.PlayfieldBounds;

        IntVector2 brainSpot = new(inner.X + 150, inner.Y + 120);
        var brain = new Brain(TestTextures.SharedTextureSet, brainSpot, new Random(9), 0, 40, FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);
        playField.PlayerLaserSlots.Fire(new IntVector2(brainSpot.X + 7, brainSpot.Y - 12), Direction8.Down);
        playField.Update(new GameTime());
        Assert.Equal(EntityLifeState.Dead, brain.LifeState); // BRNKIL: explode, then off — no blink
        Assert.Equal(500, playField.ScoreBoard.Score);

        IntVector2 progSpot = new(inner.X + 300, inner.Y + 120);
        var prog = new Prog(TestTextures.SharedTextureSet, progSpot, FamilyMemberKind.Daddy, new Random(10));
        playField.FieldEntities.Progs.Add(prog);
        playField.PlayerLaserSlots.Fire(new IntVector2(progSpot.X + 5, progSpot.Y - 12), Direction8.Down);
        playField.Update(new GameTime());

        // ROM PRGKIL: `JSR KILL` then the sprite swap, so the prog is gone AT ONCE
        // (no Dying pop) and what remains is the ordinary strip explosion of the
        // 12x16 PGXPIC card it swapped in (notes §90).
        Assert.Equal(EntityLifeState.Dead, prog.LifeState);
        Assert.Equal(600, playField.ScoreBoard.Score); // +100
        Assert.Equal(2, playField.FieldEntities.Explosions.Count); // the brain's and the prog's
        var card = playField.FieldEntities.Explosions[1];
        Assert.Equal(progSpot.X, card.Position.X);
        Assert.Equal(progSpot.Y, card.Position.Y); // UL = OBJX/OBJY, unchanged by PRGKIL
        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.ProgBurstSize.Width),
            card.GetBounds().Width);
        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.ProgBurstSize.Height),
            card.GetBounds().Height);
        Assert.Equal(StripFanAxis.Columns, card.Axis); // a vertical shot -> the H family
    }

    [Fact]
    public void IsWaveCleared_RequiresBrainsGone_ButNotProgsOrMissiles()
    {
        var playField = CreateField(new WaveParameters(1));
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        Assert.True(playField.IsWaveCleared()); // empty wave

        var brain = new Brain(TestTextures.SharedTextureSet, new IntVector2(inner.X + 100, inner.Y + 100), new Random(11), 0, 40,
            FamilyList.FirstSlot);
        playField.FieldEntities.Brains.Add(brain);
        var prog = new Prog(TestTextures.SharedTextureSet, new IntVector2(inner.X + 200, inner.Y + 100), FamilyMemberKind.Daddy,
            new Random(12));
        playField.FieldEntities.Progs.Add(prog);
        var cruiseMissile = new CruiseMissile(TestTextures.SharedTextureSet, new IntVector2(inner.X + 300, inner.Y + 100),
            playField.Player.Position, new Random(13));
        playField.FieldEntities.CruiseMissiles.Add(cruiseMissile);
        Assert.False(playField.IsWaveCleared()); // the brain is still there

        // ROM WVCHEK counts only grunts, spheroids, enforcers, brains, tanks and quarks:
        // a prog and a cruise missile left flying do not hold the wave open.
        brain.Kill();
        for (var tick = 0; tick < 200 && !playField.IsWaveCleared(); tick++) playField.Update(OneTick());

        Assert.True(playField.IsWaveCleared());
        Assert.True(prog.IsAlive());
        Assert.True(cruiseMissile.IsAlive());
    }

    [Fact]
    public void Player_HoldingFire_AutoReFires_AfterTheCooldown_AndCapsAtThreeLasers()
    {
        var playerInputSource = new PhaseInput { State = new PlayerInputState(IntVector2.Zero, IntVector2.Zero, true) };
        var playField = CreateField(new WaveParameters(1), playerInputSource);
        playField.SkipWaveStart();
        var inner = playField.PlayfieldWall.PlayfieldBounds;

        // The player aims down by default; park near the bottom wall so each
        // laser dies within a few ticks and its slot frees up.
        playField.Player.MoveTo(new IntVector2(inner.X + inner.Width / 2, inner.Bottom - 60));

        int aliveLasers()
        {
            return playField.PlayerLaserSlots.Slots.Count(l => l is { LifeState: EntityLifeState.Alive });
        }

        for (var tick = 0; tick < 60; tick++)
        {
            playField.Update(new GameTime());
            Assert.True(aliveLasers() <= LaserSlots.Capacity,
                $"tick {tick}: {aliveLasers()} lasers > {LaserSlots.Capacity}");
        }

        // If firing were press-once (the round-6 complaint), the single
        // laser would be long dead by now and no laser would be alive.
        Assert.True(aliveLasers() >= 1, "no laser alive after 60 held-fire ticks — autofire not repeating");
    }

    [Fact]
    public void SkipWaveHeld_CarriesTheFlagTheWaveClearPathReads()
    {
        // The KEY itself is read in ControlSettings.ReadPlayer (Insert — P is PAUSE; see
        // SpaceAndInsert_StayAsPortAliases); PlayingState's wave-clear check reads the flag this state
        // carries.
        Assert.False(default(PlayerInputState).SkipWaveHeld);
        Assert.True(new PlayerInputState(IntVector2.Zero, IntVector2.Zero, false, true).SkipWaveHeld);
    }

    private sealed class PhaseInput : IPlayerInputSource
    {
        public PlayerInputState State;

        public PlayerInputState Poll()
        {
            return State;
        }
    }
}
