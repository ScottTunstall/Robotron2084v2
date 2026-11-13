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
///     It has no <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_deathArcadeFrameTimer" /> times the stages of its death,
///     <see cref="_walkAnimationCountUp" /> counts the arcade frames each animation frame is shown, and
///     <see cref="_invincibilityFlickerTicks" /> counts the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> of its blinking.
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
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">arcade frames</see> of walking each
    ///     animation frame is shown for.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRF.ASM</c> <c>PLAYER</c>. The routine runs once each arcade frame, and counts up to
    ///             this while the player moves; the next animation frame shows when the count starts again from 0. A
    ///             player standing still does not count.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c>, <c>$300B</c> to <c>$302E</c>.</item>
    ///     </list>
    /// </remarks>
    internal const int WalkAnimationIntervalArcadeFrames = 2;

    /// <summary>
    ///     How big the player is, in port pixels. It is the size of the player's sprite.
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

    private readonly ArcadeFrameTimer _deathArcadeFrameTimer = new();
    private readonly Random _random;
    private readonly TextureSet _textureSet;

    private int _autoFireTicksRemaining;

    private int _deathFadeIndex;

    private int _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;

    private int _deathFlashPaletteIndex = PlayerTuning.PlayerDeathWhitePaletteIndex;

    // Which stage of the death animation is playing: a white flash, a coloured flash, or the fade to black (ROM: RRX7.ASM).
    private PlayerDeathStage _deathStage = PlayerDeathStage.White;

    private int _invincibilityFlickerTicks;

    private int _invincibilityTicksRemaining;

    private IntVector2 _position;

    /// <summary>
    ///     How many arcade frames of walking the animation frame showing has been shown for, counting from 0 and starting
    ///     again at <see cref="WalkAnimationIntervalArcadeFrames" />. The first animation frame has already been shown for one.
    /// </summary>
    private int _walkAnimationCountUp = 1;

    /// <summary>Times the arcade frames the walk animation counts.</summary>
    private readonly ArcadeFrameTimer _walkAnimationArcadeFrameTimer = new();

    private int _walkCycleIndex;

    // The Player starts on the first animation frame for walking down.
    private WalkSequence _walkSequence = GetWalkSequence(Direction8.Down);

    private bool _wasFiring;

    /// <summary>Makes the player at <paramref name="startPosition" />, with <paramref name="lives" /> lives.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="startPosition">Where the player's top-left corner is.</param>
    /// <param name="lives">How many lives the player starts with. Each death takes one off.</param>
    /// <param name="random">Where its random numbers come from. They pick the colours of the death animation.</param>
    public Player(TextureSet textureSet, IntVector2 startPosition, int lives, Random random)
    {
        _textureSet = textureSet;
        _position = startPosition;
        _random = random;
        Lives = lives;
    }

    /// <summary>The way the player faces. It is the last way the player moved, not the way the player is shooting.</summary>
    public Direction8 FacingDirection { get; private set; } = Direction8.Up;

    /// <summary>True when the player fired a laser on this update.</summary>
    public bool FiredLaserThisUpdate { get; private set; }

    /// <summary>
    ///     A testing aid that is not meant to stay. While it is on, <see cref="Kill" /> does nothing, so the player
    ///     cannot be killed.
    /// </summary>
    /// <remarks>
    ///     Each player has its own setting. The attract-mode demo leaves it off, so the demo's player can still
    ///     be killed as the arcade's can.
    /// </remarks>
    public bool InvincibleForTesting { get; set; } = PlayerTuning.PlayerInvincibleForTesting;

    /// <summary>Alive, then Dying while the death animation plays, then Dead until the player comes back to life.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRX7.ASM</c> <c>PDTHV</c>, the flash and the fade.</item>
    ///         <item>Disassembly: <c>KILL_PLAYER</c> (<c>$30EF</c>).</item>
    ///     </list>
    /// </remarks>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>How many lives the player has left, counting the one being played. Each death takes one off.</summary>
    public int Lives { get; private set; }

    /// <summary>Where the player's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c> (<c>$2FD0</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Gives the player one more life. This is done when the score passes the number of points that earns one.</summary>
    public void AddLife()
    {
        Lives += 1;
    }

    /// <summary>Draws the walk animation frame. A dying player is drawn as the same shape, filled with one colour.</summary>
    /// <param name="spriteBatch">What the player is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsDead()) return;

        // A Player who cannot be hurt flickers. This is the part of the flicker when the Player is not drawn.
        if (IsInvincible() && _invincibilityFlickerTicks >= PlayerTuning.InvincibilityFlickerVisibleTicks) return;

        // A dying Player is drawn as a shape filled with one colour.
        if (this.IsDying())
        {
            _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(),
                _textureSet.BlitterDraw.GetPaletteIndexColour(GetDeathSolidPaletteIndex()));
            return;
        }

        _textureSet.BlitterDraw.DrawSprite(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(), Color.White);
    }

    /// <summary>The box the player takes up on the screen.</summary>
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

    /// <summary>The walk animation frame the player is showing. A dying player is the same shape, filled with one colour.</summary>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _textureSet.PlayerAnimationFrameTextures[GetWalkAnimationFrameIndex()];
    }

    /// <summary>
    ///     Says whether the player cannot be hurt just now, because the countdown in
    ///     <see cref="_invincibilityTicksRemaining" /> is still running.
    /// </summary>
    public bool IsInvincible()
    {
        return _invincibilityTicksRemaining > 0;
    }

    /// <summary>Kills the player, when something deadly touches them. It does nothing if the player is already dying or dead.</summary>
    public void Kill()
    {
        if (InvincibleForTesting) return; // The Player cannot be killed while InvincibleForTesting is on.

        if (!this.IsAlive()) return;

        StartDeath();
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. A dying player plays the death
    ///     animation, and a living player moves, fires and animates.
    /// </summary>
    /// <param name="gameTime">Not used. The player counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    /// <remarks>
    ///     Before the game is live the player does nothing.
    /// </remarks>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (this.IsDead()) return;

        if (this.IsDying())
        {
            // The Player does not move while the death animation plays.
            AdvanceDeath(playField);
            return;
        }

        // The Player cannot move or fire until the start of the wave is over (ROM: RRG23.ASM PLAYRV).
        if (!playField.IsLive()) return;

        AdvanceInvincibility();

        var playerInputState = playField.PlayerInputSource.Poll();
        var move = playerInputState.MoveDirection;

        // The shoot stick only picks which way the Player fires. It does not change which way the Player faces.
        var aimDirection = Direction8Extensions.CreateFromDelta(playerInputState.ShootDirection);

        MoveFromInput(move, playField);
        UpdateFiring(playerInputState, aimDirection, playField);
        AdvanceWalkAnimation(move);
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

    /// <summary>The palette index that the dying player is filled with.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>PDEATH</c>.</item>
    ///         <item>Disassembly: <c>KILL_PLAYER</c> (<c>$30EF</c>).</item>
    ///     </list>
    ///     It is the white palette index, then a palette index picked at random, then the palette index that fades to black.
    /// </remarks>
    internal int GetDeathSolidPaletteIndex()
    {
        var isFading = _deathStage == PlayerDeathStage.Fade;
        if (isFading) return PlayerTuning.PlayerDeathFadePaletteIndex;

        return _deathFlashPaletteIndex;
    }

    /// <summary>
    ///     Which animation frame is showing, as a place in <see cref="TextureSet.PlayerAnimationFrameTextures" />, counting from
    ///     0.
    /// </summary>
    /// <remarks>The arcade numbers its animation frames from 1 to 12, so the arcade's number is one more than this.</remarks>
    internal int GetWalkAnimationFrameIndex()
    {
        return GetWalkAnimationFrameIndex(_walkSequence, _walkCycleIndex);
    }

    /// <summary>
    ///     Works out which animation frame a walk shows at a step of its cycle, as a place in
    ///     <see cref="TextureSet.PlayerAnimationFrameTextures" />.
    /// </summary>
    /// <param name="walkSequence">The walk.</param>
    /// <param name="walkCycleIndex">The step of the walk's cycle, from 0 to <see cref="WalkCycleLength" /> less 1.</param>
    /// <returns>The place of the animation frame, counting from 0.</returns>
    internal static int GetWalkAnimationFrameIndex(WalkSequence walkSequence, int walkCycleIndex)
    {
        return (int)walkSequence * 3 + WalkCycle[walkCycleIndex];
    }

    /// <summary>How many steps a walk's cycle has before it starts again.</summary>
    internal static int WalkCycleLength => WalkCycle.Length;

    /// <summary>Starts the death animation, even when <see cref="InvincibleForTesting" /> is on.</summary>
    internal void StartDeathForTesting()
    {
        if (this.IsAlive()) StartDeath();
    }

    /// <summary>Puts the player at <paramref name="position" />.</summary>
    /// <param name="position">Where to put the player's top-left corner.</param>
    internal void MoveTo(IntVector2 position)
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
            Direction8.Up => (2, DirectionSigns.Up),
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
    /// <param name="playfieldWall">The wall, which the player must not go into.</param>
    /// <returns>Where the player is after the move, or <paramref name="from" /> when the wall is in the way.</returns>
    private static IntVector2 GetPositionAfterStep(IntVector2 from, IntVector2 delta, PlayfieldWall playfieldWall)
    {
        var stepped = from + delta;
        Rectangle steppedBounds = new(stepped.X, stepped.Y, CollisionSize.Width, CollisionSize.Height);
        return playfieldWall.Intersects(steppedBounds) ? from : stepped;
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> of the death animation: the flashes, then the fade.</summary>
    /// <param name="playField">The playfield, whose palette is used for the fade.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRX7.ASM</c> the death routine.</item>
    ///         <item>Disassembly: <c>KILL_PLAYER</c> (<c>$30EF</c>).</item>
    ///     </list>
    ///     The fade takes over one palette index. That palette index's own colour changes are stopped while the fade runs,
    ///     and are started again when it is over.
    /// </remarks>
    private void AdvanceDeath(PlayField playField)
    {
        var intervalArcadeFrames = _deathStage switch
        {
            PlayerDeathStage.White => PlayerTuning.PlayerDeathWhiteArcadeFrames,
            PlayerDeathStage.Colour => PlayerTuning.PlayerDeathColourArcadeFrames,
            _ => PlayerTuning.PlayerDeathFadeArcadeFrames
        };

        if (!_deathArcadeFrameTimer.Tick(intervalArcadeFrames)) return;

        switch (_deathStage)
        {
            case PlayerDeathStage.White:
                // After the white flash, pick a random palette index for the coloured flash.
                _deathFlashPaletteIndex =
                    PlayerTuning.PlayerDeathFlashPaletteIndices[_random.Next(PlayerTuning.PlayerDeathFlashPaletteIndices.Length)];
                _deathStage = PlayerDeathStage.Colour;
                break;

            case PlayerDeathStage.Colour:
                if (--_deathFlashIterationsRemaining <= 0)
                    BeginDeathFade(playField);
                else
                    _deathStage = PlayerDeathStage.White;

                break;

            default:
                // Show the next colour of the fade. The last colour is black, and after it the Player is dead.
                _deathFadeIndex++;
                WriteDeathFade(playField);
                if (_deathFadeIndex >= PlayerTuning.PlayerDeathFadeValues.Length - 1)
                {
                    playField.GamePalette?.ResumePaletteIndex(PlayerTuning.PlayerDeathFadePaletteIndex);
                    LifeState = EntityLifeState.Dead;
                }

                break;
        }
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> of the countdown for the time the player cannot be hurt, and of its flicker.</summary>
    private void AdvanceInvincibility()
    {
        if (_invincibilityTicksRemaining > 0) _invincibilityTicksRemaining--;

        _invincibilityFlickerTicks =
            (_invincibilityFlickerTicks + 1) % (PlayerTuning.InvincibilityFlickerVisibleTicks +
                                              PlayerTuning.InvincibilityFlickerHiddenTicks);
    }

    /// <summary>
    ///     Moves the walk animation on, once each arcade frame, as the arcade does. When the move stick is not being
    ///     pushed, the animation frame stays as it is and the count stops.
    /// </summary>
    /// <param name="move">The way the move stick is pushed: left, right or neither, and up, down or neither.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>PLAYER</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c>, <c>$2FFC</c> to <c>$302E</c>.</item>
    ///     </list>
    /// </remarks>
    private void AdvanceWalkAnimation(IntVector2 move)
    {
        if (move == IntVector2.Zero) return;
        if (!_walkAnimationArcadeFrameTimer.Tick(ArcadeClock.MoveIntervalArcadeFrames)) return;

        var walkSequence = GetWalkSequence(FacingDirection);
        if (walkSequence != _walkSequence)
        {
            // The Player has turned, so the walk starts again from its first animation frame, shown at once.
            _walkSequence = walkSequence;
            _walkCycleIndex = 0;
            _walkAnimationCountUp = 1;
            return;
        }

        if (_walkAnimationCountUp == 0) _walkCycleIndex = (_walkCycleIndex + 1) % WalkCycle.Length;

        _walkAnimationCountUp = (_walkAnimationCountUp + 1) % WalkAnimationIntervalArcadeFrames;
    }

    /// <summary>
    ///     Starts the fade. It stops the fade palette index's own colour changes, and sets the palette index to the first colour of the
    ///     fade.
    /// </summary>
    /// <param name="playField">The playfield, whose palette is used for the fade.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRX7.ASM</c>, the death routine. The routines that change the palette's colours are
    ///             in RRS22.ASM.
    ///         </item>
    ///         <item>Disassembly: <c>KILL_PLAYER</c> (<c>$30EF</c>).</item>
    ///     </list>
    ///     The other colour changes carry on as normal. Only the fade palette index's own changes are stopped.
    /// </remarks>
    private void BeginDeathFade(PlayField playField)
    {
        _deathStage = PlayerDeathStage.Fade;
        _deathFadeIndex = 0;
        playField.GamePalette?.SuspendPaletteIndex(PlayerTuning.PlayerDeathFadePaletteIndex);
        WriteDeathFade(playField);
    }

    /// <summary>
    ///     Moves the player the way the move stick is pushed, for one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. The player turns to face that way, and cannot
    ///     go into the wall.
    /// </summary>
    /// <param name="move">The way the move stick is pushed: left, right or neither, and up, down or neither.</param>
    /// <param name="playField">The playfield, whose wall the player cannot go into.</param>
    private void MoveFromInput(IntVector2 move, PlayField playField)
    {
        if (move == IntVector2.Zero) return;

        FacingDirection = Direction8Extensions.CreateFromDelta(move)!.Value;

        // The sideways move is tried first, then the up-or-down move. A move that would put the Player in the wall is not made.
        var candidate = _position;
        var dx = move.X * PlayerTuning.PlayerSpeedX;
        if (dx != 0) candidate = GetPositionAfterStep(candidate, new IntVector2(dx, 0), playField.PlayfieldWall);

        var dy = move.Y * PlayerTuning.PlayerSpeedY;
        if (dy != 0) candidate = GetPositionAfterStep(candidate, new IntVector2(0, dy), playField.PlayfieldWall);

        _position = candidate;
    }

    /// <summary>Puts the death animation back at its first white flash.</summary>
    private void ResetDeathAnimation()
    {
        _deathStage = PlayerDeathStage.White;
        _deathArcadeFrameTimer.Restart();
        _deathFlashIterationsRemaining = PlayerTuning.PlayerDeathFlashIterations;
        _deathFlashPaletteIndex = PlayerTuning.PlayerDeathWhitePaletteIndex;
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

    /// <summary>Fires a laser when the fire control is used. A new press fires at once.</summary>
    /// <param name="playerInputState">The controls as they are on this <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</param>
    /// <param name="aimDirection">The way the shoot stick is pushed, or null when it is not pushed.</param>
    /// <param name="playField">The playfield, which holds the lasers.</param>
    /// <remarks>
    ///     Holding it down keeps firing, with a set wait between shots.
    ///     A held control fires again every <see cref="PlayerTuning.PlayerAutoFireTicks" /> <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>. No laser is fired
    ///     when the player already has as many lasers in flight as are allowed.
    /// </remarks>
    private void UpdateFiring(PlayerInputState playerInputState, Direction8? aimDirection, PlayField playField)
    {
        var isFireHeld = playerInputState.FireHeld;
        if (isFireHeld && (!_wasFiring || --_autoFireTicksRemaining <= 0))
        {
            var fireDirection = aimDirection ?? FacingDirection;
            var muzzle = _position + GetMuzzleOffset(fireDirection);
            FiredLaserThisUpdate = playField.TryFirePlayerLaser(muzzle, fireDirection);
            _autoFireTicksRemaining = PlayerTuning.PlayerAutoFireTicks;
        }
        else
        {
            FiredLaserThisUpdate = false;
        }

        _wasFiring = isFireHeld;
    }

    /// <summary>Sets the fade palette index to the next colour of the fade.</summary>
    /// <param name="playField">The playfield, whose palette holds the fade palette index.</param>
    private void WriteDeathFade(PlayField playField)
    {
        playField.GamePalette?.SetPaletteIndexValue(PlayerTuning.PlayerDeathFadePaletteIndex, PlayerTuning.PlayerDeathFadeValues[_deathFadeIndex]);
    }
}
