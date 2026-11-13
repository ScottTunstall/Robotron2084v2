using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.States.SpriteTest;

/// <summary>Makes the SPRITE TEST page's list: every sprite in the game, each animated as the game animates it.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Each sprite's frames and timings come from the entity that shows it. A speed that changes from wave to
///             wave is the one in the first wave that has the sprite, taken from <see cref="WaveTable" />.
///         </item>
///         <item>
///             A sprite that walks a different way for each way held shows that walk while the way is held, and stays on
///             its frame when none is.
///         </item>
///         <item>
///             A sprite that grows or shrivels shows its whole change, holds the end of it for
///             <see cref="EndHoldArcadeFrames" />, then starts again.
///         </item>
///         <item>A sprite that never animates in the game is shown still.</item>
///     </list>
/// </remarks>
public static class SpriteTestEntries
{
    /// <summary>
    ///     How long a sprite that grows or shrivels is held on its last frame before it starts again, in arcade frames:
    ///     one second. The game has no such wait; it is there so that the change can be seen again and again.
    /// </summary>
    private const int EndHoldArcadeFrames = ArcadeClock.ArcadeFramesPerSecond;

    /// <summary>Makes the list, in the order the page shows it.</summary>
    /// <param name="textureSet">The textures the sprites are drawn from.</param>
    public static IReadOnlyList<SpriteTestEntry> Create(TextureSet textureSet)
    {
        List<SpriteTestEntry> entries =
        [
            CreatePlayer(textureSet),
            CreateFamilyMember("MOMMY", textureSet.MommyAnimationFrameTextures),
            CreateFamilyMember("DADDY", textureSet.DaddyAnimationFrameTextures),
            CreateFamilyMember("MIKEY", textureSet.MikeyAnimationFrameTextures),
            CreateGrunt(textureSet),
            CreateHulk(textureSet),
            CreateBrain(textureSet),
            CreateProg("PROG MOMMY", FamilyMemberKind.Mommy.GetAnimationFrames(textureSet)),
            CreateProg("PROG DADDY", FamilyMemberKind.Daddy.GetAnimationFrames(textureSet)),
            CreateProg("PROG MIKEY", FamilyMemberKind.Mikey.GetAnimationFrames(textureSet)),
            SpriteTestEntry.CreateUnsteerable("SPHEROID",
                SpriteTestCycle.CreateFromArcadeFrames(textureSet.SpheroidAnimationFrameTextures.Take(Spheroid.SpinLastAnimationFrame + 1),
                    SpheroidTuning.BeatIntervalArcadeFrames)),
            SpriteTestEntry.CreateUnsteerable("ENFORCER", SpriteTestCycle.CreateStill(textureSet.EnforcerTexture)),
            SpriteTestEntry.CreateUnsteerable("ENFORCER GROWING", CreateGrowth(
                textureSet.EnforcerAnimationFrameTextures.Skip(1).Take(EnforcerTuning.GrowSteps), EnforcerTuning.GrowIntervalArcadeFrames,
                textureSet.EnforcerTexture)),
            SpriteTestEntry.CreateUnsteerable("ENFORCER SPARK",
                SpriteTestCycle.CreateFromArcadeFrames(textureSet.SparkAnimationFrameTextures, SparkTuning.BeatIntervalArcadeFrames)),
            SpriteTestEntry.CreateUnsteerable("QUARK",
                SpriteTestCycle.CreateFromArcadeFrames(textureSet.QuarkAnimationFrameTextures.Take(QuarkTuning.TravelAnimationFrames),
                    QuarkTuning.BeatIntervalArcadeFrames)),
            CreateTank(textureSet),
            SpriteTestEntry.CreateUnsteerable("TANK GROWING", CreateGrowth(
                textureSet.TankGrowAnimationFrameTextures, TankTuning.GrowIntervalArcadeFrames, textureSet.TankAnimationFrameTextures[0])),
            SpriteTestEntry.CreateUnsteerable("TANK SHELL", SpriteTestCycle.CreateStill(textureSet.TankShellTexture)),
            .. CreateElectrodes(textureSet),
            SpriteTestEntry.CreateUnsteerable("SKULL", SpriteTestCycle.CreateStill(textureSet.SkullTexture)),
            .. textureSet.RescueBonusAnimationFrameTextures.Select((texture, index) =>
                SpriteTestEntry.CreateUnsteerable($"RESCUE BONUS {index + 1}", SpriteTestCycle.CreateStill(texture))),
            SpriteTestEntry.CreateUnsteerable("PROG BURST", SpriteTestCycle.CreateStill(textureSet.ProgBurstTexture)),
            SpriteTestEntry.CreateUnsteerable("SPARE LIFE", SpriteTestCycle.CreateStill(textureSet.SpareLifeTexture)),
            SpriteTestEntry.CreateUnsteerable("STORY CRUISE", SpriteTestCycle.CreateStill(textureSet.AttractCruiseTexture)),
            .. textureSet.AttractElectrodeAnimationFrameTextures.Select((texture, index) =>
                SpriteTestEntry.CreateUnsteerable($"STORY ELECTRODE {index + 1}", SpriteTestCycle.CreateStill(texture))),

            // The author's own robots, which the arcade does not have, come last.
            CreateBerzerkRobot(textureSet),
            SpriteTestEntry.CreateUnsteerable("GORF",
                SpriteTestCycle.CreateFromArcadeFrames(textureSet.GorfAnimationFrameTextures, GorfTuning.AnimationIntervalArcadeFrames)) with
            {
                WalkOn = new SpriteTestWalkOn([GorfTuning.StepPixels], ArcadeClock.ToClockUnits(GorfTuning.BeatIntervalArcadeFrames),
                    Gorf.GetHopRisePixels)
            }
        ];
        return entries;
    }

    /// <summary>Makes an entry whose walk depends on the way held, with one cycle made up front for each walk.</summary>
    /// <typeparam name="TWalk">What tells the walks apart, such as a <see cref="WalkSequence" />.</typeparam>
    /// <param name="name">The name shown in the list.</param>
    /// <param name="getWalk">Gets the walk for a way, by the entity's own rule.</param>
    /// <param name="createCycle">Makes the cycle for a walk.</param>
    /// <param name="cycleWhenNoneHeld">The cycle shown when no way is held, or null to stay on the frame showing.</param>
    private static SpriteTestEntry CreateSteerable<TWalk>(string name, Func<Direction8, TWalk> getWalk,
        Func<TWalk, SpriteTestCycle> createCycle, SpriteTestCycle? cycleWhenNoneHeld = null)
        where TWalk : notnull
    {
        Dictionary<TWalk, SpriteTestCycle> cyclesByWalk = [];
        Dictionary<Direction8, SpriteTestCycle> cyclesByDirection = [];
        foreach (var direction in Enum.GetValues<Direction8>())
        {
            var walk = getWalk(direction);
            if (!cyclesByWalk.TryGetValue(walk, out var cycle))
            {
                cycle = createCycle(walk);
                cyclesByWalk[walk] = cycle;
            }

            cyclesByDirection[direction] = cycle;
        }

        return new SpriteTestEntry(name, direction => direction is { } held ? cyclesByDirection[held] : cycleWhenNoneHeld, true);
    }

    private static SpriteTestEntry CreatePlayer(TextureSet textureSet)
    {
        return CreateSteerable("PLAYER", Player.GetWalkSequence, walkSequence => SpriteTestCycle.CreateFromArcadeFrames(
            Enumerable.Range(0, Player.WalkCycleLength)
                .Select(step => textureSet.PlayerAnimationFrameTextures[Player.GetWalkAnimationFrameIndex(walkSequence, step)]),
            Player.WalkAnimationIntervalArcadeFrames)) with
        {
            WalkOn = new SpriteTestWalkOn([PlayerTuning.PlayerSpeedX], ArcadeClock.UnitsPerPortTick)
        };
    }

    private static SpriteTestEntry CreateFamilyMember(string name, Texture2D[] animationFrameTextures)
    {
        return CreateSteerable(name, FamilyMember.GetDirectionBlock, directionBlock => SpriteTestCycle.CreateFromArcadeFrames(
            Enumerable.Range(0, FamilyMember.SubStepsPerDirection)
                .Select(subStep => animationFrameTextures[FamilyMember.GetWalkAnimationFrameIndex(directionBlock, subStep)]),
            FamilyMember.BeatIntervalArcadeFrames)) with
        {
            WalkOn = new SpriteTestWalkOn([
                .. Enumerable.Range(0, FamilyMember.SubStepsPerDirection).Select(subStep => ScreenSize.ToPortPixelsFromArcadePixels(
                    -FamilyMember.GetSidewaysStepArcadePixels(FamilyMember.GetDirectionBlock(Direction8.Left), subStep)))
            ])
        };
    }

    /// <summary>Makes the grunt: it walks the same whichever way it goes, and steps after a random number of beats.</summary>
    private static SpriteTestEntry CreateGrunt(TextureSet textureSet)
    {
        var waveParameters = GetFirstWaveWith(WaveTable.Grunts);
        return SpriteTestEntry.CreateUnsteerable("GRUNT", SpriteTestCycle.CreateFromArcadeFrames(
            Enumerable.Range(1, Grunt.WalkAnimationFrameCount)
                .Select(arcadeAnimationFrameNumber => textureSet.GruntAnimationFrameTextures[Grunt.GetAnimationFrameIndex(arcadeAnimationFrameNumber)]),
            Grunt.BeatIntervalArcadeFrames, waveParameters.GruntMoveDelay)) with
        {
            WalkOn = new SpriteTestWalkOn([GruntChaseStep.StepXPixels])
        };
    }

    private static SpriteTestEntry CreateHulk(TextureSet textureSet)
    {
        var waveParameters = GetFirstWaveWith(WaveTable.Hulks);
        return CreateSteerable("HULK", direction => direction, direction => SpriteTestCycle.CreateFromArcadeFrames(
            Hulk.GetFrames(direction).Select(frame => textureSet.HulkAnimationFrameTextures[frame]),
            waveParameters.HulkBeatIntervalArcadeFrames)) with
        {
            WalkOn = new SpriteTestWalkOn([
                .. Enumerable.Range(0, Hulk.GetFrames(Direction8.Left).Length)
                    .Select(step => ScreenSize.ToPortPixelsFromArcadePixels(Hulk.GetSidewaysStepArcadePixels(step)))
            ])
        };
    }

    private static SpriteTestEntry CreateBrain(TextureSet textureSet)
    {
        var waveParameters = GetFirstWaveWith(WaveTable.Brains);
        return CreateSteerable("BRAIN", direction => Brain.GetWalkSequence(direction.ToIntVector()), walkSequence =>
            SpriteTestCycle.CreateFromArcadeFrames(
                Enumerable.Range(0, Brain.WalkCycleLength)
                    .Select(step => textureSet.BrainAnimationFrameTextures[Brain.GetWalkAnimationFrameIndex(walkSequence, step)]),
                Brain.GetBeatIntervalArcadeFrames(waveParameters.BrainBeatWaitArcadeFrames))) with
        {
            WalkOn = new SpriteTestWalkOn([Brain.StepXPixels])
        };
    }

    private static SpriteTestEntry CreateProg(string name, Texture2D[] animationFrameTextures)
    {
        return CreateSteerable(name, Prog.GetWalkSequence, walkSequence => SpriteTestCycle.CreateFromArcadeFrames(
                Enumerable.Range(0, Prog.WalkCycleLength)
                    .Select(step => animationFrameTextures[Prog.GetWalkAnimationFrameIndex(walkSequence, step)]),
                Prog.BeatIntervalArcadeFrames)) with
            {
                DrawStyle = SpriteTestDrawStyle.SolidWithBackground,
                ShapePaletteIndex = ProgTuning.ShapePaletteIndex,
                BackgroundPaletteIndex = ProgTuning.BackgroundPaletteIndex,
                WalkOn = new SpriteTestWalkOn([ScreenSize.ToPortPixelsFromColumns(Prog.StepXColumns)])
            };
    }

    /// <summary>Makes the tank: its treads run backwards while it goes left, and forwards otherwise, even when no way is held.</summary>
    private static SpriteTestEntry CreateTank(TextureSet textureSet)
    {
        var treadFrames = textureSet.TankAnimationFrameTextures;
        SpriteTestCycle CreateTreads(bool isGoingLeft)
        {
            return SpriteTestCycle.CreateFromArcadeFrames(
                Enumerable.Range(0, treadFrames.Length)
                    .Select(counter => treadFrames[Tank.GetTreadFrameIndex(counter, treadFrames.Length, isGoingLeft)]),
                TankTuning.BeatIntervalArcadeFrames);
        }

        var forwards = CreateTreads(false);
        var backwards = CreateTreads(true);
        return new SpriteTestEntry("TANK", direction => direction?.ToIntVector().X < 0 ? backwards : forwards, true);
    }

    /// <summary>
    ///     Makes the Berzerk robot: it plays its waiting animation until it walks, then walks the way held, stepping
    ///     after a random number of beats as a grunt does.
    /// </summary>
    private static SpriteTestEntry CreateBerzerkRobot(TextureSet textureSet)
    {
        var waveParameters = GetFirstWaveWith(WaveTable.Grunts);
        var idle = SpriteTestCycle.CreateFromArcadeFrames(textureSet.BerzerkRobotIdleAnimationFrameTextures,
            BerzerkRobot.BeatIntervalArcadeFrames);

        // Four steps is a whole walk whichever way it goes: a sideways walk has two frames, and an up or down walk four.
        const int stepsInAWholeWalk = 4;
        return CreateSteerable("BERZERK ROBOT",
            direction => BerzerkRobot.GetWalkSequenceTowards(IntVector2.Zero, direction.ToIntVector()),
            walkSequence => SpriteTestCycle.CreateFromArcadeFrames(
                Enumerable.Range(0, stepsInAWholeWalk).Select(step =>
                    BerzerkRobot.GetWalkFrames(textureSet, walkSequence)[BerzerkRobot.GetWalkFrameIndex(walkSequence, step)]),
                BerzerkRobot.BeatIntervalArcadeFrames, waveParameters.GruntMoveDelay),
            idle) with
        {
            // It takes a grunt's step (BerzerkRobot.Step).
            WalkOn = new SpriteTestWalkOn([GruntChaseStep.StepXPixels])
        };
    }

    /// <summary>
    ///     Makes one entry for each shape of electrode, in the colour of the first wave that has it. Each stands, then
    ///     shrivels as it does when it is shot.
    /// </summary>
    private static IEnumerable<SpriteTestEntry> CreateElectrodes(TextureSet textureSet)
    {
        var variantCount = textureSet.ElectrodeAnimationFrameTextures.Length / WavePaletteTables.ElectrodeAnimationFramesPerVariant;
        for (var variant = 0; variant < variantCount; variant++)
        {
            var wave = GetFirstWaveWithElectrodeVariant(variant);
            var baseFrame = variant * WavePaletteTables.ElectrodeAnimationFramesPerVariant;
            var frames = Electrode.ShrivelIntervalArcadeFrames.Select((holdArcadeFrames, shrivelStep) =>
            {
                // The standing frame is also the first shrivel frame, so the stand is added to its hold.
                var standArcadeFrames = shrivelStep == 0 ? EndHoldArcadeFrames : 0;
                return new SpriteTestFrame(textureSet.ElectrodeAnimationFrameTextures[baseFrame + shrivelStep],
                    ArcadeClock.ToClockUnits(standArcadeFrames + holdArcadeFrames));
            });
            yield return SpriteTestEntry.CreateUnsteerable($"ELECTRODE {variant + 1}", new SpriteTestCycle([.. frames])) with
            {
                DrawStyle = SpriteTestDrawStyle.Solid,
                ShapePaletteIndex = WavePaletteTables.GetElectrodePaletteIndex(wave)
            };
        }
    }

    /// <summary>Makes a cycle that grows through its frames, then holds the grown frame for <see cref="EndHoldArcadeFrames" />.</summary>
    private static SpriteTestCycle CreateGrowth(IEnumerable<Texture2D> growTextures, int growIntervalArcadeFrames, Texture2D grownTexture)
    {
        var growClockUnits = ArcadeClock.ToClockUnits(growIntervalArcadeFrames);
        return new SpriteTestCycle(
        [
            .. growTextures.Select(texture => new SpriteTestFrame(texture, growClockUnits)),
            new SpriteTestFrame(grownTexture, ArcadeClock.ToClockUnits(EndHoldArcadeFrames))
        ]);
    }

    /// <summary>Gets the parameters of the first wave whose count in a wave table column is more than 0.</summary>
    /// <param name="countByWave">A column of <see cref="WaveTable" />, one count per wave.</param>
    private static WaveParameters GetFirstWaveWith(int[] countByWave)
    {
        var wave = Array.FindIndex(countByWave, count => count > 0) + 1;
        return WaveParameters.CreateFromWave(wave, WaveTable.GetParameters(wave));
    }

    /// <summary>Gets the first wave that shows an electrode shape.</summary>
    /// <param name="variant">The shape.</param>
    /// <exception cref="ArgumentOutOfRangeException">No wave shows the shape.</exception>
    private static int GetFirstWaveWithElectrodeVariant(int variant)
    {
        var wavesBeforeTheShapesRepeat = WavePaletteTables.ElectrodeVariantByWaveMod10.Length;
        for (var wave = 1; wave <= wavesBeforeTheShapesRepeat; wave++)
            if (WavePaletteTables.GetElectrodeVariant(wave) == variant) return wave;

        throw new ArgumentOutOfRangeException(nameof(variant), variant, "No wave shows this electrode shape.");
    }
}
