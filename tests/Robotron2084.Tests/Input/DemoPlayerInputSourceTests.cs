using System;
using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Phase 12.1 (notes §94.3): the attract demo's phony player. The arcade's
/// real AI is the OS ROM's ATRSW2 writer (disassembly-only, undecoded), so
/// these tests pin the PLACEHOLDER's behaviour: flee close robots, fire on
/// robots in range, drift to the centre, never flee into a wall, and always
/// emit 8-way/integer directions.
/// </summary>
public sealed class DemoPlayerInputSourceTests
{
    /// <summary>A stick the test can flip (PlayField's input reference is fixed at construction).</summary>
    private sealed class MutableStick : IPlayerInputSource
    {
        public PlayerInputState State { get; set; }
        public PlayerInputState Poll() => State;
    }

    private static readonly Rectangle FullBounds =
        new(ScreenSize.Scaled(20), ScreenSize.Scaled(20), ScreenSize.Width - ScreenSize.Scaled(40), ScreenSize.Height - ScreenSize.Scaled(40));

    private static PlayField EmptyField(Rectangle bounds) => EmptyFieldWithInput(bounds, new FakeInputSource());

    private static PlayField EmptyFieldWithInput(Rectangle bounds, IPlayerInputSource input)
    {
        var parameters = new LevelParameters(LevelNumber: 1);
        return new PlayField(parameters, input, bounds, new WallColorCycle(), new Random(1234), startingLives: 3);
    }

    [Fact]
    public void Poll_WithoutABoundField_IsIdle()
    {
        var demo = new DemoPlayerInputSource();

        PlayerInputState state = demo.Poll();

        Assert.Equal(IntVector2.Zero, state.MoveDirection);
        Assert.False(state.FirePressed);
    }

    [Fact]
    public void Poll_WithACloseRobot_FleesItAndFiresAtIt()
    {
        PlayField field = EmptyField(FullBounds);
        IntVector2 player = field.Player.Position;
        field.SpawnEnforcer(player + new IntVector2(0, -ScreenSize.Scaled(30))); // straight above, inside the threat distance

        var demo = new DemoPlayerInputSource(new Random(7));
        demo.Bind(field);

        PlayerInputState state = demo.Poll();

        // Running straight down, away from it, and firing up at it.
        Assert.Equal(new IntVector2(0, 1), state.MoveDirection);
        Assert.Equal(new IntVector2(0, -1), state.AimDirection);
        Assert.True(state.FirePressed);
    }

    [Fact]
    public void Poll_WithNoRobots_IsIdleAndNotFiring()
    {
        PlayField field = EmptyField(FullBounds); // the player starts exactly at the centre

        var demo = new DemoPlayerInputSource(new Random(7));
        demo.Bind(field);

        PlayerInputState state = demo.Poll();

        Assert.Equal(IntVector2.Zero, state.MoveDirection); // nothing to flee, and the centre is where it is
        Assert.False(state.FirePressed);
    }

    [Fact]
    public void Poll_WithARobotOutOfRange_DriftsToCentreAndDoesNotFire()
    {
        PlayField field = EmptyField(FullBounds);

        // Walk the player off-centre with a plain fake stick, then hand the wheel to the demo.
        var left = new MutableStick { State = new PlayerInputState(new IntVector2(-1, 0), false) };
        field = EmptyFieldWithInput(FullBounds, left);
        for (int i = 0; i < 40; i++)
        {
            field.Update(new GameTime());
        }

        IntVector2 player = field.Player.Position;
        IntVector2 centre = new(field.Wall.PlayfieldBounds.X + field.Wall.PlayfieldBounds.Width / 2, field.Wall.PlayfieldBounds.Y + field.Wall.PlayfieldBounds.Height / 2);
        field.SpawnEnforcer(new IntVector2(centre.X + ScreenSize.Scaled(120), centre.Y)); // well past the fire range

        var demo = new DemoPlayerInputSource(new Random(7));
        demo.Bind(field);

        PlayerInputState state = demo.Poll();

        Assert.Equal(1, state.MoveDirection.X); // back toward the centre
        Assert.Equal(0, state.MoveDirection.Y);
        Assert.False(state.FirePressed);
    }

    [Fact]
    public void Poll_HoldsItsDirectionBeforeFollowingANewThreat()
    {
        PlayField field = EmptyField(FullBounds);
        IntVector2 start = field.Player.Position;
        field.SpawnEnforcer(start + new IntVector2(0, -ScreenSize.Scaled(30))); // above him → flee DOWN

        var demo = new DemoPlayerInputSource(new Random(7));
        demo.Bind(field);

        // The first decision is adopted at once (an empty stick is not a direction).
        Assert.Equal(new IntVector2(0, 1), demo.Poll().MoveDirection);

        // Now the threat is on the other side of him, so the RAW flee direction is
        // UP. The stick must not follow it yet: the arcade RESETS the walk animation
        // on every facing change, and the raw direction flips against a moving field
        // — which is what left the demo's man twitching instead of walking (notes
        // §97.5). It may pause (the deliberate stutter), but it must not reverse.
        field.Player.TeleportTo(start + new IntVector2(0, -ScreenSize.Scaled(40)));

        for (int tick = 0; tick < GameplayConstants.DemoDirectionHoldTicks; tick++)
        {
            Assert.NotEqual(new IntVector2(0, -1), demo.Poll().MoveDirection);
        }

        IntVector2 move = IntVector2.Zero;
        for (int tick = 0; tick < GameplayConstants.DemoDirectionHoldTicks + GameplayConstants.DemoDirectionSwitchTicks + 2
            && move != new IntVector2(0, -1); tick++)
        {
            move = demo.Poll().MoveDirection;
        }

        Assert.Equal(new IntVector2(0, -1), move); // ...and eventually it does follow it
    }

    [Fact]
    public void Poll_NeverFleesIntoAWall()
    {
        // A small field so the player can be pushed close to the left wall.
        Rectangle small = new(0, 0, 200, 200);
        var left = new MutableStick { State = new PlayerInputState(new IntVector2(-1, 0), false) };
        PlayField field = EmptyFieldWithInput(small, left);

        int clearance = ScreenSize.Scaled(GameplayConstants.DemoWallClearanceSpecPixels);
        while (field.Player.Position.X >= clearance)
        {
            field.Update(new GameTime());
        }

        // A robot on the CENTRE side of the player: fleeing it means running
        // into the left wall, which the demo must refuse.
        IntVector2 player = field.Player.Position;
        field.SpawnEnforcer(player + new IntVector2(ScreenSize.Scaled(40), 0));

        var demo = new DemoPlayerInputSource(new Random(7));
        demo.Bind(field);

        PlayerInputState state = demo.Poll();

        Assert.NotEqual(-1, state.MoveDirection.X);
        Assert.InRange(state.MoveDirection.X, -1, 1);
        Assert.InRange(state.MoveDirection.Y, -1, 1);
    }
}
