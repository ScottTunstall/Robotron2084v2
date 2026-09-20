using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A grunt — the basic robot. It always lumbers TOWARD the player, never away, but in staggered
/// bursts: it waits a random few beats, then steps 4 arcade px (8 screen px) on any axis it is
/// more than 2 arcade px (4 screen px) from the player. The axes are independent, so diagonal
/// steps happen and an already-close axis holds still.
///
/// A grunt dies to a player laser, or by walking onto an electrode (both die), with no death
/// animation — it explodes at once (see <see cref="Kill"/>). The field speeds the survivors up
/// every time one dies, which is the late-wave rush; while <see cref="PlayField.RobotsFrozen"/> is
/// set it holds still, but stays killable by lasers.
/// </summary>
/// <remarks>
/// <para>Unfamiliar terms below ("beat", "fifths", ROM file/label names, "notes §NN")
/// are explained once, in full, on <see cref="IEntity"/>.</para>
///
/// Its name in the arcade's own text is the Ground Roving UNit Terminator.
///
/// In the original source this is the `ROBOT` routine and its `ROB0`..`ROB11` sub-blocks
/// (RRP8.ASM, the file the arcade labels "ROBOTS AND POSTS"; RRP8 also holds the electrodes).
///
/// - The stagger is the arcade's own grunt beat, run every 4 vblanks, counting down a random timer
///   that is re-rolled (to a random 1..this wave's limit, never 0) each time it takes a step, so a
///   grunt waits a uniform random number of beats between steps (notes §29).
/// - The walk frame advances ONLY when a step happens: the draw routine sits in the step branch,
///   not on the common path, so a paused grunt freezes mid-pose with its legs stopped.
/// - The speed-up is `RMXSPD`, and its two rules are subtle enough to be worth the trip: see
///   <see cref="SpeedUp"/> and <see cref="WaveSpeedTick"/>.
/// </remarks>
public sealed class Grunt : IEntity, IExplodable
{
    /// <summary>The collision box: the grunt picture's own size, 10x13 arcade px, in port pixels,
    /// top-left anchored at <see cref="Position"/>.</summary>
    /// <remarks>The ROM picture's dimensions.</remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.GruntCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.GruntCollisionSize.Height));

    /// <summary>How far one step moves the grunt on each active axis, in port pixels: 4 arcade px = 8 screen px.</summary>
    /// <remarks>The ROM's step is 4 arcade px.</remarks>
    private const int StepScreenPixels = 8;

    /// <summary>The per-axis dead zone, in port pixels: 2 arcade px = 4 screen px.</summary>
    /// <remarks>The arcade's own dead zone.</remarks>
    private const int DeadZoneScreenPixels = 4;

    /// <summary>How many ROM frames one beat takes.</summary>
    /// <remarks>The arcade runs a grunt's beat every 4 vblanks.</remarks>
    private const int BeatIntervalRomTicks = 4;

    /// <summary>
    /// The beat period, converted to fifth-ticks (see <see cref="IEntity"/>): 4 ROM frames is 4.8 port ticks.
    /// </summary>
    /// <remarks>
    /// Notes §52: a ROM frame is 6/5 of a port tick, so `PortTicks(4)` truncates to 4,
    /// which made every grunt 17-20% fast (the same bug the quark/tank/spheroid/
    /// enforcer beats had fixed in §52).
    /// </remarks>
    private static int BeatPeriod => BeatIntervalRomTicks * 6;

    private readonly Random _random;
    private IntVector2 _position;
    private int _moveLimitBeats;
    private int _beatTimer;
    private int _moveCountdownBeats;
    private int _walkFrame = 1; // walk frame 1..4; a freshly spawned grunt starts on frame 1

    /// <summary>Creates a grunt, with its first stagger already rolled.</summary>
    /// <param name="position">Top-left of the grunt.</param>
    /// <param name="moveLimitBeats">This wave's re-roll limit: the upper bound of the random 1..N stagger.</param>
    /// <param name="speedBonus">Unused by this entity (kept for the field's uniform spawn shape).</param>
    /// <param name="random">The random source, or null to create one.</param>
    /// <remarks>This wave's stagger limit (ROM: ROBSPD, notes §29); the arcade rolls the spawn
    /// countdown as a random 1..that limit beats.</remarks>
    public Grunt(IntVector2 position, int moveLimitBeats = 15, int speedBonus = 0, Random? random = null)
    {
        _position = position;
        _moveLimitBeats = moveLimitBeats;
        _random = random ?? new Random();
        // The spawn countdown is a random 1..this wave's stagger limit, in beats.
        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
    }

    /// <summary>Top-left of the grunt.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The grunt picture's own 10x13 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it walks into an electrode; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The current stagger limit, in ROM beats: the re-roll upper bound. The field lowers it when grunts die.</summary>
    public int MoveDelayBeats => _moveLimitBeats;

    /// <summary>
    /// Called when another grunt dies: lowers this one's stagger limit to seven eighths of what it
    /// was, so the survivors step more often. The new value is used only if it is still at or above
    /// the floor; otherwise the limit is left exactly where it was. Any countdown already running is
    /// NOT re-rolled — the acceleration reaches this grunt on its next step.
    /// </summary>
    /// <param name="floorBeats">The wave's current floor, below which the limit must not go.</param>
    /// <remarks>
    /// On each grunt death, the arcade shrinks the shared stagger limit to seven eighths of its
    /// current value (truncated) **only if that value is still at or above the floor**; otherwise the
    /// limit is LEFT ALONE. That is not the same as clamping to the floor: an earlier pass's
    /// "max(floor, limit*7/8)" pushed the limit DOWN to the floor a kill early, which made the grunts
    /// speed up faster than the arcade's.
    /// <para>
    /// The arcade also does NOT touch any pending countdown here — the shared limit changes, and each
    /// grunt picks it up on its NEXT re-roll. Re-rolling every survivor's in-flight countdown (as the
    /// port used to) collapsed their pending delays to the new, smaller range on every kill, which made
    /// the wave speed up far too early.
    /// </para>
    /// </remarks>
    public void SpeedUp(int floorBeats)
    {
        int next = _moveLimitBeats * 7 / 8;
        if (next >= floorBeats)
        {
            _moveLimitBeats = next;
        }
    }

    /// <summary>
    /// Called on the level-progress tick: lowers this grunt's stagger limit by the wave's step and
    /// stops it at the floor. A countdown already running is left to finish, and only later re-rolls
    /// use the new limit.
    /// </summary>
    /// <param name="floorBeats">The wave's floor; the limit never goes below it.</param>
    /// <param name="stepBeats">How much to drop the limit by; the ROM alternates 4, then 2.</param>
    /// <remarks>
    /// The arcade drops the shared stagger limit by the pass's step and CLAMPS it to the floor. The
    /// floor descends all the way to 1, which matches the player's own speed: a grunt at limit 1 steps
    /// 4 arcade px every beat, the same 1 arcade px/frame the player moves at.
    /// </remarks>
    public void WaveSpeedTick(int floorBeats, int stepBeats = 4)
    {
        _moveLimitBeats = Math.Max(floorBeats, _moveLimitBeats - stepBeats);
    }

    /// <summary>
    /// Kills the grunt: it goes straight to Dead — no flash, no death animation — so the explosion
    /// the playfield draws is the entire visual.
    /// </summary>
    /// <remarks>
    /// The arcade simply explodes the grunt on a laser kill — its own comment reads "BLOW HIM UP!!!!"
    /// — with no flash and no death animation (ROM: RRP8.ASM's `ROBKIL`). The flash that DOES exist in
    /// the arcade belongs to the PLAYER-CONTACT case — the player dies touching a live grunt — not to
    /// a laser kill; that path is not modelled yet, and the port used to blink here for every kill,
    /// which is why grunts flashed when shot.
    /// </remarks>
    public void Kill()
    {
        // Straight to Dead — no Dying phase, so nothing can blink.
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    /// Advances the grunt when its 4-frame beat clock says so: count the stagger down and, when it
    /// expires, take one step toward the player, advance the walk frame and re-roll the stagger. Does
    /// nothing at all while the robots are held.
    /// </summary>
    /// <param name="gameTime">Unused — the beat is counted in ROM frames.</param>
    /// <param name="field">The playfield: the player to chase down, and the wall to stay inside.</param>
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

        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        // One beat pass. The non-step path goes straight to the next grunt with no
        // change; only the step branch below advances the walk frame — a paused grunt
        // freezes mid-pose.
        _beatTimer -= BeatPeriod;

        if (--_moveCountdownBeats > 0)
        {
            return;
        }

        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
        _walkFrame = _walkFrame % 4 + 1; // DRAW_GRUNT: one frame per step

        // A per-axis step of 4 arcade px (8 screen px) toward the player, with a
        // 2-arcade-px (4 screen px) dead zone; the axes are independent.
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

    /// <summary>The walk frame showing right now, 1..4 (test hook).</summary>
    /// <remarks>ROM RWDP frame.</remarks>
    internal int WalkFrame => _walkFrame;

    /// <summary>Maps a walk frame (1..4) to an index into <see cref="SpriteSet.GruntFrames"/>.</summary>
    /// <param name="romFrame">The walk frame, 1..4.</param>
    /// <returns>The index into <see cref="SpriteSet.GruntFrames"/>.</returns>
    /// <remarks>The 4 walk frames follow a "first, second, first, third" pattern rather than 4
    /// distinct pictures, so only 3 pictures are actually unique — the arcade's own art table maps
    /// walk frames 1/2/3/4 to pictures 1/2/1/3 the same way.</remarks>
    internal static int WalkArtIndex(int romFrame) => romFrame switch
    {
        2 => 1,
        4 => 2,
        _ => 0,
    };

    /// <summary>This grunt's current walk picture, for the appear and explosion effects.</summary>
    /// <param name="sprites">The shared sprite set, which holds the grunt frames.</param>
    /// <returns>The texture for the current walk frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.GruntFrames[WalkArtIndex(_walkFrame)];

    /// <summary>Draws the current walk picture in the art's own colours; a grunt never flashes.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the grunt frames.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.GruntFrames[WalkArtIndex(_walkFrame)], Bounds, Color.White);
    }
}
