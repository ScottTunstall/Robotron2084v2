using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
/// The quark's tank-drop TIMER (notes §87). The drop countdown is only advanced on the pass
/// that wraps the quark's idle animation — five animation frames, so six beats — which is why the
/// first tank arrives seconds after the quark does rather than on its second beat.
/// </summary>
public sealed class QuarkDropTimerTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int seed) =>
        new PlayFieldBuilder().WithParameters(new LevelParameters(LevelNumber: 7, SpheroidCount: 0, MaxDropsX2: 10, SpheroidDropDelay: 18,
                QuarkDropDelay: 16)).WithRandom(new Random(seed)).Build();

    [Fact]
    public void TheFirstTankWaitsForWholeIdleAnimationCycles()
    {
        // SQUARE reads `ADDD #4 / CMPD #SQP4 / BLS SQ1` and puts `DEC PD2,U` on the WRAP
        // branch, so PD2 counts CYCLES of the idle animation: five animation frames to walk plus the
        // pass that wraps = six beats. With TDPTIM = 1 (the shortest first timer the ROM can
        // roll) the first tank is therefore due six beats after the quark starts running —
        // never on the next beat, which is what counting beats did and why the author saw
        // "the Quarks are dropping tanks WAY too quickly".
        PlayField field = CreateField(2);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        var quark = new Quark(TestSprites.Shared, 
            new IntVector2(bounds.X + 100, bounds.Y + 60), new Random(4),
            maxDropsX2: 10, dropDelayBeats: 1, speedCap: 50);
        field.AddQuark(quark);

        int tick = 0;
        for (; tick < 800 && field.GetTankCount() == 0; tick++)
        {
            field.Update(Frame());
        }

        int beatTicks = (int)Math.Ceiling(QuarkTuning.BeatIntervalRomFrames * 1.2); // 6/5 a frame
        int oneCycle = (QuarkTuning.TravelAnimationFrames + 1) * beatTicks;

        Assert.True(field.GetTankCount() > 0, "the quark must eventually drop a tank");
        Assert.True(tick > oneCycle,
            $"the first tank came {tick} ticks after spawn; one idle cycle is {oneCycle}");
    }

    [Fact]
    public void TheIdlePhaseSurvivesManyBodiesWithoutDroppingAnything()
    {
        // The corollary: with the ROM's own first timer for a wave (TDPTIM 16 at wave 7) the
        // quark can idle for a long time. Measured over five seeds before §87 the FIRST drop
        // landed within 0-47 ticks; it now needs whole cycles of six beats, so a quark that
        // spawns mid-field cannot have dropped a tank after its first beat.
        PlayField field = CreateField(1);
        Rectangle bounds = field.Wall.PlayfieldBounds;

        var quark = new Quark(TestSprites.Shared, 
            new IntVector2(bounds.X + 100, bounds.Y + 60), new Random(11),
            maxDropsX2: 10, dropDelayBeats: 16, speedCap: 50);
        field.AddQuark(quark);

        for (int tick = 0; tick < ArcadeClock.ToPortTicksRoundedUp(QuarkTuning.BeatIntervalRomFrames) + 2; tick++)
        {
            field.Update(Frame());
        }

        Assert.Equal(0, field.GetTankCount());
        Assert.Equal(EntityLifeState.Alive, quark.LifeState);
    }
}