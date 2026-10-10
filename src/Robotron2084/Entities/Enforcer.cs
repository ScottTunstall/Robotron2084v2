using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>An enforcer is a small, fast robot dropped by spheroids. It grows in place for a moment, then flies around firing sparks at you.</summary>
/// <seealso cref="Spheroid"/>
/// <seealso cref="Spark"/>
/// <remarks>
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. The one time it does not is during the
/// short freeze just after the player is killed. <see cref="_beatTimer"/> gathers the ticks until it is time for
/// the next beat (see <see cref="ArcadeClock"/>). It also moves 50 times a second, timed by
/// <see cref="_moveTimer"/>, and <see cref="_growClockUnitsRemaining"/> counts down its growing.
///
/// <list type="bullet">
/// <item>Original source: <c>RRC11.ASM</c>, routine <c>ENFR1</c> (with <c>ENFNV</c>, <c>ENFDRP</c>
/// sub-blocks)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>ENFORCER_AI</c> (<c>$139D</c>). The enforcer is made in
/// <c>DROP_ENFORCER</c>/<c>CREATE_ENFORCER</c></item>
/// </list>
/// </remarks>
public sealed class Enforcer : IEntity, IExplodable, IRemovable
{
    /// <summary>The enforcer heads for a random spot in an area below and to the right of the player. This is how many columns wide that area is.</summary>
    /// <remarks>ROM: <c>ENFR1</c>.</remarks>
    private const int AimZoneColumns = 32;

    /// <summary>How many rows tall the area the enforcer heads for is (see <see cref="AimZoneColumns"/>).</summary>
    /// <remarks>ROM: <c>ENFR1</c>.</remarks>
    private const int AimZoneRows = 32;

    /// <summary>The distance to the spot the enforcer is heading for is divided by this to set <see cref="_velocitySubpixels"/>, which is how far the enforcer goes on each move.</summary>
    private const int ApproachDivisor = 2;

    /// <summary>The most beats an enforcer waits between shots, when it is not told a number.</summary>
    private const int DefaultFireIntervalBeats = 24;

    /// <summary>The enforcer waits a random number of beats before it picks a new spot to head for. That number is from 0 up to one less than this.</summary>
    /// <remarks>ROM: <c>ENFR1</c>.</remarks>
    private const int ReaimBeatsMaxExclusive = 32;

    /// <summary>How big the enforcer is, in port pixels. It is the size of the enforcer's sprite, and it is used to tell what the enforcer touches.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EnforcerCollisionSize.Width), ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EnforcerCollisionSize.Height));

    private readonly int _fireIntervalBeats;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _beatTimer;
    private int _fireCooldownBeats;
    private int _growClockUnitsRemaining;
    private int _moveTimer; // Counts up to the enforcer's next move.
    private IntVector2 _position;

    private int _reaimBeatsRemaining;

    private IntVector2 _remainderSubpixels;

    /// <summary>How far the enforcer goes on each move, sideways and up or down, in 256ths of a pixel. It moves 50 times a second.</summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>Makes an enforcer. It cannot move until it has grown.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the enforcer's top-left corner is.</param>
    /// <param name="random">Where its random numbers come from. They pick where it heads for and how long it waits between shots.</param>
    /// <param name="fireIntervalBeats">The most beats this wave's enforcer waits between shots. Each wait is a random number of beats from 1 up to this.</param>
    /// <remarks>ROM: <c>ENSTIM</c>.</remarks>
    public Enforcer(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireIntervalBeats = DefaultFireIntervalBeats)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _growClockUnitsRemaining = ArcadeClock.ToClockUnits(EnforcerTuning.GrowUpRomFrames);
        // The enforcer picks where to go on its first beat. Its first shot comes after a random number of beats.
        _reaimBeatsRemaining = 0;
        _fireCooldownBeats = 1 + random.Next(0, _fireIntervalBeats);
        // The move timer starts full, so the enforcer does not have to wait for its first move.
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>The box the enforcer takes up on the screen. It is used to tell what the enforcer touches.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The animation frame the enforcer is showing: one of the growing ones while it grows, and the full-size one after that.</summary>
    /// <remarks>The growing animation frames are the ROM's <c>ENGD1</c> to <c>ENGD5</c>. They are the second to the sixth in the set.</remarks>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (!this.IsAlive() || _growClockUnitsRemaining <= 0)
        {
            return _sprites.EnforcerSprite;
        }

        int frame = Math.Clamp(GetGrowAnimationFrameIndex(), 0, _sprites.EnforcerAnimationFrames.Length - 2);
        return _sprites.EnforcerAnimationFrames[1 + frame];
    }

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the enforcer's top-left corner is.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>Which of the growing animation frames is showing, counting from 0, or -1 once the enforcer has grown.</summary>
    internal int GetGrowAnimationFrameIndex() => _growClockUnitsRemaining > 0
        ? (ArcadeClock.ToClockUnits(EnforcerTuning.GrowUpRomFrames) - _growClockUnitsRemaining)
            / ArcadeClock.ToClockUnits(EnforcerTuning.GrowStepRomFrames)
        : -1;

    /// <summary>Draws the enforcer: a growing animation frame while it grows, and the full-size one after that.</summary>
    /// <param name="spriteBatch">What the enforcer is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>Kills the enforcer at once. It has no death animation (ROM: <c>ENFKIL</c>).</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick. The enforcer grows, then it moves, and on a beat it picks where to head for and fires a spark if it is time to.</summary>
    /// <param name="gameTime">Not used. The enforcer counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        if (field.RobotsFrozen())
        {
            return;
        }

        // A growing enforcer cannot move or shoot. This counts down the time it has left to grow.
        if (_growClockUnitsRemaining > 0)
        {
            _growClockUnitsRemaining -= ArcadeClock.UnitsPerPortTick;
            if (_growClockUnitsRemaining > 0)
            {
                return;
            }
        }

        // The enforcer moves 50 times a second, as it did in the arcade. A tick comes 60 times a second, so it does not move on every tick (see ArcadeClock).
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            AdvancePosition(field);
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < ArcadeClock.ToClockUnits(EnforcerTuning.BeatIntervalRomFrames))
        {
            return;
        }

        _beatTimer -= ArcadeClock.ToClockUnits(EnforcerTuning.BeatIntervalRomFrames);

        // A beat has come, which is the enforcer's turn to act. It counts down to its next aim and to its next shot (ROM: ENFR1).
        if (--_reaimBeatsRemaining <= 0)
        {
            _reaimBeatsRemaining = NextReaimBeats(_random);
            RollVelocity(field);
        }

        if (--_fireCooldownBeats <= 0)
        {
            _fireCooldownBeats = NextFireBeats(_random);
            if (field.GetActiveSparkCount() < SparkTuning.GlobalActiveSparkCap)
            {
                // The spark is aimed at the place where the Player is now.
                field.SpawnSpark(_position, field.Player.Position);
            }
        }
    }

    /// <summary>Picks at random how many beats the enforcer waits before it picks a new spot to head for.</summary>
    private static int NextReaimBeats(Random random) => random.Next(0, ReaimBeatsMaxExclusive);

    /// <summary>Makes one move. The sideways part of the move is made only if it keeps the enforcer inside the playfield, and the same goes for the up-and-down part.</summary>
    /// <param name="field">The playfield, whose walls the enforcer stays inside.</param>
    /// <remarks>ROM: the routine that moves every moving object (RRS22.ASM's <c>OPB80</c>).</remarks>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _remainderSubpixels += _velocitySubpixels;
        int stepX = _remainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
        int stepY = _remainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
        _remainderSubpixels -= new IntVector2(stepX * ScreenSize.SubpixelsPerPixel, stepY * ScreenSize.SubpixelsPerPixel);

        int x = _position.X + stepX;
        if (stepX != 0 && x >= bounds.X && x + CollisionSize.Width <= bounds.Right)
        {
            _position = _position with { X = x };
        }

        int y = _position.Y + stepY;
        if (stepY != 0 && y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom)
        {
            _position = _position with { Y = y };
        }
    }

    /// <summary>Picks at random how many beats the enforcer waits before its next shot.</summary>
    private int NextFireBeats(Random random) => random.Next(1, _fireIntervalBeats + 1);

    /// <summary>Picks the next spot to head for, which is a random distance below and to the right of the player, and sets the enforcer's speed to get there.</summary>
    /// <param name="field">The playfield. It says where the player is, and its walls limit where the spot can be.</param>
    private void RollVelocity(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int targetX = field.GetPlayerPosition().X + ScreenSize.ToPortPixelsFromColumns(_random.Next(0, AimZoneColumns));
        int targetY = field.GetPlayerPosition().Y + ScreenSize.ToPortPixelsFromArcadePixels(_random.Next(0, AimZoneRows));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // The further the enforcer is from the spot it is heading for, the faster it goes.
        _velocitySubpixels = new IntVector2(delta.X / ApproachDivisor, delta.Y / ApproachDivisor);
    }
}

