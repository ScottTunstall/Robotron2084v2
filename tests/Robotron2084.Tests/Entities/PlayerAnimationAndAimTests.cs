using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     R5 player walk animation (MOVE_PLAYER $2FD0) + two-stick IJKL aim
///     (2026-09-12 playtest request). Port frame N = R5 frame N (ROM image
///     table $35EB + (N-1)*4, images $361B..$382B). Walk sequences: left
///     1,2,1,3 / right 4,5,4,6 / down 7,8,7,9 / up 10,11,10,12 (diagonals
///     reuse the horizontal sequence), 3 ticks per frame, frozen while the
///     stick is centered, wave start = frame 7 (first DOWN frame).
/// </summary>
public sealed class PlayerAnimationAndAimTests
{
    private static PlayField CreateField(IPlayerInputSource input)
    {
        return new PlayFieldBuilder().WithParameters(new LevelParameters(1)).WithInput(input).WithSeed(1).Build();
    }

    [Fact]
    public void Player_StartsOnFrame7_TheFirstDownFrame()
    {
        // WAVE_START_PLAYER points the player metadata at $3603 = frame 7.
        var field = CreateField(new FakeInputSource());
        Assert.Equal(6, field.Player.GetWalkAnimationFrameIndex()); // 0-based index into PlayerAnimationFrames
    }

    [Fact]
    public void Player_WalkCycle_IsThreeTicksPerFrame_InTheRomABACPattern()
    {
        var field = CreateField(new FakeInputSource(new PlayerInputState(new IntVector2(-1, 0), false)));
        field.SkipWaveStart();

        // Left walk = frames 1,2,1,3 → PlayerAnimationFrames indices 0,1,0,2,
        // each drawn for exactly 3 movement ticks.
        int[] expected = { 0, 0, 0, 1, 1, 1, 0, 0, 0, 2, 2, 2 };

        for (var tick = 0; tick < expected.Length; tick++)
        {
            field.Update(new GameTime());
            Assert.True(
                field.Player.GetWalkAnimationFrameIndex() == expected[tick],
                $"tick {tick + 1}: expected frame index {expected[tick]}, got {field.Player.GetWalkAnimationFrameIndex()}");
        }
    }

    [Fact]
    public void Fire_WithoutAim_UsesTheMovementFacing()
    {
        var field = CreateField(new FakeInputSource(new PlayerInputState(new IntVector2(1, 1), IntVector2.Zero, true)));
        field.SkipWaveStart();
        field.Update(new GameTime());

        Assert.Equal(Direction8.DownRight, field.Player.FacingDirection);
        var laser = Assert.Single(field.PlayerLasers.GetActiveLasers());
        Assert.Equal(Direction8.DownRight, laser.Direction);
    }

    [Fact]
    public void Fire_WithAim_FollowsTheIJKLStick_WithoutMoving()
    {
        // No movement, aim right, fire: the laser must go RIGHT. Round 8:
        // the aim must NOT change the facing or the walk animation — the
        // sprite keeps its initial DOWN facing ("IJKL will shoot in the
        // required direction ... the shooting direction should not specify
        // the walking animation").
        var field = CreateField(new FakeInputSource(new PlayerInputState(IntVector2.Zero, new IntVector2(1, 0), true)));
        field.SkipWaveStart();
        field.Update(new GameTime());

        Assert.Equal(Direction8.Up, field.Player.FacingDirection); // the initial facing, untouched
        var laser = Assert.Single(field.PlayerLasers.GetActiveLasers());
        Assert.Equal(Direction8.Right, laser.Direction);
        Assert.Equal(6, field.Player.GetWalkAnimationFrameIndex()); // idle frame, untouched
    }

    [Fact]
    public void Animation_FollowsMovementOnly_AimNeverChangesFacing_OrFreezesWhenIdle()
    {
        var input = new PhaseInput { State = new PlayerInputState(new IntVector2(-1, 0), false) };
        var field = CreateField(input);
        field.SkipWaveStart();

        for (var tick = 0; tick < 4; tick++) field.Update(new GameTime());

        Assert.Equal(1, field.Player.GetWalkAnimationFrameIndex()); // left walk, frame 2 (index 1)

        // Round 8: aim UP while still moving left — the facing and the walk
        // animation must stay LEFT (the old behaviour switched the sprite to
        // the UP group; the playtest called that out).
        input.State = new PlayerInputState(new IntVector2(-1, 0), new IntVector2(0, -1), false);
        field.Update(new GameTime());

        Assert.Equal(Direction8.Left, field.Player.FacingDirection);
        Assert.InRange(field.Player.GetWalkAnimationFrameIndex(), 0, 2); // still the left walk group

        // Idle: the animation freezes on the current frame (R5 $2FFE early-out).
        input.State = default;
        var frozen = field.Player.GetWalkAnimationFrameIndex();
        for (var tick = 0; tick < 12; tick++)
        {
            field.Update(new GameTime());
            Assert.True(field.Player.GetWalkAnimationFrameIndex() == frozen, $"idle tick {tick + 1} changed the frame");
        }
    }

    [Fact]
    public void Fire_UsesTheAimDirection_WhileMoving_OtherWay()
    {
        // Round 8 contract in one shot: moving LEFT, aiming RIGHT, firing —
        // the laser goes RIGHT, the facing (and thus the walk animation)
        // stays LEFT.
        var input = new PhaseInput { State = new PlayerInputState(new IntVector2(-1, 0), new IntVector2(1, 0), true) };
        var field = CreateField(input);
        field.SkipWaveStart();

        field.Update(new GameTime());

        Assert.Equal(Direction8.Left, field.Player.FacingDirection);
        var laser = Assert.Single(field.PlayerLasers.GetActiveLasers());
        Assert.Equal(Direction8.Right, laser.Direction);
    }

    /// <summary>
    ///     ROM LTAB muzzle offsets (RRG23; notes 2026-09-12 (19) + 2026-09-13 (21)):
    ///     the laser box top-left spawns at the player box top-left plus the
    ///     per-direction offset (arcade pixels), then moves one LaserSpeed step in the
    ///     same field update it is fired (Player updates before PlayerLasers).
    /// </summary>
    [Theory]
    [InlineData(Direction8.Up, 2, -1)]
    [InlineData(Direction8.Down, 2, 4)]
    [InlineData(Direction8.Left, 0, 4)]
    [InlineData(Direction8.Right, 2, 4)]
    [InlineData(Direction8.UpLeft, 0, 0)]
    [InlineData(Direction8.DownLeft, 0, 8)]
    [InlineData(Direction8.UpRight, 2, 0)]
    [InlineData(Direction8.DownRight, 2, 12)]
    public void Fire_LaserSpawnsAtTheRomLtabMuzzleOffset(Direction8 direction, int offsetXSpec, int offsetYSpec)
    {
        var field = CreateField(
            new FakeInputSource(new PlayerInputState(IntVector2.Zero, direction.ToIntVector(), true)));
        field.SkipWaveStart();
        var start = field.Player.Position;
        field.Update(new GameTime());

        var laser = Assert.Single(field.PlayerLasers.GetActiveLasers());
        Assert.Equal(direction, laser.Direction);
        IntVector2 muzzle = new(start.X + ScreenSize.ToPortPixelsFromArcadePixels(offsetXSpec),
            start.Y + ScreenSize.ToPortPixelsFromArcadePixels(offsetYSpec));
        Assert.Equal(muzzle + direction.ToIntVector() * PlayerTuning.LaserSpeed, laser.Position);
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
