using System;
using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Input;

/// <summary>
/// Phase 12.1 (notes §94.3): the PHONY PLAYER for the attract demo — the port's
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
/// look, and it lets the demo settle into different shapes run to run).</item>
/// </list>
/// All motion is 8-way/integer, per the port's policy. The demo state binds the
/// current field each tick (<see cref="Bind"/>) and then polls — the field must
/// be bound before <see cref="Poll"/> is called.
/// </summary>
public sealed class DemoPlayerInputSource : IPlayerInputSource
{
    private readonly Random _random;
    private PlayField? _field;

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
        int clearance = ScreenSize.Scaled(GameplayConstants.DemoWallClearanceSpecPixels);

        if (move.X != 0 && position.X < bounds.X + clearance && move.X < 0)
        {
            move += new IntVector2(centre.X >= position.X ? 1 : -1, 0);
        }

        if (move.X != 0 && position.X > bounds.Right - clearance && move.X > 0)
        {
            move += new IntVector2(centre.X >= position.X ? 1 : -1, 0);
        }

        if (move.Y != 0 && position.Y < bounds.Y + clearance && move.Y < 0)
        {
            move += new IntVector2(0, centre.Y >= position.Y ? 1 : -1);
        }

        if (move.Y != 0 && position.Y > bounds.Bottom - clearance && move.Y > 0)
        {
            move += new IntVector2(0, centre.Y >= position.Y ? 1 : -1);
        }

        return new IntVector2(
            Math.Clamp(move.X, -1, 1),
            Math.Clamp(move.Y, -1, 1));
    }
}
