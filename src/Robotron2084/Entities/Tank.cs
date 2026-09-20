using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A tank — the slow, thick-skinned robot that only Quarks drop (never at level
/// start). A dropped tank is BORN: it grows through four mini-tank pictures and
/// cannot move, aim or fire until the birth finishes. It then re-aims at random
/// intervals of 1..31 bodies; about 38% of those aims chase the player and the
/// rest pick a random point in the field (and it only moves vertically when it
/// is more than 16 arcade px off its target on that axis). It steps one arcade
/// pixel per body on each active axis, and its walk animation runs backwards
/// while it moves left. It never flashes.
///
/// Its first shot comes after this wave's firing interval plus a random delay,
/// and every shot after that waits exactly the interval. Firing is also gated on
/// the field's cap of 20 shells at once, so late in a wave a tank can stop
/// firing altogether. A laser kills it outright, with no death animation.
/// </summary>
/// <remarks>
/// ROM behaviour (notes §4.8/§11.5/§52): dropped onto the MINI tank picture and BORN — `MTANK`
/// plays the four mini-tank pictures, one per NAP 12 ROM frames, during which
/// the tank does not move, aim or fire. Only then does it re-aim every
/// RND(1..31) ticks; each aim roll (RND 0..255 ≤ $60, i.e. ~38%) makes it seek
/// the player, otherwise it walks toward a random point (and only vertically
/// when more than 16 arcade px off). Moves one pixel per tick, axis-aligned
/// ±1/0. It never flashes (notes §50 — the old blue visible-20/hidden-10 gate
/// was invented). First shot after RND(0..31)+TNKSHT, then every TNKSHT —
/// gated on the arcade's 20-shells-per-wave counter (the fizzle bug: the
/// count only drops when a shell is killed, so late in the wave tanks
/// simply stop firing). Walk animation runs backwards when moving left.
/// </remarks>
public sealed class Tank : IEntity, IExplodable
{
    /// <summary>Collision box = the ROM picture dimensions (14x16 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.TankCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.TankCollisionSize.Height));

    internal static int CollisionWidth => CollisionSize.Width;
    internal static int CollisionHeight => CollisionSize.Height;

    /// <summary>
    /// A tank moves vertically only when its target is more than 16 arcade
    /// pixels (= 32 screen px) away on that axis.
    /// </summary>
    /// <remarks>ROM 4E11.</remarks>
    private const int VerticalMoveThresholdScreenPx = 32;

    private readonly Random _random;
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;
    private IntVector2 _step; // current 8-way/0 step vector (±1/0 components)
    private IntVector2 _destination;
    private int _aimBodiesRemaining;
    private int _fireCooldownBodies;
    private int _animationTicks;

    /// <summary>Sub-tick carry for the body, in exact 6ths of a tick (a ROM frame is 6/5 tick).</summary>
    private int _bodyFifths;

    /// <summary>Which grow (birth) picture is showing, 0..TankGrowSteps.</summary>
    /// <remarks>ROM `MTANK` birth.</remarks>
    private int _growStep;

    /// <summary>Sub-tick carry for the grow step, in exact 6ths of a tick (a ROM frame is 6/5 tick).</summary>
    private int _growFifths;

    /// <summary>Creates a tank at <paramref name="position"/>; it must be born before it can move or fire.</summary>
    /// <param name="position">Top-left of the tank.</param>
    /// <param name="random">The random source: the aim rolls, the destinations and the first-fire delay.</param>
    /// <param name="fireDelayRomTicks">This wave's firing interval, in ROM frames.</param>
    /// <param name="speedBonus">Unused by this entity (kept for the field's uniform spawn shape).</param>
    /// <remarks>ROM TNKSHT for this wave (notes §11.2).</remarks>
    public Tank(IntVector2 position, Random random, int fireDelayRomTicks = 32, int speedBonus = 0)
    {
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _destination = position;
        // ROM TANKL: the first body fires the shot check, moves, and only THEN
        // tests PD7 — so the destination is picked on the first body after the
        // birth completes, not before it (the "picks it immediately at creation"
        // note described TNKSTV, the wave-start path this port never uses).
        _aimBodiesRemaining = 0;
        // ROM 4CE4-4CEA: initial fire countdown = TNKSHT + RND(0..31) BODIES.
        _fireCooldownBodies = fireDelayRomTicks + random.Next(0, 32);
    }

    /// <summary>Top-left of the tank (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>
    /// The collision box. While the tank is being BORN it is the CURRENT birth
    /// picture's size — every mini-tank picture is smaller than the full tank,
    /// so a growing tank is genuinely a smaller target.
    /// </summary>
    /// <remarks>
    /// The ROM bounds and collides against the frame it is showing (notes §53).
    /// </remarks>
    public Rectangle Bounds
    {
        get
        {
            if (_growStep < GameplayConstants.TankGrowSteps)
            {
                (int w, int h) = GameplayConstants.TankGrowSizes[_growStep];
                return new Rectangle(_position.X, _position.Y, ScreenSize.Scaled(w), ScreenSize.Scaled(h));
            }

            return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
        }
    }

    /// <summary>Alive until shot or until it walks into an electrode; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>
    /// Kills the tank: it goes straight to Dead, with no death animation, so the
    /// explosion the playfield draws is the entire visual.
    /// </summary>
    /// <remarks>
    /// Laser kill (ROM RRTK4 `TNKIL`): `JSR HVEXST` (explode) then `JSR
    /// KILROB` and `JSR KILL` — the object is gone immediately. Same shape as
    /// the brain's BRNKIL, and again with no death animation (notes §50).
    /// </remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Runs one step when the tank's clocks say so: the birth animation first (frozen),
    /// then the body — fire, move one pixel on each active axis, advance the tread frame and
    /// re-aim when the direction timer expires. Held still entirely while
    /// <see cref="PlayField.RobotsFrozen"/>.
    /// </summary>
    /// <param name="gameTime">Unused — the birth and body clocks are counted in ROM frames.</param>
    /// <param name="field">The playfield: the player to aim at, the wall to bounce off and the shell cap.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        _animationTicks++;

        if (field.RobotsFrozen)
        {
            return;
        }

        // ROM MTANK ("MINI TANK GROW"): a quark-dropped tank is BORN. It plays
        // the four mini-tank pictures — one per NAP 12 ROM frames — and does NOT
        // move, aim or fire until the pointer reaches TNKP1. This is the
        // behaviour the author reported missing (2026-09-16: "tanks spawn
        // instantly whereas they are 'born' like the enforcer"); the "picks its
        // destination immediately" note belonged to TNKSTV, the WAVE-START path,
        // which the port never uses.
        // A ROM frame is 6/5 of a port tick, so accumulating in 6ths keeps the
        // 12-frame step exact (12 frames = 14.4 ticks, not the 14 that integer
        // division gives).
        if (_growStep < GameplayConstants.TankGrowSteps)
        {
            _growFifths += 5;
            if (_growFifths < GrowFifthsPerStep)
            {
                return;
            }

            _growFifths -= GrowFifthsPerStep;

            // MTANK applies the CURRENT picture's (dx,dy) to the object's address
            // and only THEN advances the pointer (notes §53), so the mini tank
            // walks up-left as it grows and the full 14x16 tank lands centred on
            // the drop point — a total of -2 columns and -6 rows.
            (int columns, int rows) = GameplayConstants.TankGrowDeltas[_growStep];
            _position += new IntVector2(ScreenSize.Scaled(columns * 2), ScreenSize.Scaled(rows));

            if (++_growStep < GameplayConstants.TankGrowSteps)
            {
                return;
            }
        }

        // The body: TNKSPD (2) vblanks plus the frame the process runs in = 3 ROM
        // frames = 3.6 port ticks. Accumulating in 6ths keeps it exact.
        _bodyFifths += 5;
        if (_bodyFifths < GameplayConstants.TankBodyRomFrames * 6)
        {
            return;
        }

        _bodyFifths -= GameplayConstants.TankBodyRomFrames * 6;

        // One body of the ROM's TANK process, in its order:
        //   DEC PD6 -> fire ; X += PD4>>1, Y += PD5 ; walk frame ; DEC PD7 -> re-aim
        if (--_fireCooldownBodies <= 0)
        {
            Direction8? towardPlayer = Direction8Extensions.FromDelta(field.Player.Position - _position);
            if (towardPlayer is { } d && field.CanFireShell)
            {
                field.SpawnTankShell(_position, d.ToIntVector());
            }

            // ROM 4E46-4E50: after firing, countdown = TNKSHT exactly (no extra RND).
            _fireCooldownBodies = _fireDelayRomTicks;
        }

        // ROM TANK1: one arcade px on each active axis per BODY (see
        // TankStepArcadePixels — the port used to move one unit per tick).
        int stepPixels = ScreenSize.Scaled(GameplayConstants.TankStepArcadePixels);
        IntVector2 step = new(_step.X * stepPixels, _step.Y * stepPixels);
        IntVector2 next = _position + step;
        if (field.Wall.Intersects(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            // Bounce off the wall (never crosses): mirror the axis that is blocked.
            if (field.Wall.Intersects(new Rectangle(_position.X + step.X, _position.Y, CollisionSize.Width, CollisionSize.Height)))
            {
                _step = new IntVector2(-_step.X, _step.Y);
                step = new IntVector2(-step.X, step.Y);
                next = _position + step;
            }
            if (field.Wall.Intersects(new Rectangle(_position.X, _position.Y + step.Y, CollisionSize.Width, CollisionSize.Height)))
            {
                _step = new IntVector2(_step.X, -_step.Y);
            }
        }
        else
        {
            _position = next;
        }

        _animationTicks++; // one walk frame per body (ROM TANK3 advances OPICT once)

        // ROM TANKND: the direction timer counts down in BODIES too, and a blocked
        // step re-aims immediately (the wall bounce above already turned the tank).
        if (--_aimBodiesRemaining <= 0)
        {
            _aimBodiesRemaining = NextAimInterval(_random);
            PickDestination(field);
        }
    }

    /// <summary>The next re-aim interval: a random 1..31 bodies.</summary>
    /// <remarks>ROM TANKND.</remarks>
    private static int NextAimInterval(Random random) => random.Next(1, 32);

    /// <summary>One ROM grow step in 6ths of a port tick (`TankGrowRomFrames` x 6).</summary>
    private static int GrowFifthsPerStep => GameplayConstants.TankGrowRomFrames * 6;

    /// <summary>True while the ROM birth sequence is still playing (test hook).</summary>
    internal bool IsBeingBorn => _growStep < GameplayConstants.TankGrowSteps;

    /// <summary>
    /// Picks the next destination: about 38% of the time the player, otherwise a
    /// random point in the playfield. The vertical component is set only when the
    /// target is more than 16 arcade px away vertically.
    /// </summary>
    /// <remarks>
    /// ROM ANIMATE_TANK (4E11-4E46): RND(0..255) ≤ $60 (~38%) → destination =
    /// player; otherwise a random point in the playfield.
    /// </remarks>
    private void PickDestination(PlayField field)
    {
        _destination = _random.Next(256) <= 96
            ? field.Player.Position
            : RandomPointIn(field);

        int dx = Math.Sign(_destination.X - _position.X);
        int dy = Math.Abs(_destination.Y - _position.Y) > VerticalMoveThresholdScreenPx
            ? Math.Sign(_destination.Y - _position.Y)
            : 0;
        _step = new IntVector2(dx, dy);
    }

    /// <summary>A random point inside the playfield, inset by the tank's own box.</summary>
    private IntVector2 RandomPointIn(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        return new IntVector2(
            _random.Next(bounds.X + CollisionSize.Width, bounds.Right - CollisionSize.Width + 1),
            _random.Next(bounds.Y + CollisionSize.Height, bounds.Bottom - CollisionSize.Height + 1));
    }

    /// <summary>Draws the birth pictures while it is being born, else the tread frame (backwards when moving left).</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the tank and birth frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // NO flash: the tank is always drawn (author, 2026-09-16: "Quarks and
        // tanks should not flash"). The counter above is now purely the frame
        // clock for the tread animation below.

        // No death animation: the ROM turns the object off and bursts it
        // immediately (notes §50) — see Kill().

        // While it is being BORN the tank draws the ROM's own mini-tank pictures,
        // MTNKP1..4 (notes §53) — each at its own size, anchored at the object's
        // position, exactly as the blitter does. NOT a scaled copy of the tank.
        if (_growStep < GameplayConstants.TankGrowSteps)
        {
            (int gw, int gh) = GameplayConstants.TankGrowSizes[_growStep];
            Rectangle birth = new(
                _position.X,
                _position.Y,
                ScreenSize.Scaled(gw),
                ScreenSize.Scaled(gh));
            sprites.DrawSprite(spriteBatch, sprites.TankGrowFrames[_growStep], birth, Color.White);
            return;
        }

        // ROM walk cycle (TANK3): OPICT advances one picture per BODY, and it walks
        // BACKWARDS while the tank moves left (`TST PD4,U` picks the sign).
        int frames = sprites.TankFrames.Length;
        int forward = _animationTicks % frames;
        int frame = _step.X < 0 ? frames - 1 - forward : forward;
        sprites.DrawSprite(spriteBatch, sprites.TankFrames[frame], Bounds, Color.White);
    }

    /// <summary>The frame an explosion would copy (see <see cref="IArtSource"/>): the birth picture while being born, else the tread frame.</summary>
    /// <param name="sprites">The shared sprite set, which holds the tank and birth frames.</param>
    /// <returns>The birth picture while it is being born, else the current tread frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
    {
        if (_growStep < GameplayConstants.TankGrowSteps)
        {
            return sprites.TankGrowFrames[_growStep];
        }

        int frames = sprites.TankFrames.Length;
        int forward = _animationTicks % frames;
        int frame = _step.X < 0 ? frames - 1 - forward : forward;
        return sprites.TankFrames[frame];
    }
}
