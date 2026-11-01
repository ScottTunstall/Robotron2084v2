using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Input;

/// <summary>
///     (notes §94.3) The attract demo's phony player. The arcade's
///     real AI is the OS ROM's ATRSW2 writer (disassembly-only, undecoded), so
///     these tests pin the PLACEHOLDER's behaviour: flee close robots, fire on
///     robots in range, drift to the centre, never flee into a wall, and always
///     emit 8-way/integer directions.
/// </summary>
public sealed class DemoPlayerInputSourceTests
{
    private static readonly Rectangle FullBounds =
        new(ScreenSize.ToPortPixelsFromArcadePixels(20), ScreenSize.ToPortPixelsFromArcadePixels(20),
            ScreenSize.Width - ScreenSize.ToPortPixelsFromArcadePixels(40),
            ScreenSize.Height - ScreenSize.ToPortPixelsFromArcadePixels(40));

    private static PlayField EmptyField(Rectangle bounds)
    {
        return EmptyFieldWithInput(bounds, new FakeInputSource());
    }

    private static PlayField EmptyFieldWithInput(Rectangle bounds, IPlayerInputSource playerInputSource)
    {
        var waveParameters = new WaveParameters(1);
        return new PlayFieldBuilder().WithParameters(waveParameters).WithInput(playerInputSource).WithBounds(bounds).WithSeed(1234)
            .Build();
    }

    [Fact]
    public void Poll_WithoutABoundField_IsIdle()
    {
        var demoPlayerInputSource = new DemoPlayerInputSource(new Random(7));

        var playerInputState = demoPlayerInputSource.Poll();

        Assert.Equal(IntVector2.Zero, playerInputState.MoveDirection);
        Assert.False(playerInputState.FireHeld);
    }

    [Fact]
    public void Poll_WithACloseRobot_FleesItAndFiresAtIt()
    {
        var playField = EmptyField(FullBounds);
        var player = playField.Player.Position;
        playField.SpawnEnforcer(player +
                            new IntVector2(0,
                                -ScreenSize.ToPortPixelsFromArcadePixels(
                                    30))); // straight above, inside the threat distance

        var demoPlayerInputSource = new DemoPlayerInputSource(new Random(7));
        demoPlayerInputSource.Bind(playField);

        var playerInputState = demoPlayerInputSource.Poll();

        // Running straight down, away from it, and firing up at it.
        Assert.Equal(new IntVector2(0, 1), playerInputState.MoveDirection);
        Assert.Equal(new IntVector2(0, -1), playerInputState.ShootDirection);
        Assert.True(playerInputState.FireHeld);
    }

    [Fact]
    public void Poll_WithNoRobots_IsIdleAndNotFiring()
    {
        var playField = EmptyField(FullBounds); // the player starts exactly at the centre

        var demoPlayerInputSource = new DemoPlayerInputSource(new Random(7));
        demoPlayerInputSource.Bind(playField);

        var playerInputState = demoPlayerInputSource.Poll();

        Assert.Equal(IntVector2.Zero, playerInputState.MoveDirection); // nothing to flee, and the centre is where it is
        Assert.False(playerInputState.FireHeld);
    }

    [Fact]
    public void Poll_WithARobotOutOfRange_DriftsToCentreAndDoesNotFire()
    {
        var playField = EmptyField(FullBounds);
        playField.SkipWaveStart();

        // Walk the player off-centre with a plain fake stick, then hand the wheel to the demo.
        var leftMutableStick = new MutableStick { State = new PlayerInputState(new IntVector2(-1, 0), false) };
        playField = EmptyFieldWithInput(FullBounds, leftMutableStick);
        playField.SkipWaveStart();
        for (var i = 0; i < 40; i++) playField.Update(new GameTime());

        IntVector2 centre = new(playField.PlayfieldWall.PlayfieldBounds.X + playField.PlayfieldWall.PlayfieldBounds.Width / 2,
            playField.PlayfieldWall.PlayfieldBounds.Y + playField.PlayfieldWall.PlayfieldBounds.Height / 2);
        playField.SpawnEnforcer(new IntVector2(centre.X + ScreenSize.ToPortPixelsFromArcadePixels(120),
            centre.Y)); // well past the fire range

        var demoPlayerInputSource = new DemoPlayerInputSource(new Random(7));
        demoPlayerInputSource.Bind(playField);

        var playerInputState = demoPlayerInputSource.Poll();

        Assert.Equal(1, playerInputState.MoveDirection.X); // back toward the centre
        Assert.Equal(0, playerInputState.MoveDirection.Y);
        Assert.False(playerInputState.FireHeld);
    }

    [Fact]
    public void Poll_HoldsItsDirectionBeforeFollowingANewThreat()
    {
        var playField = EmptyField(FullBounds);
        var start = playField.Player.Position;
        playField.SpawnEnforcer(start +
                            new IntVector2(0, -ScreenSize.ToPortPixelsFromArcadePixels(30))); // above him → flee DOWN

        var demoPlayerInputSource = new DemoPlayerInputSource(new Random(7));
        demoPlayerInputSource.Bind(playField);

        // The first decision is adopted at once (an empty stick is not a direction).
        Assert.Equal(new IntVector2(0, 1), demoPlayerInputSource.Poll().MoveDirection);

        // Now the threat is on the other side of him, so the RAW flee direction is
        // UP. The stick must not follow it yet: the arcade RESETS the walk animation
        // on every facing change, and the raw direction flips against a moving field
        // — which is what left the demo's man twitching instead of walking (notes
        // §97.5). It may pause (the deliberate stutter), but it must not reverse.
        playField.Player.MoveTo(start + new IntVector2(0, -ScreenSize.ToPortPixelsFromArcadePixels(40)));

        for (var tick = 0; tick < AttractTuning.DemoDirectionHoldTicks; tick++)
            Assert.NotEqual(new IntVector2(0, -1), demoPlayerInputSource.Poll().MoveDirection);

        var move = IntVector2.Zero;
        for (var tick = 0;
             tick < AttractTuning.DemoDirectionHoldTicks + AttractTuning.DemoDirectionSwitchTicks + 2
             && move != new IntVector2(0, -1);
             tick++)
            move = demoPlayerInputSource.Poll().MoveDirection;

        Assert.Equal(new IntVector2(0, -1), move); // ...and eventually it does follow it
    }

    [Fact]
    public void Poll_NeverFleesIntoAWall()
    {
        // A small field so the player can be pushed close to the left wall.
        Rectangle small = new(0, 0, 200, 200);
        var leftMutableStick = new MutableStick { State = new PlayerInputState(new IntVector2(-1, 0), false) };
        var playField = EmptyFieldWithInput(small, leftMutableStick);

        var clearance = ScreenSize.ToPortPixelsFromArcadePixels(AttractTuning.DemoWallClearanceArcadePixels);
        while (playField.Player.Position.X >= clearance) playField.Update(new GameTime());

        // A robot on the CENTRE side of the player: fleeing it means running
        // into the left wall, which the demo must refuse.
        var player = playField.Player.Position;
        playField.SpawnEnforcer(player + new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(40), 0));

        var demoPlayerInputSource = new DemoPlayerInputSource(new Random(7));
        demoPlayerInputSource.Bind(playField);

        var playerInputState = demoPlayerInputSource.Poll();

        Assert.NotEqual(-1, playerInputState.MoveDirection.X);
        Assert.InRange(playerInputState.MoveDirection.X, -1, 1);
        Assert.InRange(playerInputState.MoveDirection.Y, -1, 1);
    }

    /// <summary>A stick the test can flip (PlayField's input reference is fixed at construction).</summary>
    private sealed class MutableStick : IPlayerInputSource
    {
        public PlayerInputState State { get; set; }

        public PlayerInputState Poll()
        {
            return State;
        }
    }
}
