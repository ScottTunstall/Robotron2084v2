using System;
using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Input;

/// <summary>
/// The PHONY PLAYER for the attract demo (notes §94.3) — the port's
/// stand-in for the arcade's OS-ROM auto-play, which drives the game by writing
/// the fake joystick/fire bytes into ATRSW2/ATRSW3 ($14/$15). The OS ROM is
/// disassembly-only and its writer is not decoded in <c>robomame.asm</c>, so
/// this is an EXPLICITLY-LABELLED PLACEHOLDER, not an arcade claim:
/// <list type="bullet">
/// <item>when a robot is within <c>DemoThreatDistanceSpecPixels</c> the player
/// runs AWAY from it (steering around the walls);</item>
/// <item>otherwise it drifts toward the field centre;</item>
/// <item>it fires at the nearest robot while one is within
/// <c>DemoFireRangeSpecPixels</c>;</item>
/// <item>one tick in <c>DemoStutterChanceDenominator</c> it pauses (a human
/// look, and it lets the demo settle into different shapes run to run);</item>
/// <item>a new direction must win <c>DemoDirectionSwitchTicks</c> ticks in a row
/// before the stick follows it — the raw flee direction flips almost every tick
/// and the arcade resets the walk animation on every facing change, so without
/// the hysteresis the demo's man twitches instead of walking (notes §97.5).</item>
/// </list>
/// All motion is 8-way/integer, per the port's policy. The demo state binds the
/// current field each tick (<see cref="Bind"/>) and then polls — the field must
/// be bound before <see cref="Poll"/> is called.
/// </summary>
public sealed class DemoPlayerInputSource : IPlayerInputSource
{
    private readonly Random _random;
    private PlayField? _field;
    private IntVector2 _heldMove;
    private int _directionVotes;
    private int _holdTicks;

    /// <param name="random">Optional seedable RNG (tests); the demo uses the default.</param>
    public DemoPlayerInputSource(Random? random = null)
    {
        _random = random ?? new Random();
    }

    /// <summary>Attaches the field this poll will reason about (the demo state calls it every tick).</summary>
    public void Bind(PlayField field) => _field = field;

    public PlayerInputState Poll()
    {
        if (_field is not { } field || field.Player.LifeState != EntityLifeState.Alive)
        {
            return new PlayerInputState(IntVector2.Zero, false);
        }

        IntVector2 position = field.Player.Position;
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 centre = new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        IntVector2? nearest = field.NearestLivingRobotPositionTo(position);

        // Default: work the middle of the field, staying mobile.
        IntVector2 move = new IntVector2(Math.Sign(centre.X - position.X), Math.Sign(centre.Y - position.Y));

        if (nearest is { } robot)
        {
            int dx = position.X - robot.X;
            int dy = position.Y - robot.Y;

            if (Math.Abs(dx) + Math.Abs(dy) < ScreenSize.Scaled(GameplayConstants.DemoThreatDistanceSpecPixels))
            {
                // In danger: run away (steering around the walls).
                move = SteerClearOfWalls(new IntVector2(Math.Sign(dx), Math.Sign(dy)), position, bounds, centre);
            }
            // A robot past the fire range just gets ignored for movement — the
            // centre drift stays.
        }

        // Stick hysteresis (notes §97.5): the flee direction is `sign(player − robot)`
        // against a field that moves under it and against a nearest-robot pick that
        // changes as robots die, so the raw value flipped on ~75% of ticks — and the
        // arcade RESETS the walk animation on every facing change, which made the
        // demo's man twitch instead of walk. A direction must win
        // DemoDirectionSwitchTicks ticks in a row before the stick follows it.
        if (move == _heldMove)
        {
            _directionVotes = 0;
        }
        else if (_heldMove == IntVector2.Zero || (_holdTicks <= 0 && ++_directionVotes >= GameplayConstants.DemoDirectionSwitchTicks))
        {
            // An empty stick is "no decision yet", not a direction to defend, so the
            // first move (and the first stop) is adopted at once.
            _heldMove = move;
            _directionVotes = 0;
            _holdTicks = move == IntVector2.Zero ? 0 : GameplayConstants.DemoDirectionHoldTicks;
        }

        if (_holdTicks > 0)
        {
            _holdTicks--;
        }

        move = _heldMove;

        if (move != IntVector2.Zero && _random.Next(GameplayConstants.DemoStutterChanceDenominator) == 0)
        {
            move = IntVector2.Zero;
        }

        // Fire at the nearest robot in range; the aim stick points at it so the
        // port's facing-follow fire rule never overrides the AI's aim.
        if (nearest is { } target &&
            Math.Abs(target.X - position.X) + Math.Abs(target.Y - position.Y)
                < ScreenSize.Scaled(GameplayConstants.DemoFireRangeSpecPixels))
        {
            // 8-way digital: exactly -1/0/1 per component (PlayerInputState contract).
            IntVector2 aim = new(Math.Sign(target.X - position.X), Math.Sign(target.Y - position.Y));
            return new PlayerInputState(move, aim, true);
        }

        return new PlayerInputState(move, false);
    }

    /// <summary>
    /// If the flee direction would push the player into a wall they are already
    /// close to, bend that component toward the field centre instead.
    /// </summary>
    private static IntVector2 SteerClearOfWalls(IntVector2 move, IntVector2 position, Rectangle bounds, IntVector2 centre)
    {
        move = SteerClearOnX(move, position, bounds, centre);
        move = SteerClearOnY(move, position, bounds, centre);

        return new IntVector2(
            Math.Clamp(move.X, -1, 1),
            Math.Clamp(move.Y, -1, 1));
    }

    /// <summary>Bends the X component toward the centre when the flee heads into the left or right wall.</summary>
    /// <param name="move">The flee direction.</param>
    /// <param name="position">The player's position.</param>
    /// <param name="bounds">The playfield interior.</param>
    /// <param name="centre">The field's centre, which the bend is toward.</param>
    /// <returns>The direction, with its X component bent when it was heading into a wall.</returns>
    private static IntVector2 SteerClearOnX(IntVector2 move, IntVector2 position, Rectangle bounds, IntVector2 centre)
    {
        int clearance = ScreenSize.Scaled(GameplayConstants.DemoWallClearanceSpecPixels);
        bool headingIntoWall = (move.X < 0 && position.X < bounds.X + clearance)
            || (move.X > 0 && position.X > bounds.Right - clearance);

        return headingIntoWall
            ? move + new IntVector2(centre.X >= position.X ? 1 : -1, 0)
            : move;
    }

    /// <summary>Bends the Y component toward the centre when the flee heads into the top or bottom wall.</summary>
    /// <param name="move">The flee direction.</param>
    /// <param name="position">The player's position.</param>
    /// <param name="bounds">The playfield interior.</param>
    /// <param name="centre">The field's centre, which the bend is toward.</param>
    /// <returns>The direction, with its Y component bent when it was heading into a wall.</returns>
    private static IntVector2 SteerClearOnY(IntVector2 move, IntVector2 position, Rectangle bounds, IntVector2 centre)
    {
        int clearance = ScreenSize.Scaled(GameplayConstants.DemoWallClearanceSpecPixels);
        bool headingIntoWall = (move.Y < 0 && position.Y < bounds.Y + clearance)
            || (move.Y > 0 && position.Y > bounds.Bottom - clearance);

        return headingIntoWall
            ? move + new IntVector2(0, centre.Y >= position.Y ? 1 : -1)
            : move;
    }
}
