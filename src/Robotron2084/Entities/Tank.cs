using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A tank — the slow, thick-skinned robot that only quarks drop. It fires shells.</summary>
/// <seealso cref="Quark"/>
/// <seealso cref="TankShell"/>
/// <remarks>ROM: RRTK4.ASM's <c>TANK</c> process (notes §4.8). A dropped tank is born through four
/// mini-tank pictures (one per 12 ROM frames), immobile until it finishes; each birth picture is a
/// smaller box, so a growing tank is a smaller target. Its beat is this wave's <c>TNKSPD</c> plus
/// 1 frame; each beat it fires if due, steps one arcade px on each active axis, advances the tread
/// frame (backwards while moving left) and re-aims every 1..31 beats — about 38% of aims chase the
/// player, and it only moves vertically when the target is more than 16 arcade px off. The first shot
/// waits <c>TNKSHT</c> plus a random 0..31 beats, and later shots wait exactly the interval; firing
/// is capped at 20 shells in play, so a late tank can stop firing. It never flashes and dies outright
/// when hit. Timers count 5 per tick and 6 per arcade frame, so an interval of N frames is due at
/// 6 x N.</remarks>
public sealed class Tank : IExplodable, IRemovable
{
    private readonly SpriteSet _sprites;
    /// <summary>Collision box = the ROM picture dimensions (14x16 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.TankCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.TankCollisionSize.Height));

    internal static int CollisionWidth => CollisionSize.Width;
    internal static int CollisionHeight => CollisionSize.Height;

    /// <summary>
    /// A tank moves vertically only when its target is more than 16 arcade pixels (32 screen px) off.
    /// </summary>
    /// <remarks>ROM $4E11.</remarks>
    private const int VerticalMoveThresholdScreenPx = 32;

    private readonly Random _random;
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;
    private IntVector2 _step; // current 8-way/0 step vector (±1/0 components)
    private IntVector2 _destination;
    private int _aimBeatsRemaining;
    private int _fireCooldownBeats;

    /// <summary>Counts the tread pictures shown, one per beat.</summary>
    /// <remarks>ROM: <c>TANK3</c> advances the picture once per beat.</remarks>
    private int _treadFrameCounter;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    /// <summary>Which grow (birth) picture is showing, 0..TankGrowSteps.</summary>
    /// <remarks>ROM: <c>MTANK</c> birth.</remarks>
    private int _growStep;

    /// <summary>Counts up to the next grow (birth) picture.</summary>
    private int _growTimer;

    /// <summary>Creates a tank at <paramref name="position"/>; it must be born before it can move or fire.</summary>
    /// <param name="position">Top-left of the tank.</param>
    /// <param name="random">The random source: the aim rolls, the destinations and the first-fire delay.</param>
    /// <param name="fireDelayRomTicks">This wave's firing interval, in ROM frames.</param>
    /// <remarks>ROM: <c>TNKSHT</c> — this wave's firing interval.</remarks>
    public Tank(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireDelayRomTicks = 32)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _destination = position;
        // The first beat fires and moves before the re-aim check (ROM: TANKL).
        _aimBeatsRemaining = 0;
        // The first shot waits the interval plus a random 0..31 beats.
        _fireCooldownBeats = fireDelayRomTicks + random.Next(0, 32);
    }

    /// <summary>Top-left of the tank (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The collision box: the current birth picture's size while being born, else the tank's.</summary>
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

    /// <summary>Kills the tank outright: no death animation.</summary>
    /// <remarks>ROM: RRTK4.ASM's <c>TNKIL</c>.</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Runs the birth animation, then the beat: fire, move, animate and re-aim.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        // ROM MTANK ("MINI TANK GROW"): the tank plays the four mini-tank pictures, one per
        // 12 ROM frames, and cannot move, aim or fire until it finishes.
        if (_growStep < GameplayConstants.TankGrowSteps)
        {
            _growTimer += 5;
            if (_growTimer < GrowPeriod)
            {
                return;
            }

            _growTimer -= GrowPeriod;

            // MTANK applies the current picture's (dx,dy) before advancing, so the mini tank
            // walks up-left and the full tank lands centred on the drop point (notes §53).
            (int columns, int rows) = GameplayConstants.TankGrowDeltas[_growStep];
            _position += new IntVector2(ScreenSize.Scaled(columns * 2), ScreenSize.Scaled(rows));

            if (++_growStep < GameplayConstants.TankGrowSteps)
            {
                return;
            }
        }

        // This wave's tank speed (2 vblanks) plus the 1 frame the process takes (ROM: TNKSPD).
        _beatTimer += 5;
        if (_beatTimer < GameplayConstants.TankBeatRomFrames * 6)
        {
            return;
        }

        _beatTimer -= GameplayConstants.TankBeatRomFrames * 6;

        // One beat, always in this order (ROM: the TANK process).
        if (--_fireCooldownBeats <= 0)
        {
            Direction8? towardPlayer = Direction8Extensions.FromDelta(field.Player.Position - _position);
            if (towardPlayer is { } d && field.CanFireShell)
            {
                field.SpawnTankShell(_position, d.ToIntVector());
            }

            // Later shots reload to exactly this wave's interval — no random padding.
            _fireCooldownBeats = _fireDelayRomTicks;
        }

        // One arcade pixel on each active axis per beat (ROM: the TANK1 step).
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

        _treadFrameCounter++; // one walk frame per beat (ROM: `TANK3` advances the picture once)

        // The re-aim timer counts down in beats; a blocked step already turned the tank (ROM: TANKND).
        if (--_aimBeatsRemaining <= 0)
        {
            _aimBeatsRemaining = NextAimInterval(_random);
            PickDestination(field);
        }
    }

    /// <summary>The next re-aim interval: a random 1..31 beats.</summary>
    /// <remarks>ROM: <c>TANKND</c>.</remarks>
    private static int NextAimInterval(Random random) => random.Next(1, 32);

    /// <summary>How many timer units one grow step takes (a tick adds 5; an arcade frame is 6 units).</summary>
    private static int GrowPeriod => GameplayConstants.TankGrowRomFrames * 6;

    /// <summary>True while the ROM birth sequence is still playing (test hook).</summary>
    internal bool IsBeingBorn => _growStep < GameplayConstants.TankGrowSteps;

    /// <summary>Picks the next destination: the player about 38% of the time, else a random point.</summary>
    /// <remarks>ROM: <c>ANIMATE_TANK</c>.</remarks>
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

    /// <summary>Draws the birth pictures while being born, else the tread frame.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // While being born it draws the ROM's own mini-tank pictures, each at its own size
        // anchored at the object's position (ROM: MTNKP1..4) — not a scaled copy of the tank.
        if (_growStep < GameplayConstants.TankGrowSteps)
        {
            (int gw, int gh) = GameplayConstants.TankGrowSizes[_growStep];
            Rectangle birth = new(
                _position.X,
                _position.Y,
                ScreenSize.Scaled(gw),
                ScreenSize.Scaled(gh));
            _sprites.DrawSprite(spriteBatch, _sprites.TankGrowFrames[_growStep], birth, Color.White);
            return;
        }

        _sprites.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>The frame an explosion would copy (see <see cref="IAnimationFrameSource"/>): the birth picture while being born, else the tread frame.</summary>
    /// <remarks>The walk frame advances once per beat and plays backwards while moving left
    /// (ROM: TANK3 takes the direction from the X step's sign).</remarks>
    public Texture2D CurrentAnimationFrame
    {
        get
        {
            if (_growStep < GameplayConstants.TankGrowSteps)
            {
                return _sprites.TankGrowFrames[_growStep];
            }

            return _sprites.TankFrames[TreadFrameIndex];
        }
    }

    /// <summary>Which tread picture is showing: the index into <see cref="SpriteSet.TankFrames"/>.</summary>
    /// <remarks>Playing backwards while the tank moves left is the ROM's own rule: TANK3 takes the
    /// direction from the X step's sign.</remarks>
    internal int TreadFrameIndex
    {
        get
        {
            int frames = _sprites.TankFrames.Length;
            int forward = _treadFrameCounter % frames;
            return _step.X < 0 ? frames - 1 - forward : forward;
        }
    }
}
