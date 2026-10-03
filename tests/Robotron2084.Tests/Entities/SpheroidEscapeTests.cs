using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Pins where the spheroid's escape gives up (ROM <c>CIRC3L</c>: <c>CMPA #XMIN+3</c> /
/// <c>CMPA #XMAX-10</c>, i.e. column 10 and column 133 of the video buffer). The exit is
/// only tested on the beat that wraps an animation frame cycle, so a spheroid leaves on the first
/// wrap beat past the line, never exactly on it.
/// </summary>
/// <remarks>This class exists because the two exits are measured from DIFFERENT origins
/// in the port and one of them must be wrong; the test pins today's behaviour so that a
/// change to it is deliberate. See the open question in the code review's A03.</remarks>
public sealed class SpheroidEscapeTests
{
    private static readonly TimeSpan FrameSpan = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    /// <summary>Frames of field.Update needed to expire the player's 2-second start grace.</summary>
    private const int GraceWarmupTicks = 121;

    private static GameTime Frame() => new(TimeSpan.Zero, FrameSpan);

    private static PlayField CreateField(int seed) =>
        new PlayFieldBuilder().WithParameters(new LevelParameters(
                LevelNumber: 1,
                SpheroidCount: 1,
                MaxDropsX2: 2,
                SpheroidDropDelay: 3)).WithRandom(new Random(seed)).Build();

    [Fact]
    public void TheRightEscapeExitIsMeasuredFromTheScreenOrigin()
    {
        int rightExit = ScreenSize.ToPortPixels(2 * SpheroidTuning.EscapeExitRightColumn);

        // The left exit is `bounds.X + Scaled(2 * 10)` = 80 (40 of playfield + 40 of
        // margin); the right exit is Scaled(2 * 133) = 532 with NO playfield term, so
        // it is anchored to the screen's origin instead of the playfield's. The two
        // cannot both be right. This test pins the right one as it stands today: the
        // spheroid leaves on the first wrap beat past 532, i.e. BEFORE
        // bounds.X + 532 = 572. It does not endorse the line, only freezes it.
        for (int seed = 1; seed <= 40; seed++)
        {
            PlayField field = CreateField(seed);
            Spheroid spheroid = field.Spheroids[0];

            if (!StartEscape(field, spheroid) || FollowRightwardEscape(field, spheroid) is not { } escape)
            {
                continue;
            }

            // The line sits between the last wrap beat that did not trigger the exit and
            // the one that did.
            Assert.True(
                escape.LastWrapBeatX < rightExit,
                $"seed {seed}: a wrap beat at x {escape.LastWrapBeatX} was already past the exit line {rightExit}");
            Assert.True(
                escape.DeathX >= rightExit,
                $"seed {seed}: the spheroid left at x {escape.DeathX}, short of the exit line {rightExit}");
            return;
        }

        Assert.Fail("no seed escaped to the right");
    }

    /// <summary>Expires the start grace, then runs the spheroid until it starts escaping.</summary>
    /// <returns>True when the spheroid is escaping.</returns>
    private static bool StartEscape(PlayField field, Spheroid spheroid)
    {
        for (int tick = 0; tick < GraceWarmupTicks; tick++)
        {
            field.Update(Frame());
        }

        for (int guard = 0; guard < 4000 && !spheroid.IsEscaping; guard++)
        {
            spheroid.Update(Frame(), field);
        }

        return spheroid.IsEscaping;
    }

    /// <summary>Follows an escape to its end, recording the last animation-frame wrap beat and where the spheroid left.</summary>
    /// <returns>The two X positions, or null when the spheroid went left or never left the field.</returns>
    private static (int LastWrapBeatX, int DeathX)? FollowRightwardEscape(PlayField field, Spheroid spheroid)
    {
        int startX = spheroid.Position.X;
        int lastWrapBeatX = startX;
        int previousAnimationFrame = spheroid.AnimationFrameIndex;

        for (int tick = 0; tick < 900; tick++)
        {
            spheroid.Update(Frame(), field);

            if (spheroid.Position.X < startX)
            {
                return null; // this seed escaped leftwards
            }

            if (spheroid.LifeState == EntityLifeState.Dead)
            {
                return (lastWrapBeatX, spheroid.Position.X);
            }

            if (spheroid.AnimationFrameIndex < previousAnimationFrame)
            {
                lastWrapBeatX = spheroid.Position.X;
            }

            previousAnimationFrame = spheroid.AnimationFrameIndex;
        }

        return null;
    }
}
