using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
/// Hulk walk animation (RRH11, decoded 2026-09-13 from the ROM — see
/// docs/arcade-fidelity-notes.md progress (21)): each direction block
/// walks an ABAC sequence over the nine verified hulk frames, and every
/// direction change restarts at the block's first frame (ROM HND10:
/// CLRA; STA PD4,U; OPICT = HLKLP1 + first image). The picture list
/// HLKLP1 at $0CF9 maps the blocks to repo frames: LEFT = 1,2,1,3 /
/// RIGHT = 7,8,7,9 / DOWN = UP = 4,5,4,6. Horizontal step length (3/4
/// arcade px) follows the animation entry (even = 3, odd = 4).
/// </summary>
public sealed class HulkAnimationTests
{
    /// <summary>Expected frame (0-based index into HulkFrames = repo hulk N+1) per direction and entry.</summary>
    private static int ExpectedFrame(Direction8 direction, int entry) => direction switch
    {
        Direction8.Left => new[] { 0, 1, 0, 2 }[entry],
        Direction8.Right => new[] { 6, 7, 6, 8 }[entry],
        _ => new[] { 3, 4, 3, 5 }[entry], // Down and Up share the ROM's block
    };

    [Fact]
    public void Hulk_WalksTheRomFrameSequence_AndRestartsOnEveryDirectionChange()
    {
        PlayField field = CreateField();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        // Hunt the playfield center from the right side: the first aim is
        // LEFT; re-aims (RND 1..31 steps or wall contact) flip the axis.
        IntVector2 center = new(bounds.X + bounds.Width / 2 - 16, bounds.Y + bounds.Height / 2 - 16);
        IntVector2 spot = new(bounds.X + 120, bounds.Y + 120);
        var hulk = new Hulk(spot, new Random(19), hulkSpeedRomTicks: 2, () => center);
        field.AddHulk(hulk);

        field.Update(new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3))); // end start grace; first unfrozen update = the spawn aim

        // ROM state after the spawn aim: entry 0 of the aimed block.
        Direction8 lastDirection = hulk.Direction;
        int entry = 0;
        int expectedFrame = ExpectedFrame(lastDirection, 0);
        IntVector2 lastPosition = hulk.Position;
        Assert.Equal(expectedFrame, hulk.AnimationFrameIndex);

        bool reaimed = false;
        for (int i = 0; i < 400; i++)
        {
            field.Update(new GameTime());
            Assert.True(IsFullyInside(hulk.Bounds, bounds), $"hulk left the playfield at update {i}");

            bool moved = hulk.Position != lastPosition;
            reaimed = hulk.Direction != lastDirection;

            if (moved)
            {
                // A step always uses the state's direction and entry BEFORE
                // any re-aim this cycle (a post-step re-aim flips the axis,
                // so the moved direction and the new direction differ).
                int stepEntry = entry;
                int stepArcadePx = lastDirection is Direction8.Up or Direction8.Down
                    ? 2
                    : ((stepEntry & 1) == 0 ? 3 : 4);
                int step = ScreenSize.Scaled(stepArcadePx);
                int expectedDeltaX = lastDirection switch
                {
                    Direction8.Left => -step,
                    Direction8.Right => step,
                    _ => 0,
                };
                int expectedDeltaY = lastDirection switch
                {
                    Direction8.Up => -step,
                    Direction8.Down => step,
                    _ => 0,
                };
                Assert.Equal(lastPosition.X + expectedDeltaX, hulk.Position.X);
                Assert.Equal(lastPosition.Y + expectedDeltaY, hulk.Position.Y);
                expectedFrame = ExpectedFrame(lastDirection, stepEntry);
                entry = (stepEntry + 1) & 3;
            }

            if (reaimed)
            {
                // ROM HND10: the walk restarts at the new block's first frame
                // (this wins over the step's frame when both happen in one
                // cycle — the ROM re-points OPICT at the end of HULKND).
                lastDirection = hulk.Direction;
                entry = 0;
                expectedFrame = ExpectedFrame(lastDirection, 0);
            }

            Assert.Equal(expectedFrame, hulk.AnimationFrameIndex);
            lastPosition = hulk.Position;
        }
    }

    private static bool IsFullyInside(Rectangle rect, Rectangle bounds) =>
        rect.Left >= bounds.Left && rect.Right <= bounds.Right && rect.Top >= bounds.Top && rect.Bottom <= bounds.Bottom;

    private static PlayField CreateField()
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: 0,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0);
        return new PlayField(
            parameters,
            new FakeInputSource(),
            PlayFieldSpawnTests.InnerBounds,
            new WallColorCycle(),
            new Random(1),
            startingLives: 3);
    }
}
