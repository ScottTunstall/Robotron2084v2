using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Entities;

/// <summary>
///     R5 player walk animation (MOVE_PLAYER $2FD0) + two-stick IJKL aim
///     (2026-09-12 playtest request). Port frame N = R5 frame N (ROM image
///     table $35EB + (N-1)*4, images $361B..$382B). Walk sequences: left
///     1,2,1,3 / right 4,5,4,6 / down 7,8,7,9 / up 10,11,10,12 (diagonals
///     reuse the horizontal sequence), 2 arcade frames per frame, frozen while the
///     stick is centred, wave start = frame 7 (first DOWN frame).
/// </summary>
public sealed class PlayerAnimationAndAimTests
{
    private static PlayField CreateField(IPlayerInputSource playerInputSource)
    {
        return new PlayFieldBuilder().WithParameters(new WaveParameters(1)).WithInput(playerInputSource).WithSeed(1).Build();
    }

    [Fact]
    public void Player_StartsOnFrame7_TheFirstDownFrame()
    {
        // WAVE_START_PLAYER points the player metadata at $3603 = frame 7.
        var playField = CreateField(new FakeInputSource());
        Assert.Equal(6, playField.Player.GetWalkAnimationFrameIndex()); // 0-based index into PlayerAnimationFrames
    }

    [Fact]
    public void Player_WalkCycle_IsTwoArcadeFramesPerFrame_InTheArcadeABACPattern()
    {
        var playField = CreateField(new FakeInputSource(new PlayerInputState(new IntVector2(-1, 0), false)));
        playField.SkipWaveStart();

        // Two seconds of walking left: 100 arcade frames, so 100 runs of MOVE_PLAYER.
        var ticks = 2 * ArcadeClock.PortTicksPerSecond;
        var arcadeFrames = 2 * ArcadeClock.ArcadeFramesPerSecond;
        List<int> runs = [playField.Player.GetWalkAnimationFrameIndex()];
        for (var tick = 0; tick < ticks; tick++)
        {
            playField.Update(new GameTime());
            var frame = playField.Player.GetWalkAnimationFrameIndex();
            if (frame != runs[^1]) runs.Add(frame);
        }

        // It starts on the first DOWN frame and turns to the left walk at once. Left walk = frames 1,2,1,3, which are
        // PlayerAnimationFrames indices 0,1,0,2; after the turn the next frame shows every second arcade frame.
        Assert.Equal([6, 0, 1, 0, 2, 0], runs.Take(6));
        Assert.Equal(1 + 1 + (arcadeFrames - 1) / Player.WalkAnimationIntervalArcadeFrames, runs.Count);
    }

    [Fact]
    public void Fire_WithoutAim_UsesTheMovementFacing()
    {
        var playField = CreateField(new FakeInputSource(new PlayerInputState(new IntVector2(1, 1), IntVector2.Zero, true)));
        playField.SkipWaveStart();
        playField.Update(new GameTime());

        Assert.Equal(Direction8.DownRight, playField.Player.FacingDirection);
        var laser = Assert.Single(playField.PlayerLaserSlots.GetActiveLasers());
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
        var playField = CreateField(new FakeInputSource(new PlayerInputState(IntVector2.Zero, new IntVector2(1, 0), true)));
        playField.SkipWaveStart();
        playField.Update(new GameTime());

        Assert.Equal(Direction8.Up, playField.Player.FacingDirection); // the initial facing, untouched
        var laser = Assert.Single(playField.PlayerLaserSlots.GetActiveLasers());
        Assert.Equal(Direction8.Right, laser.Direction);
        Assert.Equal(6, playField.Player.GetWalkAnimationFrameIndex()); // idle frame, untouched
    }

    [Fact]
    public void Animation_FollowsMovementOnly_AimNeverChangesFacing_OrFreezesWhenIdle()
    {
        var playerInputSource = new PhaseInput { State = new PlayerInputState(new IntVector2(-1, 0), false) };
        var playField = CreateField(playerInputSource);
        playField.SkipWaveStart();

        for (var tick = 0; tick < 4; tick++) playField.Update(new GameTime());

        Assert.Equal(1, playField.Player.GetWalkAnimationFrameIndex()); // left walk, frame 2 (index 1)

        // Round 8: aim UP while still moving left — the facing and the walk
        // animation must stay LEFT (the old behaviour switched the sprite to
        // the UP group; the playtest called that out).
        playerInputSource.State = new PlayerInputState(new IntVector2(-1, 0), new IntVector2(0, -1), false);
        playField.Update(new GameTime());

        Assert.Equal(Direction8.Left, playField.Player.FacingDirection);
        Assert.InRange(playField.Player.GetWalkAnimationFrameIndex(), 0, 2); // still the left walk group

        // Idle: the animation freezes on the current frame (R5 $2FFE early-out).
        playerInputSource.State = default;
        var frozen = playField.Player.GetWalkAnimationFrameIndex();
        for (var tick = 0; tick < 12; tick++)
        {
            playField.Update(new GameTime());
            Assert.True(playField.Player.GetWalkAnimationFrameIndex() == frozen, $"idle tick {tick + 1} changed the frame");
        }
    }

    [Fact]
    public void Fire_UsesTheAimDirection_WhileMoving_OtherWay()
    {
        // Round 8 contract in one shot: moving LEFT, aiming RIGHT, firing —
        // the laser goes RIGHT, the facing (and thus the walk animation)
        // stays LEFT.
        var playerInputSource = new PhaseInput { State = new PlayerInputState(new IntVector2(-1, 0), new IntVector2(1, 0), true) };
        var playField = CreateField(playerInputSource);
        playField.SkipWaveStart();

        playField.Update(new GameTime());

        Assert.Equal(Direction8.Left, playField.Player.FacingDirection);
        var laser = Assert.Single(playField.PlayerLaserSlots.GetActiveLasers());
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
    public void Fire_LaserSpawnsAtTheArcadeLtabMuzzleOffset(Direction8 direction, int offsetXSpec, int offsetYSpec)
    {
        var playField = CreateField(
            new FakeInputSource(new PlayerInputState(IntVector2.Zero, direction.ToIntVector(), true)));
        playField.SkipWaveStart();
        var start = playField.Player.Position;
        playField.Update(new GameTime());

        var laser = Assert.Single(playField.PlayerLaserSlots.GetActiveLasers());
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
