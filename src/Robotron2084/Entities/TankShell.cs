using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A shell fired by a tank. It is aimed once, then flies straight, bouncing off the walls until it fizzles out. It moves every ROM frame, and has a beat every few ROM frames. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_frameTimer"/> gathers the ticks until it is time for the next ROM frame (see <see cref="ArcadeClock"/>), and <see cref="_framesToNextBeat"/> counts the ROM frames to its next beat.</summary>
/// <seealso cref="Tank"/>
/// <seealso cref="Level.PlayField"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRTK4.ASM</c>: <c>TNKFIR</c> (aiming and speed), <c>SHELL</c> (each beat, bouncing and fizzling) and <c>SHLKIL</c></item>
/// <item>Disassembly: <c>asm/robomame.asm</c>: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>), <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>)</item>
/// </list>
/// Half the shots are aimed at the player, give or take a miss. The other half are aimed at a wall so that they bounce back across the field.
/// A shell's speed grows with the gap to its target and with the wave's shell setting.
/// Sideways distances are in columns and up-and-down distances in rows, as in the ROM.
/// </remarks>
public sealed class TankShell : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int BoxHeight = ScreenSize.ToPortPixels(CollisionSizes.TankShellCollisionSize.Height);

    /// <summary>The shell sprite's own 8x7 arcade px box, in port pixels.</summary>
    private static readonly int BoxWidth = ScreenSize.ToPortPixels(CollisionSizes.TankShellCollisionSize.Width);

    /// <summary>One column, in port pixels.</summary>
    private static readonly int ColumnPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>One row, in port pixels.</summary>
    private static readonly int RowPixels = ScreenSize.ToPortPixels(1);

    private readonly Rectangle _playfieldBounds;
    private readonly SpriteSet _sprites;

    /// <summary>ROM frames left before the shell's next beat.</summary>
    private int _framesToNextBeat = TankShellTuning.BeatIntervalRomFrames;

    /// <summary>Builds up, a tick at a time, until it is time for the next ROM frame.</summary>
    private int _frameTimer;

    private IntVector2 _position;

    /// <summary>The part of a port pixel the shell has moved but not yet shown, in 256ths, carried from frame to frame. A shell held against a wall can stop for good if its speed is under a pixel a ROM frame, as in the ROM.</summary>
    private IntVector2 _remainderSubpixels;

    /// <summary>Beats left before the shell fizzles out.</summary>
    private int _beatsRemaining;

    /// <summary>How fast the shell goes sideways, in 256ths of a column a ROM frame, and up and down, in 256ths of a row a ROM frame.</summary>
    private IntVector2 _velocity;

    /// <summary>Fires a shell. It starts a column to the right of the given position and is aimed once, at the player or at a wall.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">The top-left corner of the tank that fired it, in port pixels.</param>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting. A bigger number is a faster shell.</param>
    /// <param name="playfieldBounds">The edges of the playfield, in port pixels.</param>
    /// <param name="random">Where the shot type, the aim misses and the shell's life come from.</param>
    public TankShell(SpriteSet sprites, IntVector2 position, IntVector2 playerPosition, int shellSpeed, Rectangle playfieldBounds, Random random)
    {
        _sprites = sprites;
        _playfieldBounds = playfieldBounds;
        _position = position + new IntVector2(ScreenSize.ToPortPixelsFromColumns(TankShellTuning.StartOffsetColumns), 0);
        _velocity = random.Next(TankShellTuning.RollSides) >= TankShellTuning.ReboundRollAtOrAbove
            ? GetReboundVelocity(playerPosition, shellSpeed, random)
            : GetAimedVelocity(playerPosition, shellSpeed, random);
        _beatsRemaining = TankShellTuning.LifeBaseBeats + random.Next(TankShellTuning.LifeExtraBeatsMaxExclusive);
    }

    /// <summary>True when this tick's beat bounced the shell off a wall, so the bounce sound can be played.</summary>
    public bool BouncedThisUpdate { get; private set; }

    /// <summary>The box around the shell, from its top-left corner.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, BoxWidth, BoxHeight);

    /// <summary>The shell's one animation frame. It never flashes.</summary>
    public Texture2D GetCurrentAnimationFrame() => _sprites.TankShell;

    /// <summary>Alive until it fizzles out or is shot, then dead at once. A shell has no dying animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The top-left corner of the shell.</summary>
    public IntVector2 Position => _position;

    /// <summary>How fast the shell goes sideways, in 256ths of a column a ROM frame.</summary>
    internal int VelocityX => _velocity.X;

    /// <summary>How fast the shell goes up and down, in 256ths of a row a ROM frame.</summary>
    internal int VelocityY => _velocity.Y;

    /// <summary>Works out the speed of an aimed shot on one axis: the gap to the target, scaled by the wave's shell setting.</summary>
    /// <param name="gap">The gap from the shell to the target, in columns or rows. Negative means the target is to the left or above.</param>
    /// <param name="shellSpeed">The wave's shell setting.</param>
    /// <returns>The speed, in 256ths of a column or row a ROM frame.</returns>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKF2</c> to <c>TNKF4</c>. A gap to the left or above becomes one less than minus the speed, as in the ROM. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4E8F</c> to <c>$4EA9</c>.</remarks>
    internal static int ComputeAimedSpeed(int gap, int shellSpeed)
    {
        int distance = Math.Min(Math.Abs(gap), byte.MaxValue);
        int scaled = (distance * shellSpeed) / TankShellTuning.AimedSpeedDivisor;
        return gap >= 0
            ? scaled * TankShellTuning.AimedSpeedMultiplier
            : -(scaled + 1) * TankShellTuning.AimedSpeedMultiplier;
    }

    /// <summary>Speeds a rebound shot up, by doubling, until it is fast enough on either axis.</summary>
    /// <param name="velocity">The starting speed, in 256ths of a column and of a row a ROM frame.</param>
    /// <param name="shellSpeed">The wave's shell setting, which sets how fast is fast enough.</param>
    /// <returns>The speed after doubling.</returns>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TRBXY</c> to <c>TRBXYL</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4F3F</c> to <c>$4F7E</c>.</remarks>
    internal static IntVector2 GetBoostedVelocity(IntVector2 velocity, int shellSpeed)
    {
        int floor = (shellSpeed * TankShellTuning.ReboundFloorFactor / TankShellTuning.AimedSpeedDivisor) * TankShellTuning.ReboundFloorMultiplier;
        int sidewaysLow = -floor - 1;
        int sidewaysHigh = floor;
        int upAndDownLow = (TankShellTuning.ReboundUpAndDownFloorFactor * sidewaysLow);
        int upAndDownHigh = ~upAndDownLow;

        for (int doublings = 0; doublings < TankShellTuning.ReboundMaxDoublings; doublings++)
        {
            bool fastEnoughSideways = velocity.X <= sidewaysLow || velocity.X >= sidewaysHigh;
            bool fastEnoughUpAndDown = velocity.Y <= upAndDownLow || velocity.Y >= upAndDownHigh;
            if (fastEnoughSideways || fastEnoughUpAndDown)
            {
                break;
            }

            velocity = new IntVector2(velocity.X * 2, velocity.Y * 2);
        }

        return velocity;
    }

    /// <summary>Draws the shell. It is drawn at its own size and never flashes.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsAlive())
        {
            _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
        }
    }

    /// <summary>Takes the shell off the field at once, as when a laser hits it.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SHLKIL</c>. Disassembly: <c>SHELL_COLLISION_HANDLER</c> (<c>$4FD5</c>).</remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick of the shell. Each ROM frame it moves, and every second ROM frame it has a beat: bouncing off a wall or counting down its life.</summary>
    /// <param name="gameTime">Not used. The shell counts in ticks, not in seconds.</param>
    /// <param name="field">Not used.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRTK4.ASM</c> <c>SHELL</c>, with the movement done by the shared mover in <c>RRS22.ASM</c> (<c>OPB80</c>)</item>
    /// <item>Disassembly: <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>)</item>
    /// </list>
    /// </remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        BouncedThisUpdate = false;

        if (!this.IsAlive())
        {
            return;
        }

        _frameTimer += ArcadeClock.UnitsPerPortTick;
        if (_frameTimer < ArcadeClock.UnitsPerRomFrame)
        {
            return;
        }

        _frameTimer -= ArcadeClock.UnitsPerRomFrame;
        MoveOneFrame();

        if (--_framesToNextBeat > 0)
        {
            return;
        }

        _framesToNextBeat = TankShellTuning.BeatIntervalRomFrames;
        RunBeat();
    }

    /// <summary>Divides, rounding towards the lower number.</summary>
    /// <param name="value">The number to divide, which may be negative.</param>
    /// <param name="divisor">The number to divide by.</param>
    private static int DivideRoundingDown(int value, int divisor) =>
        (int)Math.Floor(value / (double)divisor);

    /// <summary>Works out the starting speed of a shot aimed at the player, give or take a miss.</summary>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting.</param>
    /// <param name="random">Where the misses come from.</param>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKF1</c> to <c>TNKF6</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4E7F</c> to <c>$4ECE</c>.</remarks>
    private IntVector2 GetAimedVelocity(IntVector2 playerPosition, int shellSpeed, Random random)
    {
        int playerColumn = DivideRoundingDown(playerPosition.X, ColumnPixels);
        int playerRow = DivideRoundingDown(playerPosition.Y, RowPixels);
        int missColumns = random.Next(2 * TankShellTuning.AimMissRange) - TankShellTuning.AimMissRange;
        int missRows = random.Next(2 * TankShellTuning.AimMissRange) - TankShellTuning.AimMissRange;

        int columnsFromLeftWall = playerColumn - DivideRoundingDown(_playfieldBounds.X, ColumnPixels);
        if (columnsFromLeftWall < TankShellTuning.NoSidewaysMissColumnsFromLeftWall)
        {
            missColumns = 0;
        }

        int gapColumns = playerColumn + missColumns - DivideRoundingDown(_position.X, ColumnPixels);
        int gapRows = playerRow + missRows - DivideRoundingDown(_position.Y, RowPixels);
        return new IntVector2(ComputeAimedSpeed(gapColumns, shellSpeed), ComputeAimedSpeed(gapRows, shellSpeed));
    }

    /// <summary>Works out the starting speed of a shot aimed at a wall, so that it bounces back across the field.</summary>
    /// <param name="playerPosition">The top-left corner of the player, in port pixels.</param>
    /// <param name="shellSpeed">The wave's shell setting.</param>
    /// <param name="random">Where the shot's direction and misses come from.</param>
    /// <remarks>
    /// Original source: <c>RRTK4.ASM</c> <c>TNKFRB</c>, <c>TRBY</c> and <c>TRBXY</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) from <c>$4ED4</c> to <c>$4F7E</c>.
    /// Seven times in eight the shot goes for the wall on the player's own side of the field; the other time it goes for the far wall.
    /// </remarks>
    private IntVector2 GetReboundVelocity(IntVector2 playerPosition, int shellSpeed, Random random)
    {
        int shellColumn = DivideRoundingDown(_position.X, ColumnPixels);
        int shellRow = DivideRoundingDown(_position.Y, RowPixels);
        int playerColumn = DivideRoundingDown(playerPosition.X, ColumnPixels);
        int playerRow = DivideRoundingDown(playerPosition.Y, RowPixels);
        int leftColumn = DivideRoundingDown(_playfieldBounds.X, ColumnPixels);
        int rightColumn = DivideRoundingDown(_playfieldBounds.Right, ColumnPixels);
        int topRow = DivideRoundingDown(_playfieldBounds.Y, RowPixels);
        int bottomRow = DivideRoundingDown(_playfieldBounds.Bottom, RowPixels);

        bool aimAtTopOrBottom = random.Next(2) == 1;
        bool farSide = random.Next(TankShellTuning.ReboundSideRollSides) == TankShellTuning.ReboundFarSideRoll;
        int targetColumn;
        int targetRow;
        if (aimAtTopOrBottom)
        {
            int missColumns = random.Next(2 * TankShellTuning.ReboundWidthMissRange) - TankShellTuning.ReboundWidthMissRange;
            targetColumn = DivideRoundingDown(missColumns + shellColumn + playerColumn, 2);
            bool playerInBottomHalf = playerRow >= (topRow + bottomRow) / 2;
            targetRow = playerInBottomHalf != farSide ? bottomRow : topRow;
        }
        else
        {
            int missRows = random.Next(2 * TankShellTuning.ReboundHeightMissRange) - TankShellTuning.ReboundHeightMissRange;
            targetRow = DivideRoundingDown(missRows + shellRow + playerRow, 2);
            bool playerInRightHalf = playerColumn >= (leftColumn + rightColumn) / 2;
            targetColumn = playerInRightHalf != farSide ? rightColumn : leftColumn;
        }

        return GetBoostedVelocity(new IntVector2(targetColumn - shellColumn, targetRow - shellRow), shellSpeed);
    }

    /// <summary>Moves the shell by one ROM frame's worth of speed. A move that would take it out of the playfield is not made.</summary>
    /// <remarks>Original source: <c>RRS22.ASM</c> <c>OPB80</c>, the shared mover. Disassembly: not separately labelled.</remarks>
    private void MoveOneFrame()
    {
        IntVector2 subpixels = GetNextFrameSubpixels();
        int stepX = DivideRoundingDown(subpixels.X, ScreenSize.SubpixelsPerPixel);
        int stepY = DivideRoundingDown(subpixels.Y, ScreenSize.SubpixelsPerPixel);

        // A move that is refused leaves the position, and the carried remainder with it, exactly as it was.
        int x = _position.X;
        int remainderX = _remainderSubpixels.X;
        if (FitsInsideX(_position.X + stepX))
        {
            x += stepX;
            remainderX = subpixels.X - (stepX * ScreenSize.SubpixelsPerPixel);
        }

        int y = _position.Y;
        int remainderY = _remainderSubpixels.Y;
        if (FitsInsideY(_position.Y + stepY))
        {
            y += stepY;
            remainderY = subpixels.Y - (stepY * ScreenSize.SubpixelsPerPixel);
        }

        _position = new IntVector2(x, y);
        _remainderSubpixels = new IntVector2(remainderX, remainderY);
    }

    /// <summary>Says whether the shell, put at this sideways position, would be inside the playfield.</summary>
    /// <param name="x">The sideways position of the shell's left side.</param>
    private bool FitsInsideX(int x) => x >= _playfieldBounds.X && x + BoxWidth <= _playfieldBounds.Right;

    /// <summary>Says whether the shell, put at this up-and-down position, would be inside the playfield.</summary>
    /// <param name="y">The up-and-down position of the shell's top.</param>
    private bool FitsInsideY(int y) => y >= _playfieldBounds.Y && y + BoxHeight <= _playfieldBounds.Bottom;

    /// <summary>Works out, in 256ths of a port pixel, how far the shell has to go after one more ROM frame, counting what it carried over.</summary>
    private IntVector2 GetNextFrameSubpixels() => new(
        _remainderSubpixels.X + (_velocity.X * ColumnPixels),
        _remainderSubpixels.Y + (_velocity.Y * RowPixels));

    /// <summary>Works out where the shell will be after one more ROM frame, from its speed and its carried remainder.</summary>
    private IntVector2 GetNextPosition()
    {
        IntVector2 subpixels = GetNextFrameSubpixels();
        return new IntVector2(
            _position.X + DivideRoundingDown(subpixels.X, ScreenSize.SubpixelsPerPixel),
            _position.Y + DivideRoundingDown(subpixels.Y, ScreenSize.SubpixelsPerPixel));
    }

    /// <summary>Runs one beat: bounce if the next move would hit a wall, otherwise count down the shell's life.</summary>
    /// <remarks>
    /// Original source: <c>RRTK4.ASM</c> <c>SHELL</c>, <c>XVNEG</c> and <c>YVNEG</c>. Disassembly: <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>), <c>TANK_SHELL_BOUNCE_HORIZONTAL</c> (<c>$4FC1</c>) and <c>TANK_SHELL_BOUNCE_VERTICAL</c> (<c>$4FC7</c>).
    /// A beat that bounces the shell does not count down its life, and a sideways bounce skips the up-and-down check.
    /// </remarks>
    private void RunBeat()
    {
        IntVector2 next = GetNextPosition();
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

        if (--_beatsRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }
}
