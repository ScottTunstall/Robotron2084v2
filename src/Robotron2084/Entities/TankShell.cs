using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A shell fired by a tank. It is aimed once, then flies straight, bouncing off the walls until it fizzles out.</summary>
/// <seealso cref="Tank" />
/// <seealso cref="Level.PlayField" />
/// <remarks>
///     It moves steadily, and has a beat every few moves. The <see cref="PlayField" /> calls
///     <see cref="Update" /> on every tick, through <see cref="FieldEntities" /> and
///     <see cref="PlayField.UpdateEntity" />. The one time it does not is during the short freeze just after the player
///     is killed. <see cref="_frameTimer" /> gathers the ticks until it is time for the next move (see
///     <see cref="ArcadeClock" />), and <see cref="_framesToNextBeat" /> counts the moves to its next beat.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRTK4.ASM</c>: <c>TNKFIR</c> (aiming and speed), <c>SHELL</c> (each beat,
///             bouncing and fizzling) and <c>SHLKIL</c>
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c>: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>),
///             <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>)
///         </item>
///     </list>
///     Half the shots are
///     aimed at the player, give or take a miss. The other half are aimed at a wall so that they bounce back
///     across the field. A shell's speed grows with the gap to its target and with the wave's shell setting.
///     Sideways distances are in columns and up-and-down distances in rows, as in the ROM.
/// </remarks>
public sealed class TankShell : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>How tall the shell is, in port pixels.</summary>
    private static readonly int BoxHeight =
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.TankShellCollisionSize.Height);

    /// <summary>How wide the shell is, in port pixels.</summary>
    private static readonly int BoxWidth =
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.TankShellCollisionSize.Width);

    /// <summary>One column, in port pixels.</summary>
    private static readonly int ColumnPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>One row, in port pixels.</summary>
    private static readonly int RowPixels = ScreenSize.ToPortPixelsFromArcadePixels(1);

    private readonly Rectangle _playfieldBounds;
    private readonly SpriteSet _sprites;

    /// <summary>Beats left before the shell fizzles out.</summary>
    private int _beatsRemaining;

    /// <summary>
    ///     Counts up, a tick at a time, to the shell's next move. The shell moves on its own clock (see
    ///     <see cref="ArcadeClock" />).
    /// </summary>
    private int _frameTimer;

    /// <summary>How many more moves the shell makes before its next beat.</summary>
    private int _framesToNextBeat = TankShellTuning.BeatIntervalRomFrames;

    private IntVector2 _position;

    /// <summary>
    ///     The fraction of a pixel left over from the last move, in 256ths of a pixel. It is added to the next move. A
    ///     shell held against a wall can stop for good if it goes less than a pixel on each move, as it can in the arcade.
    /// </summary>
    private IntVector2 _remainderSubpixels;

    /// <summary>How far the shell goes on each move: sideways in 256ths of a column, and up or down in 256ths of a row.</summary>
    private IntVector2 _velocity;

    /// <summary>
    ///     Makes a shell. It starts a column to the right of the given position and is aimed once, at the player or at a
    ///     wall.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">The top-left corner of the tank that fired it, in port pixels.</param>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting. A bigger number is a faster shell.</param>
    /// <param name="playfieldBounds">The inside of the playfield wall, in port pixels.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick what the shell is aimed at, how far the aim is off,
    ///     and how long the shell lasts.
    /// </param>
    public TankShell(SpriteSet sprites, IntVector2 position, IntVector2 playerPosition, int shellSpeed,
        Rectangle playfieldBounds, Random random)
    {
        _sprites = sprites;
        _playfieldBounds = playfieldBounds;
        _position = position +
                    new IntVector2(ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns), 0);
        _velocity = random.Next(TankShellTuning.RollSides) >= TankShellTuning.ReboundRollAtOrAbove
            ? GetReboundVelocity(playerPosition, shellSpeed, random)
            : GetAimedVelocity(playerPosition, shellSpeed, random);
        _beatsRemaining = TankShellTuning.LifeBaseBeats + random.Next(TankShellTuning.LifeExtraBeatsMaxExclusive);
    }

    /// <summary>True when this tick's beat bounced the shell off a wall, so the bounce sound can be played.</summary>
    public bool BouncedThisUpdate { get; private set; }

    /// <summary>How far the shell goes sideways on each move, in 256ths of a column.</summary>
    internal int VelocityX => _velocity.X;

    /// <summary>How far the shell goes up or down on each move, in 256ths of a row.</summary>
    internal int VelocityY => _velocity.Y;

    /// <summary>The shell's one animation frame. It never flashes.</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.TankShellSprite;
    }

    /// <summary>The box the shell takes up on the screen. It is used to tell what the shell hits.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, BoxWidth, BoxHeight);
    }

    /// <summary>Alive until it fizzles out or is shot, then dead at once. A shell has no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the shell's top-left corner is.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the shell. It is drawn at its own size and never flashes.</summary>
    /// <param name="spriteBatch">What the shell is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsAlive())
            _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     Runs one tick of the shell. It moves steadily, separately from its beat. Every few moves it has a beat, when
    ///     it bounces off a wall or counts down its life.
    /// </summary>
    /// <param name="gameTime">Not used. The shell counts ticks.</param>
    /// <param name="field">Not used.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>SHELL</c>. The moving is done by the routine that moves
    ///             every moving object, in <c>RRS22.ASM</c> (<c>OPB80</c>)
    ///         </item>
    ///         <item>Disassembly: <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>)</item>
    ///     </list>
    /// </remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        BouncedThisUpdate = false;

        if (!this.IsAlive()) return;

        _frameTimer += ArcadeClock.UnitsPerPortTick;
        if (_frameTimer < ArcadeClock.UnitsPerRomFrame) return;

        _frameTimer -= ArcadeClock.UnitsPerRomFrame;
        MoveOneFrame();

        if (--_framesToNextBeat > 0) return;

        _framesToNextBeat = TankShellTuning.BeatIntervalRomFrames;
        RunBeat();
    }

    /// <summary>Takes the shell off the field at once, as when a laser hits it.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SHLKIL</c>.</item>
    ///         <item>Disassembly: <c>SHELL_COLLISION_HANDLER</c> (<c>$4FD5</c>).</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Works out how fast an aimed shot goes, either sideways or up-and-down. The bigger the gap to the target and
    ///     the bigger the wave's shell setting, the faster it goes.
    /// </summary>
    /// <param name="gap">
    ///     The gap from the shell to the target, in columns or rows. Negative means the target is to the left or
    ///     above.
    /// </param>
    /// <param name="shellSpeed">The wave's shell setting.</param>
    /// <returns>How far the shell goes on each move, in 256ths of a column or of a row.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>TNKF2</c> to <c>TNKF4</c>. A gap to the left or above
    ///             becomes one less than minus the speed, as in the ROM.
    ///         </item>
    ///         <item>Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4E8F</c> to <c>$4EA9</c>.</item>
    ///     </list>
    /// </remarks>
    internal static int ComputeAimedSpeed(int gap, int shellSpeed)
    {
        var distance = Math.Min(Math.Abs(gap), byte.MaxValue);
        var scaled = distance * shellSpeed / TankShellTuning.AimedSpeedDivisor;
        return gap >= 0
            ? scaled * TankShellTuning.AimedSpeedMultiplier
            : -(scaled + 1) * TankShellTuning.AimedSpeedMultiplier;
    }

    /// <summary>
    ///     Speeds up a shot that is aimed at a wall. Its speed is doubled again and again until it is fast enough, either
    ///     sideways or up-and-down.
    /// </summary>
    /// <param name="velocity">
    ///     The starting speed: sideways in 256ths of a column for each move, and up or down in 256ths of a
    ///     row.
    /// </param>
    /// <param name="shellSpeed">The wave's shell setting, which sets how fast is fast enough.</param>
    /// <returns>The speed after doubling.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TRBXY</c> to <c>TRBXYL</c>.</item>
    ///         <item>Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4F3F</c> to <c>$4F7E</c>.</item>
    ///     </list>
    /// </remarks>
    internal static IntVector2 GetBoostedVelocity(IntVector2 velocity, int shellSpeed)
    {
        var floor = shellSpeed * TankShellTuning.ReboundFloorFactor / TankShellTuning.AimedSpeedDivisor *
                    TankShellTuning.ReboundFloorMultiplier;
        var sidewaysLow = -floor - 1;
        var sidewaysHigh = floor;
        var upAndDownLow = TankShellTuning.ReboundUpAndDownFloorFactor * sidewaysLow;
        var upAndDownHigh = ~upAndDownLow;

        for (var doublings = 0; doublings < TankShellTuning.ReboundMaxDoublings; doublings++)
        {
            var fastEnoughSideways = velocity.X <= sidewaysLow || velocity.X >= sidewaysHigh;
            var fastEnoughUpAndDown = velocity.Y <= upAndDownLow || velocity.Y >= upAndDownHigh;
            if (fastEnoughSideways || fastEnoughUpAndDown) break;

            velocity = new IntVector2(velocity.X * 2, velocity.Y * 2);
        }

        return velocity;
    }

    /// <summary>Divides, rounding towards the lower number.</summary>
    /// <param name="value">The number to divide, which may be negative.</param>
    /// <param name="divisor">The number to divide by.</param>
    private static int DivideRoundingDown(int value, int divisor)
    {
        return (int)Math.Floor(value / (double)divisor);
    }

    /// <summary>Works out the starting speed of a shot aimed at the player, give or take a miss.</summary>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting.</param>
    /// <param name="random">Where the misses come from.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TNKF1</c> to <c>TNKF6</c>.</item>
    ///         <item>Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4E7F</c> to <c>$4ECE</c>.</item>
    ///     </list>
    /// </remarks>
    private IntVector2 GetAimedVelocity(IntVector2 playerPosition, int shellSpeed, Random random)
    {
        var playerColumn = DivideRoundingDown(playerPosition.X, ColumnPixels);
        var playerRow = DivideRoundingDown(playerPosition.Y, RowPixels);
        var missColumns = random.Next(2 * TankShellTuning.AimMissRange) - TankShellTuning.AimMissRange;
        var missRows = random.Next(2 * TankShellTuning.AimMissRange) - TankShellTuning.AimMissRange;

        var columnsFromLeftWall = playerColumn - DivideRoundingDown(_playfieldBounds.X, ColumnPixels);
        if (columnsFromLeftWall < TankShellTuning.NoSidewaysMissColumnsFromLeftWall) missColumns = 0;

        var gapColumns = playerColumn + missColumns - DivideRoundingDown(_position.X, ColumnPixels);
        var gapRows = playerRow + missRows - DivideRoundingDown(_position.Y, RowPixels);
        return new IntVector2(ComputeAimedSpeed(gapColumns, shellSpeed), ComputeAimedSpeed(gapRows, shellSpeed));
    }

    /// <summary>Works out the starting speed of a shot aimed at a wall, so that it bounces back across the field.</summary>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting.</param>
    /// <param name="random">Where the shot's direction and misses come from.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TNKFRB</c>, <c>TRBY</c> and <c>TRBXY</c>.</item>
    ///         <item>
    ///             Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4ED4</c> to <c>$4F7E</c>. Seven
    ///             times in eight the shot goes for the wall on the player's own side of the field; the other
    ///             time it goes for the far wall.
    ///         </item>
    ///     </list>
    /// </remarks>
    private IntVector2 GetReboundVelocity(IntVector2 playerPosition, int shellSpeed, Random random)
    {
        var shellColumn = DivideRoundingDown(_position.X, ColumnPixels);
        var shellRow = DivideRoundingDown(_position.Y, RowPixels);
        var playerColumn = DivideRoundingDown(playerPosition.X, ColumnPixels);
        var playerRow = DivideRoundingDown(playerPosition.Y, RowPixels);
        var leftColumn = DivideRoundingDown(_playfieldBounds.X, ColumnPixels);
        var rightColumn = DivideRoundingDown(_playfieldBounds.Right, ColumnPixels);
        var topRow = DivideRoundingDown(_playfieldBounds.Y, RowPixels);
        var bottomRow = DivideRoundingDown(_playfieldBounds.Bottom, RowPixels);

        var aimAtTopOrBottom = random.Next(2) == 1;
        var farSide = random.Next(TankShellTuning.ReboundSideRollSides) == TankShellTuning.ReboundFarSideRoll;
        int targetColumn;
        int targetRow;
        if (aimAtTopOrBottom)
        {
            var missColumns = random.Next(2 * TankShellTuning.ReboundWidthMissRange) -
                              TankShellTuning.ReboundWidthMissRange;
            targetColumn = DivideRoundingDown(missColumns + shellColumn + playerColumn, 2);
            var playerInBottomHalf = playerRow >= (topRow + bottomRow) / 2;
            targetRow = playerInBottomHalf != farSide ? bottomRow : topRow;
        }
        else
        {
            var missRows = random.Next(2 * TankShellTuning.ReboundHeightMissRange) -
                           TankShellTuning.ReboundHeightMissRange;
            targetRow = DivideRoundingDown(missRows + shellRow + playerRow, 2);
            var playerInRightHalf = playerColumn >= (leftColumn + rightColumn) / 2;
            targetColumn = playerInRightHalf != farSide ? rightColumn : leftColumn;
        }

        return GetBoostedVelocity(new IntVector2(targetColumn - shellColumn, targetRow - shellRow), shellSpeed);
    }

    /// <summary>Makes one move. A move that would take the shell out of the playfield is not made.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRS22.ASM</c> <c>OPB80</c>, the routine that moves every moving object.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    private void MoveOneFrame()
    {
        var subpixels = GetNextFrameSubpixels();
        var stepX = DivideRoundingDown(subpixels.X, ScreenSize.SubpixelsPerPixel);
        var stepY = DivideRoundingDown(subpixels.Y, ScreenSize.SubpixelsPerPixel);

        // The sideways move is made only if it keeps the shell inside the playfield. The same goes for the up-or-down move. A move that is not made changes nothing.
        var x = _position.X;
        var remainderX = _remainderSubpixels.X;
        if (FitsInsideX(_position.X + stepX))
        {
            x += stepX;
            remainderX = subpixels.X - stepX * ScreenSize.SubpixelsPerPixel;
        }

        var y = _position.Y;
        var remainderY = _remainderSubpixels.Y;
        if (FitsInsideY(_position.Y + stepY))
        {
            y += stepY;
            remainderY = subpixels.Y - stepY * ScreenSize.SubpixelsPerPixel;
        }

        _position = new IntVector2(x, y);
        _remainderSubpixels = new IntVector2(remainderX, remainderY);
    }

    /// <summary>Says whether the shell, put at this sideways position, would be inside the playfield.</summary>
    /// <param name="x">The sideways position of the shell's left side.</param>
    private bool FitsInsideX(int x)
    {
        return x >= _playfieldBounds.X && x + BoxWidth <= _playfieldBounds.Right;
    }

    /// <summary>Says whether the shell, put at this up-and-down position, would be inside the playfield.</summary>
    /// <param name="y">The up-and-down position of the shell's top.</param>
    private bool FitsInsideY(int y)
    {
        return y >= _playfieldBounds.Y && y + BoxHeight <= _playfieldBounds.Bottom;
    }

    /// <summary>
    ///     Works out how far the shell goes on its next move, in 256ths of a pixel, counting the fraction left over from
    ///     the last move.
    /// </summary>
    private IntVector2 GetNextFrameSubpixels()
    {
        return new IntVector2(
            _remainderSubpixels.X + _velocity.X * ColumnPixels,
            _remainderSubpixels.Y + _velocity.Y * RowPixels);
    }

    /// <summary>
    ///     Works out where the shell will be after its next move, from its speed and the fraction left over from the last
    ///     move.
    /// </summary>
    private IntVector2 GetNextPosition()
    {
        var subpixels = GetNextFrameSubpixels();
        return new IntVector2(
            _position.X + DivideRoundingDown(subpixels.X, ScreenSize.SubpixelsPerPixel),
            _position.Y + DivideRoundingDown(subpixels.Y, ScreenSize.SubpixelsPerPixel));
    }

    /// <summary>Runs one beat. The shell bounces if its next move would hit a wall. Otherwise it counts down its life.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SHELL</c>, <c>XVNEG</c> and <c>YVNEG</c>.</item>
    ///         <item>
    ///             Disassembly: <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>),
    ///             <c>TANK_SHELL_BOUNCE_HORIZONTAL</c> (<c>$4FC1</c>) and <c>TANK_SHELL_BOUNCE_VERTICAL</c>
    ///             (<c>$4FC7</c>). A beat that bounces the shell does not count down its life, and a sideways
    ///             bounce skips the up-and-down check.
    ///         </item>
    ///     </list>
    /// </remarks>
    private void RunBeat()
    {
        var next = GetNextPosition();
        if (!FitsInsideX(next.X))
        {
            _velocity = new IntVector2(~_velocity.X, _velocity.Y);
            BouncedThisUpdate = true;
            return;
        }

        if (!FitsInsideY(next.Y))
        {
            _velocity = new IntVector2(_velocity.X, ~_velocity.Y);
            BouncedThisUpdate = true;
            return;
        }

        if (--_beatsRemaining <= 0) LifeState = EntityLifeState.Dead;
    }
}
