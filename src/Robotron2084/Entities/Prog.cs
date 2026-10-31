using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A prog is a family member that a brain has caught and turned into an enemy. It looks like the family member it
///     used to be, but now it hunts the player.
/// </summary>
/// <seealso cref="FamilyMember" />
/// <seealso cref="StripEffect" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRB10.ASM</c>, routine <c>PROG</c> (with
///             <c>PROGST</c>/<c>GPOFF</c>/<c>GPDIR</c>/<c>PRGKIL</c>)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$1EAB</c> (<c>ANIMATE_PROG</c>)</item>
///     </list>
/// </remarks>
public sealed class Prog : IExplodable, IRemovable
{
    /// <summary>
    ///     The range of the random number that decides whether a prog goes sideways or up and down. Each is picked half the
    ///     time.
    /// </summary>
    private const int AxisFlipSides = 2;

    /// <summary>How long one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> lasts.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>PROG</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    ///     The arcade runs the prog's routine this often.
    /// </remarks>
    private const int BeatIntervalArcadeFrames = 3;

    /// <summary>The nearest to the right wall, in columns, that the top-left corner of a prog's explosion may be.</summary>
    /// <remarks>Disassembly: <c>$1F45</c>, <c>LDA #$8A / CMPA $0004,X</c>. The right wall is at column <c>$8F</c>.</remarks>
    private const int ExplosionColumnsFromRightWallMin = 0x8F - 0x8A;

    /// <summary>The nearest to the bottom wall, in rows, that the top-left corner of a prog's explosion may be.</summary>
    /// <remarks>Disassembly: <c>$1F4D</c>, <c>LDA #$DB / CMPA $0005,X</c>. The bottom wall is at row <c>$EA</c>.</remarks>
    private const int ExplosionRowsFromBottomWallMin = 0xEA - 0xDB;

    /// <summary>
    ///     A prog heads for a spot a little to the left or right of the player. This, with <see cref="OffsetXRandomMax" />, sets how far
    ///     off it can be, in steps of <see cref="OffsetXStepColumns" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>GPOFF</c>, which gives up to 28 columns left or right of the player,
    ///             in steps of 4.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private const int OffsetXHalfRange = 8;

    /// <summary>The biggest random number behind a prog's sideways offset. The smallest is one.</summary>
    private const int OffsetXRandomMax = 15;

    /// <summary>The size of one step of a prog's sideways offset, in columns.</summary>
    private const int OffsetXStepColumns = 4;

    /// <summary>
    ///     A prog heads for a spot a little above or below the player. This, with <see cref="OffsetYSteps" />, sets how far off it can
    ///     be, in steps of <see cref="OffsetYStepRows" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private const int OffsetYCentre = 19;

    /// <summary>The size of one step of a prog's up-and-down offset, in rows.</summary>
    private const int OffsetYStepRows = 2;

    /// <summary>The biggest random number behind a prog's up-and-down offset. The smallest is one.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c>, which works it out from a random number from 1 to 18.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private const int OffsetYSteps = 18;

    /// <summary>
    ///     How often a prog picks a new direction: about one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> in
    ///     ten.
    /// </summary>
    private const int ReDirectionThreshold256 = 0xE4;

    /// <summary>
    ///     The cut-off for how often a prog picks again how far off the player the spot it heads for is. The higher it is,
    ///     the less often it happens.
    /// </summary>
    private const int ReOffsetThreshold256 = 0xF8;

    /// <summary>
    ///     How far a prog goes on a step to the left or right, in columns. It is the same distance on the screen as a
    ///     step up or down.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>GPOFF</c>. The table of sideways steps moves 2 columns at a time,
    ///             and a column is 2 arcade pixels wide.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private const int StepXColumns = 2;

    /// <summary>How far a prog goes on a step up or down, in rows.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>GPOFF</c>, the table of up and down steps.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private const int StepYRows = 4;

    /// <summary>
    ///     The range of the random numbers that decide how often a prog picks a new direction or a new offset: from 0 up to
    ///     one less than this.
    /// </summary>
    private const int ThresholdRandomRange = 256;

    /// <summary>
    ///     How far past the right wall, in columns, the spot a prog heads for can be before it wraps round to the left wall.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>GPDIR</c>, which moves a spot that is this far past one wall to
    ///             the opposite wall.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private const int WrapMarginXColumns = 0x30;

    /// <summary>
    ///     How far below the bottom wall, in rows, the spot a prog heads for can be before it wraps round to the top wall.
    /// </summary>
    private const int WrapMarginYRows = 18;

    // The order the walk animation frames are shown in. Family members walk in the same order.
    private static readonly int[] WalkCycle = { 0, 1, 0, 2 };

    private readonly ArcadeFrameTimer _beatArcadeFrameTimer = new();
    private readonly (int Width, int Height) _collisionSize;

    /// <summary>The ghosts the prog has left behind it, newest first (see <see cref="Ghost" />).</summary>
    private readonly List<Ghost> _ghosts = new();

    private readonly Random _random;

    private readonly TextureSet _textureSet;

    // The way the prog is walking: up, down, left or right. A new prog walks down until it picks a direction.
    private Direction8 _direction = Direction8.Down;

    // How far to the left or right of the Player the spot is that the prog heads for, in columns.
    private int _offsetX;

    // How far above or below the Player the spot is that the prog heads for, in rows.
    private int _offsetY;

    /// <summary>
    ///     The inside of the wall as it was on the last update, for keeping the explosion clear of the right and bottom
    ///     walls.
    /// </summary>
    private Rectangle? _playfieldBounds;

    private IntVector2 _position;

    // Which place in WalkCycle the prog has reached.
    private int _walkCycleIndex;

    /// <summary>Makes a prog where the family member was. It has that family member's animation frames and size.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where the prog's top-left corner is.</param>
    /// <param name="kind">Which family member it used to be. This decides its animation frames and its size.</param>
    /// <param name="random">Where its random numbers come from. They pick how far off the player the spot it heads for is, and when it turns.</param>
    public Prog(TextureSet textureSet, IntVector2 position, FamilyMemberKind kind, Random random)
    {
        _textureSet = textureSet;
        _position = position;
        Kind = kind;
        _random = random;
        _collisionSize = (
            ScreenSize.ToPortPixelsFromArcadePixels(kind.GetArcadeCollisionSize().Width),
            ScreenSize.ToPortPixelsFromArcadePixels(kind.GetArcadeCollisionSize().Height));
        SetRandomOffsets(); // A new prog picks at once how far off the Player the spot is that it heads for (ROM: PROGST calls GPOFF).
    }

    /// <summary>Which family member this prog used to be. This decides its animation frames and its size.</summary>
    public FamilyMemberKind Kind { get; }

    /// <summary>Alive until it is shot. It is never Dying, because it is blown apart at once (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the prog's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Draws the ghosts, oldest first, and then the prog. Each is drawn as a one-colour shape on a one-colour box.</summary>
    /// <param name="spriteBatch">What the prog is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        var frames = Kind.GetAnimationFrames(_textureSet);
        var animationFrame = frames[GetWalkAnimationFrameIndex()];

        // The oldest ghost is drawn first, so that newer ghosts cover older ones. Each ghost keeps the animation frame the prog had when it left the ghost behind.
        for (var i = _ghosts.Count - 1; i >= 0; i--)
        {
            var ghost = _ghosts[i];
            _textureSet.BlitterDraw.DrawSpriteSolidWithBackground(
                spriteBatch,
                frames[ghost.AnimationFrameIndex],
                GetBoundsAt(ghost.Position),
                _textureSet.BlitterDraw.GetPaletteIndexColour(ProgTuning.GhostBackgroundPaletteIndex),
                _textureSet.BlitterDraw.GetPaletteIndexColour(ProgTuning.GhostShapePaletteIndex));
        }

        _textureSet.BlitterDraw.DrawSpriteSolidWithBackground(
            spriteBatch,
            animationFrame,
            GetBounds(),
            _textureSet.BlitterDraw.GetPaletteIndexColour(ProgTuning.BackgroundPaletteIndex),
            _textureSet.BlitterDraw.GetPaletteIndexColour(ProgTuning.ShapePaletteIndex));
    }

    /// <summary>
    ///     The box the prog takes up on the screen. It is the size of the family member the prog used to be.
    /// </summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, _collisionSize.Width, _collisionSize.Height);
    }

    /// <summary>The walk animation frame the prog is showing.</summary>
    /// <returns>The animation frame that is showing.</returns>
    /// <remarks>
    ///     Disassembly: the prog's animation frame pointer at <c>$0002,X</c>, which stays on the family member's walk
    ///     animation frame until the prog is killed.
    /// </remarks>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return Kind.GetAnimationFrames(_textureSet)[GetWalkAnimationFrameIndex()];
    }

    /// <summary>
    ///     The sprite that is blown apart when the prog is killed. It is a special sprite for this, not one of the family
    ///     member's animation frames.
    /// </summary>
    /// <returns>The sprite that is blown apart.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRB10.ASM</c> <c>PRGKIL</c> swaps the sprite to the 12x16 <c>PGXPIC</c>.</item>
    ///         <item>Disassembly: <c>PROG_COLLISION_DETECTION</c>, <c>$1F40</c>, <c>LDD #$1F68 / STD $0002,X</c>.</item>
    ///     </list>
    /// </remarks>
    public Texture2D GetExplosionAnimationFrameTexture()
    {
        return _textureSet.ProgBurstTexture;
    }

    /// <summary>
    ///     The box the explosion starts in. It is at the prog's top-left corner, and it is the size of the sprite that is
    ///     blown apart.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>PRGKIL</c>/<c>EXSTV</c> swap the sprite without moving
    ///             the object.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    ///     The arcade moves the corner left or up when the prog is close to the right wall or the bottom wall, so that
    ///     the explosion starts no further right than <see cref="ExplosionColumnsFromRightWallMin" /> columns from the right
    ///     wall and no lower than <see cref="ExplosionRowsFromBottomWallMin" /> rows from the bottom wall (<c>$1F45</c> to
    ///     <c>$1F53</c>).
    /// </remarks>
    public Rectangle GetExplosionBounds()
    {
        var x = _position.X;
        var y = _position.Y;
        if (_playfieldBounds is { } playfieldBounds)
        {
            var rightLimit = playfieldBounds.Right - ScreenSize.ToPortPixelsFromColumns(ExplosionColumnsFromRightWallMin);
            var bottomLimit = playfieldBounds.Bottom - ScreenSize.ToPortPixelsFromArcadePixels(ExplosionRowsFromBottomWallMin);
            x = Math.Min(x, rightLimit);
            y = Math.Min(y, bottomLimit);
        }

        return new Rectangle(
            x,
            y,
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.ProgBurstSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.ProgBurstSize.Height));
    }

    /// <summary>Kills the prog at once. All that is seen is the strip explosion.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>PRGKIL</c>. The object is gone at once, and only the strip
    ///             explosion is left.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (this.IsAlive()) LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. On a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> the prog shows its next walk animation frame, may change its offset or its direction,
    ///     leaves a ghost, and takes a step.
    /// </summary>
    /// <param name="gameTime">Not used. The prog counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        _playfieldBounds = playField.PlayfieldWall.PlayfieldBounds;
        if (!this.IsAlive()) return;

        if (playField.RobotsFrozen()) return;

        if (_beatArcadeFrameTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>The animation frame each ghost shows, newest first.</summary>
    internal IReadOnlyList<int> GetGhostFrames()
    {
        var frames = new List<int>(_ghosts.Count);
        foreach (var ghost in _ghosts) frames.Add(ghost.AnimationFrameIndex);

        return frames;
    }

    /// <summary>Where each ghost is, newest first.</summary>
    internal IReadOnlyList<IntVector2> GetGhostTrail()
    {
        var positions = new List<IntVector2>(_ghosts.Count);
        foreach (var ghost in _ghosts) positions.Add(ghost.Position);

        return positions;
    }

    /// <summary>
    ///     Which walk animation frame the prog is showing. It comes from the set for the way the prog is walking, in the
    ///     order <see cref="WalkCycle" /> gives.
    /// </summary>
    internal int GetWalkAnimationFrameIndex()
    {
        var walkSequence = _direction switch
        {
            Direction8.Left => WalkSequence.Left,
            Direction8.Right => WalkSequence.Right,
            Direction8.Down => WalkSequence.Down,
            _ => WalkSequence.Up
        };
        return (int)walkSequence * 3 + WalkCycle[_walkCycleIndex];
    }

    /// <summary>
    ///     Says whether a box is wholly inside the playfield. If the prog's next step would not be, the prog does not
    ///     take it.
    /// </summary>
    /// <param name="outerBounds">The inside of the playfield wall.</param>
    /// <param name="innerBounds">The box to check.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>CKLIMV</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private static bool IsInside(Rectangle outerBounds, Rectangle innerBounds)
    {
        return innerBounds.X >= outerBounds.X
               && innerBounds.Y >= outerBounds.Y
               && innerBounds.Right <= outerBounds.Right
               && innerBounds.Bottom <= outerBounds.Bottom;
    }

    /// <summary>
    ///     What the prog does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It shows its next walk animation frame, may change its offset or its direction,
    ///     leaves a ghost, and takes a step.
    /// </summary>
    /// <param name="playField">The playfield.</param>
    private void ActOnBeat(PlayField playField)
    {
        // The walk animation moves on at every beat, even when the prog cannot take its step.
        _walkCycleIndex = (_walkCycleIndex + 1) % WalkCycle.Length;

        // Now and then the prog changes how far off the Player the spot is that it heads for. This comes before picking a direction, because the direction depends on it.
        if (_random.Next(ThresholdRandomRange) > ReOffsetThreshold256) SetRandomOffsets();

        if (_random.Next(ThresholdRandomRange) > ReDirectionThreshold256)
        {
            _direction = GetRandomDirection(playField);
            _walkCycleIndex = 0;
        }

        // Leave a ghost where the prog is standing, even if it then cannot take its step. Only the newest ghosts are kept (ROM: PROG3).
        _ghosts.Insert(0, new Ghost(_position, GetWalkAnimationFrameIndex()));
        if (_ghosts.Count > ProgTuning.GhostCount) _ghosts.RemoveAt(_ghosts.Count - 1);

        // A step goes left, right, up or down. It never goes diagonally.
        var stepX = ScreenSize.ToPortPixelsFromColumns(StepXColumns);
        var stepY = ScreenSize.ToPortPixelsFromArcadePixels(StepYRows);
        var step = _direction switch
        {
            Direction8.Left => new IntVector2(-stepX, 0),
            Direction8.Right => new IntVector2(stepX, 0),
            Direction8.Up => new IntVector2(0, -stepY),
            _ => new IntVector2(0, stepY)
        };

        var candidate = _position + step;
        Rectangle next = new(candidate.X, candidate.Y, _collisionSize.Width, _collisionSize.Height);
        if (IsInside(playField.PlayfieldWall.PlayfieldBounds, next))
        {
            _position = candidate;
        }
        else
        {
            // The step would leave the playfield, so it is not taken and the prog picks a new direction (ROM: CKLIM fails, then GPDIR).
            _direction = GetRandomDirection(playField);
            _walkCycleIndex = 0;
        }
    }

    /// <summary>The prog's box, as it would be at another place. It is used to draw the ghosts.</summary>
    /// <param name="position">Where the top-left corner of the box is.</param>
    private Rectangle GetBoundsAt(IntVector2 position)
    {
        return new Rectangle(position.X, position.Y, _collisionSize.Width, _collisionSize.Height);
    }

    /// <summary>
    ///     Picks which way the prog walks next. Half the time it picks left or right, and half the time up or down, so it
    ///     never walks diagonally.
    /// </summary>
    /// <param name="playField">The playfield, which says where the player and the walls are.</param>
    /// <returns>The way to walk: left, right, up or down.</returns>
    /// <remarks>
    ///     It picks the way that leads towards the spot it is heading for.
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>GPDIR</c>. When the prog is already level with the spot it is
    ///             heading for, it goes left, or up.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private Direction8 GetRandomDirection(PlayField playField)
    {
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        var playerPosition = playField.Player.Position;

        if (_random.Next(AxisFlipSides) == 0)
        {
            // _offsetX is in columns, so it is changed to pixels.
            var destinationX = playerPosition.X + ScreenSize.ToPortPixelsFromColumns(_offsetX);
            if (destinationX > bounds.Right + ScreenSize.ToPortPixelsFromColumns(WrapMarginXColumns)) destinationX = bounds.Left;

            return destinationX <= _position.X ? Direction8.Left : Direction8.Right;
        }

        var destinationY = playerPosition.Y + ScreenSize.ToPortPixelsFromArcadePixels(_offsetY);
        if (destinationY > bounds.Bottom + ScreenSize.ToPortPixelsFromArcadePixels(WrapMarginYRows)) destinationY = bounds.Top;

        return destinationY <= _position.Y ? Direction8.Up : Direction8.Down;
    }

    /// <summary>
    ///     Picks at random how far off the player the spot is that the prog heads for: some columns to the left or right, and some rows above
    ///     or below. The prog keeps heading for a spot that far off until this is called again.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRB10.ASM</c> <c>GPOFF</c>. These numbers are how far off the spot the prog heads for stays from
    ///             the player.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_PROG</c> (<c>$1EAB</c>).</item>
    ///     </list>
    /// </remarks>
    private void SetRandomOffsets()
    {
        _offsetX = (OffsetXRandomMax + 1 - ArcadeRandom.PickUpTo(_random, OffsetXRandomMax) - OffsetXHalfRange) *
                   OffsetXStepColumns;
        _offsetY = (OffsetYCentre - ArcadeRandom.PickUpTo(_random, OffsetYSteps)) * OffsetYStepRows - OffsetYSteps;
    }

    /// <summary>One ghost: a place the prog has left, and the animation frame the prog was showing there.</summary>
    /// <remarks>The arcade draws a ghost once and never draws it again, so a ghost keeps the animation frame it started with.</remarks>
    private readonly record struct Ghost(IntVector2 Position, int AnimationFrameIndex);
}
