using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The Ground Roving UNit Terminator. ALWAYS lumbs toward the player
/// (never away), with a STAGGER: the ROM body ($39CD, every 4 vblanks)
/// counts down a random timer re-rolled to RND(1..ROBSPD) ($D042: A×RND
/// with INCA, never 0) each time a step happens — a uniform random
/// 1..ROBSPD bodies between steps (notes §29). A step moves 4 arcade px
/// (8 screen px) on each axis more than 2 arcade px (4 screen px) from the
/// player — axes are independent, so diagonal steps occur; closer axes
/// hold. The RWDP frame advances ONLY when a step happens — DRAW_GRUNT
/// ($3A2B) sits in the step branch ($39E6), not the common path, so a
/// paused grunt freezes mid-pose (legs stop). The field speeds up ALL
/// surviving grunts (limit × 7/8 truncated, floored at the current floor —
/// RMXSPD, which itself descends to 1 = player speed over the wave) each
/// time one dies — the late-wave rush. Killed by a player
/// laser, or by walking onto an electrode (both die). Frozen while
/// <c>RobotsFrozen</c>, still killable by lasers.
/// </summary>
public sealed class Grunt : IEntity, IExplodable
{
    /// <summary>Collision box = the ROM picture dimensions (10x13 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.GruntCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.GruntCollisionSize.Height));

    /// <summary>One ROM step = 4 arcade px = 8 screen px on the active axis.</summary>
    private const int StepScreenPixels = 8;

    /// <summary>Per-axis dead zone: 2 arcade px = 4 screen px (R5 $3A19/$39F5).</summary>
    private const int DeadZoneScreenPixels = 4;

    /// <summary>ROM body cadence: LDA #$04 — every 4 vblanks (R5 $3A70).</summary>
    private const int BodyIntervalRomTicks = 4;

    /// <summary>
    /// The body period in exact 6ths of a port tick (notes §52): a ROM frame is 6/5
    /// of a port tick, so 4 frames is 4.8 ticks — `PortTicks(4)` truncates to 4,
    /// which made every grunt 17-20% fast (the same bug the quark/tank/spheroid/
    /// enforcer bodies had fixed in §52).
    /// </summary>
    private static int BodyFifths => BodyIntervalRomTicks * 6;

    private readonly Random _random;
    private IntVector2 _position;
    private int _moveLimitRomBodies;
    private int _bodyFifths;
    private int _moveCountdownBodies;
    private int _walkFrame = 1; // ROM RWDP frame 1..4 (RRP8: LDD #RWDP1 at spawn)

    /// <param name="moveLimitRomBodies">ROM ROBSPD for this wave — the RND(1..N) re-roll limit (notes §29).</param>
    public Grunt(IntVector2 position, int moveLimitRomBodies = 15, int speedBonus = 0, Random? random = null)
    {
        _position = position;
        _moveLimitRomBodies = moveLimitRomBodies;
        _random = random ?? new Random();
        // R5 $38E1-38E7: spawn countdown = RND(1..ROBSPD) bodies.
        _moveCountdownBodies = _random.Next(1, _moveLimitRomBodies + 1);
    }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Current re-roll limit in ROM bodies (the field lowers it when grunts die).</summary>
    public int MoveDelayRomTicks => _moveLimitRomBodies;

    /// <summary>
    /// R5 $3A94-$3A9F (per grunt death): `LDB #$E0 / LDA $BE5C / MUL / CMPA $BE5D`
    /// — the limit becomes the HIGH byte of `limit × 224` (= limit × 7/8,
    /// truncated) **only if that value is still at or above the floor**; otherwise
    /// the limit is LEFT ALONE. That is not the same as clamping to the floor: the
    /// old `max(floor, limit*7/8)` pushed the limit DOWN to the floor a kill early
    /// (and so made the grunts faster than the arcade's).
    /// <para>
    /// The ROM also does NOT touch any pending countdown here — the shared $BE5C
    /// changes and each grunt picks it up on its NEXT re-roll, so the survivors
    /// accelerate gradually. Re-rolling every survivor's in-flight countdown (as the
    /// port did) collapsed their pending delays to the new, smaller range on every
    /// kill, which made the wave speed up far too early.
    /// </para>
    /// </summary>
    public void SpeedUp(int floorRomBodies)
    {
        int next = _moveLimitRomBodies * 7 / 8;
        if (next >= floorRomBodies)
        {
            _moveLimitRomBodies = next;
        }
    }

    /// <summary>
    /// R5 $2AE6-2AF1 (level-progress tick): `ADDB $BE5C / CMPB $BE5D / BGE / LDB $BE5D`
    /// — the limit drops by the pass's step (4, then 2, alternating) and is CLAMPED
    /// to the floor. No re-roll: the in-flight countdown runs out, future re-rolls
    /// use the new limit. The floor descends to 1 = the arcade player's speed
    /// (player deltas are ±1 arcade px/frame at $3031; a grunt at limit 1 steps
    /// 4 arcade px every body = 1 arcade px/frame).
    /// </summary>
    public void WaveSpeedTick(int floorRomBodies, int stepRomBodies = 4)
    {
        _moveLimitRomBodies = Math.Max(floorRomBodies, _moveLimitRomBodies - stepRomBodies);
    }

    /// <summary>
    /// Laser kill: the ROM explodes the grunt IMMEDIATELY (RRP8.ASM `ROBKIL`:
    /// `JSR EXST` — "BLOW HIM UP!!!!") with NO flash and NO death animation.
    /// The explosion entity IS the entire visual.
    ///
    /// The flash in the ROM (`ROBKON` → `DMAON`, "ON GRUNT...SEE WHAT YOU
    /// HIT") belongs to the PLAYER-CONTACT case — the player dies touching a
    /// live grunt — not a laser kill. That path is NOT modelled yet: the port
    /// used to blink here for every kill, which is why grunts flashed when shot.
    /// </summary>
    public void Kill()
    {
        // Straight to Dead — no Dying phase, so nothing can blink.
        LifeState = EntityLifeState.Dead;
    }

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

        _bodyFifths += 5;
        if (_bodyFifths < BodyFifths)
        {
            return;
        }

        // One body pass: R5 $39CD. The non-step path ($39E0) goes straight
        // to the next grunt; only the step branch ($39E6 → DRAW_GRUNT $3A2B)
        // advances the walk frame — a paused grunt freezes mid-pose.
        _bodyFifths -= BodyFifths;

        if (--_moveCountdownBodies > 0)
        {
            return;
        }

        _moveCountdownBodies = _random.Next(1, _moveLimitRomBodies + 1);
        _walkFrame = _walkFrame % 4 + 1; // DRAW_GRUNT: one frame per step

        // R5 $39EF-3A29: per-axis step of 4 arcade px (8 screen px) toward
        // the player, dead zone 2 arcade px (4 screen px), axes independent.
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;
        int dx = Math.Sign(player.X - _position.X) * StepScreenPixels;
        int dy = Math.Sign(player.Y - _position.Y) * StepScreenPixels;
        _position = new IntVector2(
            Math.Abs(player.X - _position.X) > DeadZoneScreenPixels ? _position.X + dx : _position.X,
            Math.Abs(player.Y - _position.Y) > DeadZoneScreenPixels ? _position.Y + dy : _position.Y);
        _position = new IntVector2(
            Math.Clamp(_position.X, bounds.X, bounds.Right - CollisionSize.Width),
            Math.Clamp(_position.Y, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>
    /// ROM RWDP frame (1..4). The frame table maps RWDP1/2/3/4 → arts
    /// RWDD1/RWDD2/RWDD1/RWDD3, so only 3 of the 4 frames are unique.
    /// </summary>
    internal int WalkFrame => _walkFrame;

    /// <summary>RRP8 art table: RWDP frame → repo frame index ([A,B,A,C]).</summary>
    internal static int WalkArtIndex(int romFrame) => romFrame switch
    {
        2 => 1,
        4 => 2,
        _ => 0,
    };

    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.GruntFrames[WalkArtIndex(_walkFrame)];

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.GruntFrames[WalkArtIndex(_walkFrame)], Bounds, Color.White);
    }
}
