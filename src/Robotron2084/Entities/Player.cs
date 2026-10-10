using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     The player is the hero the person plays as: it moves around, shoots at enemies, and can be killed and brought
///     back to try again.
/// </summary>
/// <seealso cref="PlayField" />
/// <seealso cref="PlayerLaser" />
/// <remarks>
///     It has no beat. The <see cref="PlayField" /> calls <see cref="Update" /> directly, in
///     <see cref="PlayField.Update" />,
///     on every tick. The one time it does not is during the short freeze just after the player is killed.
///     <see cref="_deathTimer" /> times the stages of its death (see <see cref="ArcadeClock" />),
///     <see cref="_animationFrameTicks" /> counts the ticks each animation frame is shown, and
///     <see cref="_invincibilityBlinkTicks" /> counts the ticks of its blinking.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRF.ASM</c>, routine <c>PLAYER</c> (called every interrupt; see also
///             <c>PDEATH</c>, and RRG23.ASM's <c>LTAB</c> table of where each shot starts)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$2FD0</c> (<c>MOVE_PLAYER</c>)</item>
///     </list>
/// </remarks>
public sealed class Player : IEntity, IAnimationFrameSource
{
    /// <summary>
    ///     How many ticks of walking each animation frame is shown for. <see cref="_animationFrameTicks" /> counts up to
    ///     this and then the next animation frame is shown.
    /// </summary>
    private const int FrameTicksPerAnimationFrame = 3;

    /// <summary>
    ///     How big the player is, in port pixels. It is the size of the player's sprite, and it is used to tell what the
    ///     player touches.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>PLAYER</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    ///     </list>
    ///     The arcade tells what the player touches from the player's sprite, not from a
    ///     square of a fixed size.
    /// </remarks>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.PlayerCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.PlayerCollisionSize.Height));

    /// <summary>The order each walk shows its three animation frames in: first, second, first, third.</summary>
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    private int
        _animationFrameTicks = 1; // How many ticks of walking the current walk animation frame has been shown for.

    private int _autoFireTicksRemaining;
    private int _deathFadeIndex;
    private int _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;
    private int _deathFlashSlot = PlayerTuning.PlayerDeathWhiteSlot;

    // Which stage of the death animation is playing: a white flash, a coloured flash, or the fade to black (ROM: RRX7.ASM).
    private DeathStage _deathStage = DeathStage.White;

    private int _deathTimer;
    private int _invincibilityBlinkTicks;
    private int _invincibilityTicksRemaining;
    private IntVector2 _position;
    private int _walkCycleStep;

    // The Player starts on the first animation frame for walking down.
    private WalkSequence _walkSequence = GetWalkSequence(Direction8.Down);
    private bool _wasFiring;

    /// <summary>Makes the player at <paramref name="startPosition" />, with <paramref name="lives" /> lives.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="startPosition">Where the player's top-left corner is.</param>
    /// <param name="lives">How many lives the player starts with. Each death takes one off.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick the colours of the death animation. If this is null,
    ///     the player makes its own.
    /// </param>
    public Player(SpriteSet sprites, IntVector2 startPosition, int lives, Random? random = null)
    {
        _sprites = sprites;
        _position = startPosition;
        _random = random ?? new Random();
        Lives = lives;
    }

    /// <summary>The way the player faces. It is the last way the player moved, not the way the player is shooting.</summary>
    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    /// <summary>
    ///     A testing aid that is not meant to stay. While it is on, <see cref="Kill" /> does nothing, so the player
    ///     cannot be killed.
    /// </summary>
    /// <remarks>
    ///     Each player has its own setting. The attract-mode demo leaves it off, so the demo's player can still
    ///     be killed as the arcade's can.
    /// </remarks>
    public bool InvincibleForTesting { get; set; } = PlayerTuning.PlayerInvincibleForTesting;

    /// <summary>True when the player fired a laser on this update.</summary>
    public bool FiredLaserThisUpdate { get; private set; }

    /// <summary>How many lives the player has left, counting the one being played. Each death takes one off.</summary>
    public int Lives { get; private set; }

    /// <summary>The walk animation frame the player is showing. A dying player is the same shape, filled with one colour.</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.PlayerAnimationFrames[GetWalkAnimationFrameIndex()];
    }

    /// <summary>The box the player takes up on the screen. It is used to tell what the player touches.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRF.ASM</c> <c>PLAYER</c>. The arcade checks against the sprite, not against a
    ///             square of a fixed size.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    ///     </list>
    /// </remarks>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>Alive, then Dying while the death animation plays, then Dead until the player comes back to life.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRX7.ASM</c> <c>PDTHV</c>, the flash and the fade.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    ///     </list>
    /// </remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the player's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Draws the walk animation frame. A dying player is drawn as the same shape, filled with one colour.</summary>
    /// <param name="spriteBatch">What the player is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsDead()) return;

        // A Player who cannot be hurt flickers. This is the part of the flicker when the Player is not drawn.
        if (IsInvincible() && _invincibilityBlinkTicks >= PlayerTuning.InvincibilityFlickerVisibleTicks) return;

        // A dying Player is drawn as a shape filled with one colour.
        if (this.IsDying())
        {
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), GetBounds(),
                _sprites.Blitter.GetSlotColour(GetDeathSolidSlot()));
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     Runs one tick. A dying player plays the death animation. A living player moves, fires and animates. Before the
    ///     game is live the player does nothing.
    /// </summary>
    /// <param name="gameTime">Not used. The player counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (this.IsDead()) return;

        if (this.IsDying())
        {
            // The Player does not move while the death animation plays.
            AdvanceDeath(field);
            return;
        }

        // The Player cannot move or fire until the start of the wave is over (ROM: RRG23.ASM PLAYRV).
        if (!field.IsLive()) return;

        AdvanceInvincibility();

        var input = field.Input.Poll();
        var move = input.MoveDirection;

        // The shoot stick only picks which way the Player fires. It does not change which way the Player faces.
        var aimDirection = Direction8Extensions.CreateFromDelta(input.ShootDirection);

        MoveFromInput(move, field);
        UpdateFiring(input, aimDirection, field);
        AdvanceWalkAnimation(move);
    }

    /// <summary>
    ///     Says whether the player cannot be hurt just now, because the countdown in
    ///     <see cref="_invincibilityTicksRemaining" /> is still running.
    /// </summary>
    public bool IsInvincible()
    {
        return _invincibilityTicksRemaining > 0;
    }

    /// <summary>The palette slot that the dying player is filled with.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>PDEATH</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    ///     </list>
    ///     It is the white slot, then a slot picked at random, then the slot that fades to black.
    /// </remarks>
    internal int GetDeathSolidSlot()
    {
        return _deathStage == DeathStage.Fade
            ? PlayerTuning.PlayerDeathFadeSlot
            : _deathFlashSlot;
    }

    /// <summary>
    ///     Which animation frame is showing, as a place in <see cref="SpriteSet.PlayerAnimationFrames" />, counting from
    ///     0.
    /// </summary>
    /// <remarks>The arcade numbers its animation frames from 1 to 12, so the arcade's number is one more than this.</remarks>
    internal int GetWalkAnimationFrameIndex()
    {
        return (int)_walkSequence * 3 + WalkCycle[_walkCycleStep];
    }

    /// <summary>Gives the player one more life. This is done when the score passes the number of points that earns one.</summary>
    public void AddLife()
    {
        Lives += 1;
    }

    /// <summary>Kills the player, when something deadly touches them. It does nothing if the player is already dying or dead.</summary>
    public void Kill()
    {
        if (InvincibleForTesting) return; // The Player cannot be killed while InvincibleForTesting is on.

        if (!this.IsAlive()) return;

        StartDeath();
    }

    /// <summary>Works out which walk to show for a direction. Walking diagonally shows the left walk or the right walk.</summary>
    /// <param name="direction">The way the player is facing.</param>
    /// <returns>The walk to show.</returns>
    /// <remarks>This is the arcade's own rule.</remarks>
    internal static WalkSequence GetWalkSequence(Direction8 direction)
    {
        return direction switch
        {
            Direction8.Left or Direction8.UpLeft or Direction8.DownLeft => WalkSequence.Left,
            Direction8.Right or Direction8.UpRight or Direction8.DownRight => WalkSequence.Right,
            Direction8.Down => WalkSequence.Down,
            _ => WalkSequence.Up
        };
    }

    /// <summary>Starts the death animation, even when <see cref="InvincibleForTesting" /> is on. Tests use this.</summary>
    internal void StartDeathForTesting()
    {
        if (this.IsAlive()) StartDeath();
    }

    /// <summary>Puts the player at <paramref name="position" />. Tests use this.</summary>
    /// <param name="position">Where to put the player's top-left corner.</param>
    internal void TeleportTo(IntVector2 position)
    {
        _position = position;
    }

    /// <summary>Works out where a laser fired in a direction starts, measured from the player's top-left corner.</summary>
    /// <param name="direction">The way the laser is fired.</param>
    /// <returns>How far to the right of the player's top-left corner, and how far below it, the laser starts, in port pixels.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c>, the table of where each shot starts.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER_LASER_RIGHT</c> (<c>$3279</c>) area.</item>
    ///     </list>
    /// </remarks>
    private static IntVector2 GetMuzzleOffset(Direction8 direction)
    {
        (int x, int y) offset = direction switch
        {
            Direction8.Up => (2, -1),
            Direction8.Down => (2, 4),
            Direction8.Left => (0, 4),
            Direction8.Right => (2, 4),
            Direction8.UpLeft => (0, 0),
            Direction8.DownLeft => (0, 8),
            Direction8.UpRight => (2, 0),
            _ => (2, 12) // Down and to the right.
        };
        return new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(offset.x),
            ScreenSize.ToPortPixelsFromArcadePixels(offset.y));
    }

    /// <summary>Makes a move either sideways or up-and-down, unless it would put the player in the wall.</summary>
    /// <param name="from">Where the player is before the move.</param>
    /// <param name="delta">The move: either sideways or up-and-down, never both.</param>
    /// <param name="wall">The wall, which the player must not go into.</param>
    /// <returns>Where the player is after the move, or <paramref name="from" /> when the wall is in the way.</returns>
    private static IntVector2 StepAxis(IntVector2 from, IntVector2 delta, PlayfieldWall wall)
    {
        var stepped = from + delta;
        Rectangle box = new(stepped.X, stepped.Y, CollisionSize.Width, CollisionSize.Height);
        return wall.Intersects(box) ? from : stepped;
    }

    /// <summary>Runs one tick of the death animation: the flashes, then the fade.</summary>
    /// <param name="field">The playfield, whose palette is used for the fade.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRX7.ASM</c> the death routine.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    ///     </list>
    ///     The fade takes over one palette slot. That slot's own colour changes are stopped while the fade runs,
    ///     and are started again when it is over.
    /// </remarks>
    private void AdvanceDeath(PlayField field)
    {
        _deathTimer += ArcadeClock.UnitsPerPortTick;

        var romFrames = _deathStage switch
        {
            DeathStage.White => PlayerTuning.PlayerDeathWhiteRomFrames,
            DeathStage.Colour => PlayerTuning.PlayerDeathColourRomFrames,
            _ => PlayerTuning.PlayerDeathFadeRomFrames
        };

        if (_deathTimer < ArcadeClock.ToClockUnits(romFrames)) return;

        _deathTimer -= ArcadeClock.ToClockUnits(romFrames);

        switch (_deathStage)
        {
            case DeathStage.White:
                // After the white flash, pick a random palette slot for the coloured flash.
                _deathFlashSlot =
                    PlayerTuning.PlayerDeathFlashSlots[_random.Next(PlayerTuning.PlayerDeathFlashSlots.Length)];
                _deathStage = DeathStage.Colour;
                break;

            case DeathStage.Colour:
                if (--_deathFlashIterationsRemaining <= 0)
                    BeginDeathFade(field);
                else
                    _deathStage = DeathStage.White;

                break;

            default:
                // Show the next colour of the fade. The last colour is black, and after it the Player is dead.
                _deathFadeIndex++;
                WriteDeathFade(field);
                if (_deathFadeIndex >= PlayerTuning.PlayerDeathFadeValues.Length - 1)
                {
                    field.Palette?.ResumeSlot(PlayerTuning.PlayerDeathFadeSlot);
                    LifeState = EntityLifeState.Dead;
                }

                break;
        }
    }

    /// <summary>Runs one tick of the countdown for the time the player cannot be hurt, and of its flicker.</summary>
    private void AdvanceInvincibility()
    {
        if (_invincibilityTicksRemaining > 0) _invincibilityTicksRemaining--;

        _invincibilityBlinkTicks =
            (_invincibilityBlinkTicks + 1) % (PlayerTuning.InvincibilityFlickerVisibleTicks +
                                              PlayerTuning.InvincibilityFlickerHiddenTicks);
    }

    /// <summary>Moves the walk animation on. When the move stick is not being pushed, the animation frame stays as it is.</summary>
    /// <param name="move">The way the move stick is pushed: left, right or neither, and up, down or neither.</param>
    private void AdvanceWalkAnimation(IntVector2 move)
    {
        if (move == IntVector2.Zero) return;

        var walkSequence = GetWalkSequence(FacingDirection);
        if (walkSequence != _walkSequence)
        {
            // The Player has turned, so the walk starts again from the first animation frame for the new direction.
            _walkSequence = walkSequence;
            _walkCycleStep = 0;
            _animationFrameTicks = 1;
        }
        else if (_animationFrameTicks >= FrameTicksPerAnimationFrame)
        {
            _animationFrameTicks = 1;
            _walkCycleStep = (_walkCycleStep + 1) % WalkCycle.Length;
        }
        else
        {
            _animationFrameTicks++;
        }
    }

    /// <summary>
    ///     Starts the fade. It stops the fade slot's own colour changes, and sets the slot to the first colour of the
    ///     fade.
    /// </summary>
    /// <param name="field">The playfield, whose palette is used for the fade.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRX7.ASM</c>, the death routine. The routines that change the palette's colours are
    ///             in RRS22.ASM.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    ///     </list>
    ///     The other colour changes carry on as normal. Only the fade slot's own changes are stopped.
    /// </remarks>
    private void BeginDeathFade(PlayField field)
    {
        _deathStage = DeathStage.Fade;
        _deathFadeIndex = 0;
        field.Palette?.SuspendSlot(PlayerTuning.PlayerDeathFadeSlot);
        WriteDeathFade(field);
    }

    /// <summary>
    ///     Moves the player the way the move stick is pushed, for one tick. The player turns to face that way, and cannot
    ///     go into the wall.
    /// </summary>
    /// <param name="move">The way the move stick is pushed: left, right or neither, and up, down or neither.</param>
    /// <param name="field">The playfield, whose wall the player cannot go into.</param>
    private void MoveFromInput(IntVector2 move, PlayField field)
    {
        if (move == IntVector2.Zero) return;

        FacingDirection = Direction8Extensions.CreateFromDelta(move)!.Value;

        // The sideways move is tried first, then the up-or-down move. A move that would put the Player in the wall is not made.
        var candidate = _position;
        var dx = move.X * PlayerTuning.PlayerSpeedX;
        if (dx != 0) candidate = StepAxis(candidate, new IntVector2(dx, 0), field.Wall);

        var dy = move.Y * PlayerTuning.PlayerSpeedY;
        if (dy != 0) candidate = StepAxis(candidate, new IntVector2(0, dy), field.Wall);

        _position = candidate;
    }

    /// <summary>Puts the death animation back at its first white flash.</summary>
    private void ResetDeathAnimation()
    {
        _deathStage = DeathStage.White;
        _deathTimer = 0;
        _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;
        _deathFlashSlot = PlayerTuning.PlayerDeathWhiteSlot;
        _deathFadeIndex = 0;
    }

    /// <summary>
    ///     Starts the player dying and takes off a life. <see cref="Kill" /> and <see cref="StartDeathForTesting" /> both
    ///     use it.
    /// </summary>
    private void StartDeath()
    {
        LifeState = EntityLifeState.Dying;
        ResetDeathAnimation();
        Lives -= 1;
    }

    /// <summary>
    ///     Fires a laser when the fire control is used. A new press fires at once. Holding it down keeps firing, with a
    ///     set wait between shots.
    /// </summary>
    /// <param name="input">The controls as they are on this tick.</param>
    /// <param name="aimDirection">The way the shoot stick is pushed, or null when it is not pushed.</param>
    /// <param name="field">The playfield, which holds the lasers.</param>
    /// <remarks>
    ///     A held control fires again every <see cref="PlayerTuning.PlayerAutoFireTicks" /> ticks. No laser is fired
    ///     when the player already has as many lasers in flight as are allowed.
    /// </remarks>
    private void UpdateFiring(PlayerInputState input, Direction8? aimDirection, PlayField field)
    {
        var isFireHeld = input.FireHeld;
        if (isFireHeld && (!_wasFiring || --_autoFireTicksRemaining <= 0))
        {
            var fireDirection = aimDirection ?? FacingDirection;
            var muzzle = _position + GetMuzzleOffset(fireDirection);
            FiredLaserThisUpdate = field.TryFirePlayerLaser(muzzle, fireDirection);
            _autoFireTicksRemaining = PlayerTuning.PlayerAutoFireTicks;
        }
        else
        {
            FiredLaserThisUpdate = false;
        }

        _wasFiring = isFireHeld;
    }

    /// <summary>Sets the fade slot to the next colour of the fade.</summary>
    /// <param name="field">The playfield, whose palette holds the fade slot.</param>
    private void WriteDeathFade(PlayField field)
    {
        field.Palette?.SetSlot(PlayerTuning.PlayerDeathFadeSlot, PlayerTuning.PlayerDeathFadeValues[_deathFadeIndex]);
    }

    /// <summary>The stages of the death animation: a white flash, a coloured flash, then the fade to black.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>PDEATH</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>KILL_PLAYER</c>.</item>
    ///     </list>
    ///     The white flash and the coloured flash are repeated several times before the fade. The fade is made by changing
    ///     the colour of one palette slot (<see cref="PlayerTuning.PlayerDeathFadeSlot" />).
    /// </remarks>
    private enum DeathStage
    {
        /// <summary>The white flash.</summary>
        White,

        /// <summary>A flash in a colour picked at random.</summary>
        Colour,

        /// <summary>The fade to black.</summary>
        Fade
    }
}
