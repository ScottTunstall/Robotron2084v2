using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     GORF (Galactic Orbiting Robot Force Leader) hops across the screen in a string of jumps, from one side to the other, dropping grunts as it goes. It
///     does not shoot.
/// </summary>
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, and takes one step on each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, and <see cref="_animationArcadeFrameTimer" /> when it is
///     time for the next animation frame.
///     Gorf is the author's own robot. It is not in the arcade game (notes §138.2). It starts off the screen, on a random
///     side and
///     at a random height. It hops to the far side (<see cref="GorfPath" />) and then it is gone. Every hop is the same
///     height
///     (<see cref="GorfTuning.HopRows" />).
///     At a few points on the way (<see cref="GorfTuning.DropStops" />) it drops some grunts side by side, which fall to
///     the ground.
///     How many it drops is picked at random, the way a spheroid picks how many enforcers to drop, from the wave's
///     <c>ENFNUM</c>
///     (<see cref="WaveParameters.MaxDropsX2" />). So later waves and harder settings drop more. A wave can only hold so
///     many
///     grunts, so Gorf drops fewer, or none, when the wave is full.
///     A Gorf that gets across scores nothing. A Gorf that is shot scores the same as a grunt.
/// </remarks>
public sealed class Gorf : IExplodable, IRemovable
{
    /// <summary>How big Gorf is, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GorfCollisionSize.Height));

    /// <summary>How high every hop goes, in port pixels.</summary>
    private static readonly int HopPixels = ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows);

    /// <summary>How far Gorf goes sideways on each step, in port pixels.</summary>
    private const int StepPixels = GorfTuning.StepPixels;

    private readonly ArcadeFrameTimer _animationArcadeFrameTimer = new();
    private readonly ArcadeFrameTimer _beatArcadeFrameTimer = new();
    private readonly int _directionSignX;
    private readonly Queue<int> _dropCounts = new();
    private readonly Queue<int> _dropSteps = new();
    private readonly int _groundY;
    private readonly int _maxDropsX2;
    private readonly Rectangle _playfieldBounds;
    private readonly Random _random;
    private readonly TextureSet _textureSet;
    private readonly int _totalSteps;
    private IntVector2 _position;
    private PlayField? _playField;
    private readonly int _dropIntervalSteps;
    private int _pendingDropCount;
    private string? _lineToSay;
    private int _stepCount;

    /// <summary>
    ///     Makes a Gorf just off the screen, on a random side and at a random height, and works out where on its way it
    ///     will drop grunts.
    /// </summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="random">Where its random numbers come from. They pick the side, the height and how many grunts it drops.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels. Gorf starts and ends outside it.</param>
    /// <param name="maxDropsX2">
    ///     This wave's limit on how many grunts it drops at once. The number is picked the way a spheroid
    ///     picks its enforcers.
    /// </param>
    /// <param name="gruntDropCount">
    ///     How many grunts it drops in all, one at a time, evenly spaced along its way across, so they come gradually. It
    ///     hands any it has not dropped yet to a Gorf that comes back if it is killed first. 0 leaves its three stops to
    ///     chance (<paramref name="maxDropsX2" />).
    /// </param>
    /// <param name="dropIntervalSteps">
    ///     How many steps pass between one drop and the next, or 0 to spread the drops evenly over its way across. A Gorf
    ///     that comes back is given a shorter one than the Gorf it follows.
    /// </param>
    /// <param name="playerBoxToAvoid">
    ///     Where the player is, or null. Gorf is put at a height where its whole body, hops included, stays clear of this
    ///     box, so it does not appear on a collision course with the player.
    /// </param>
    /// <param name="lineToSay">
    ///     The name of a speech line (<c>Content/Speech/</c>) to say the first time it appears, or null to say nothing.
    ///     It is said once.
    /// </param>
    public Gorf(TextureSet textureSet, Random random, Rectangle playfieldBounds, int maxDropsX2, int gruntDropCount = 0,
        int dropIntervalSteps = 0, Rectangle? playerBoxToAvoid = null, string? lineToSay = null)
    {
        _lineToSay = lineToSay;
        _textureSet = textureSet;
        _playfieldBounds = playfieldBounds;
        _random = random;
        _maxDropsX2 = maxDropsX2;
        _directionSignX = DirectionSigns.Left;
        var isEnteringFromLeft = random.Next(2) == 0;
        if (isEnteringFromLeft) _directionSignX = DirectionSigns.Right;

        var highest = playfieldBounds.Y + HopPixels;
        var lowest = Math.Max(highest, playfieldBounds.Bottom - CollisionSize.Height);
        _groundY = ChooseGroundY(highest, lowest, playerBoxToAvoid);
        var startX = playfieldBounds.Right;
        if (isEnteringFromLeft) startX = playfieldBounds.X - CollisionSize.Width;

        _position = new IntVector2(startX, _groundY);
        _totalSteps = (playfieldBounds.Width + CollisionSize.Width) / StepPixels;

        _dropIntervalSteps = dropIntervalSteps;
        if (_dropIntervalSteps <= 0 && gruntDropCount > 0) _dropIntervalSteps = _totalSteps / (gruntDropCount + 1);

        if (gruntDropCount > 0) PlanGradualDrops(gruntDropCount);
        else PlanDropStops();
    }

    /// <summary>
    ///     Alive until it is shot or gets across the screen. It is never Dying, because it has no death animation (see
    ///     <see cref="Kill" />).
    /// </summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where Gorf's top-left corner is.</summary>
    public IntVector2 Position => _position;

    /// <summary>Which of the two animation frames is showing, 0 or 1.</summary>
    internal int AnimationFrameIndex { get; private set; }

    /// <summary>How many steps there are between one drop and the next, or 0 when it drops at its three stops by chance.</summary>
    internal int DropIntervalSteps => _dropIntervalSteps;

    /// <summary>Draws the part of the current animation frame that is inside the wall, so Gorf comes in from off the screen.</summary>
    /// <param name="spriteBatch">What Gorf is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        var animationFrame = GetCurrentAnimationFrameTexture();
        var drawn = BlitterDraw.GetDrawnBounds(GetBounds(), animationFrame);
        var visible = Rectangle.Intersect(drawn, _playfieldBounds);
        if (visible.IsEmpty) return;

        var pixel = ScreenSize.ToPortPixelsFromArcadePixels(1);
        var source = new Rectangle((visible.X - drawn.X) / pixel, (visible.Y - drawn.Y) / pixel, visible.Width / pixel,
            visible.Height / pixel);
        _textureSet.BlitterDraw.UsePassThrough();
        spriteBatch.Draw(animationFrame, visible, source, Color.White);
    }

    /// <summary>The box Gorf takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>Gets the animation frame Gorf is showing.</summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _textureSet.GorfAnimationFrameTextures[AnimationFrameIndex];
    }

    /// <inheritdoc />
    /// <remarks>It is the animation frame showing now (<see cref="GetCurrentAnimationFrameTexture" />).</remarks>
    public Texture2D GetExplosionAnimationFrameTexture()
    {
        return GetCurrentAnimationFrameTexture();
    }

    /// <inheritdoc />
    /// <remarks>It is the same box as <see cref="GetBounds" />.</remarks>
    public Rectangle GetExplosionBounds()
    {
        return GetBounds();
    }

    /// <summary>
    ///     Kills Gorf at once, with no flash and no death animation. If it still has grunts to drop, a new Gorf comes on a
    ///     little later to drop them, a little faster (<see cref="GorfTuning.ReturnDelayTicks" />), so a wave that is owed
    ///     a number of grunts still gets them all.
    /// </summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
        if (_playField is null || (_dropCounts.Count == 0 && _pendingDropCount == 0)) return;

        var gruntsOwed = _dropCounts.Sum() + _pendingDropCount;
        _pendingDropCount = 0;
        _dropCounts.Clear();
        _dropSteps.Clear();
        var fasterInterval = Math.Max(1, _dropIntervalSteps * GorfTuning.ReturnDropIntervalFifths / 5);
        _playField.ScheduleGorfReturn(gruntsOwed, fasterInterval);
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. On a
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> Gorf animates, takes a step along its hop, and drops
    ///     grunts if it has reached a drop point.
    /// </summary>
    /// <param name="gameTime">Not used. Gorf counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield, which the dropped grunts are put on.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (!this.IsAlive() || playField.RobotsFrozen()) return;

        _playField = playField;
        SayTheAppearanceLine(playField);

        if (_animationArcadeFrameTimer.Tick(GorfTuning.AnimationIntervalArcadeFrames)) ShowNextAnimationFrame();
        if (_beatArcadeFrameTimer.Tick(GorfTuning.BeatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>Says the line Gorf was given to say when it first appears, once, and then has none left to say.</summary>
    /// <param name="playField">The playfield, which has the line said.</param>
    private void SayTheAppearanceLine(PlayField playField)
    {
        if (_lineToSay is not { } lineName) return;

        _lineToSay = null;
        playField.SayLine(lineName);
    }

    /// <summary>
    ///     Gets how high above the ground Gorf is after a number of steps. Each hop takes <see cref="GorfTuning.HopSteps" />
    ///     steps, and Gorf is back on the ground at the end of each.
    /// </summary>
    /// <param name="stepsTaken">How many steps Gorf has taken, counting from 1.</param>
    /// <returns>The height, in port pixels.</returns>
    internal static int GetHopRisePixels(int stepsTaken)
    {
        var stepInHop = (stepsTaken - 1) % GorfTuning.HopSteps + 1;
        return GorfPath.GetHopHeight(stepInHop, GorfTuning.HopSteps, HopPixels);
    }

    /// <summary>How many more times it will drop grunts.</summary>
    internal int GetDropStopsRemaining()
    {
        return _dropSteps.Count;
    }

    /// <summary>
    ///     What Gorf does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It takes one step along the hop, drops grunts if a drop is due, and is gone once
    ///     it has crossed the playfield.
    /// </summary>
    /// <param name="playField">The playfield, which the dropped grunts are put on.</param>
    private void ActOnBeat(PlayField playField)
    {
        _stepCount++;
        _position = new IntVector2(_position.X + _directionSignX * StepPixels, _groundY - GetHopRisePixels(_stepCount));

        while (_dropSteps.Count > 0 && _stepCount >= _dropSteps.Peek())
        {
            if (_pendingDropCount == 0) _pendingDropCount = GetDropCount();

            // A drop never lands on an electrode: it waits until Gorf has moved on to somewhere clear.
            if (!playField.IsClearOfElectrodes(GetDropColumn(_pendingDropCount))) break;

            _dropSteps.Dequeue();
            var count = _pendingDropCount;
            _pendingDropCount = 0;
            DropGrunts(playField, count);
        }

        if (_stepCount >= _totalSteps) Kill();
    }

    /// <summary>Plans the usual drops: <see cref="GorfTuning.DropStops" /> stops, evenly spaced, each dropping a number by chance.</summary>
    private void PlanDropStops()
    {
        for (var stop = 1; stop <= GorfTuning.DropStops; stop++)
            _dropSteps.Enqueue(stop * _totalSteps / (GorfTuning.DropStops + 1));
    }

    /// <summary>Plans an exact number of drops: one grunt at a time, the same number of steps apart.</summary>
    /// <param name="gruntDropCount">How many grunts to drop in all.</param>
    private void PlanGradualDrops(int gruntDropCount)
    {
        for (var drop = 1; drop <= gruntDropCount; drop++)
        {
            _dropSteps.Enqueue(drop * _dropIntervalSteps);
            _dropCounts.Enqueue(1);
        }
    }

    /// <summary>
    ///     Chooses the height of the ground Gorf hops along. It is by chance anywhere the whole of Gorf, hops included, is
    ///     inside the wall, and, when there is a player to avoid, anywhere its whole body stays clear of the player's box.
    /// </summary>
    /// <param name="highest">The highest ground there is, in port pixels from the top.</param>
    /// <param name="lowest">The lowest ground there is.</param>
    /// <param name="playerBoxToAvoid">Where the player is, or null.</param>
    private int ChooseGroundY(int highest, int lowest, Rectangle? playerBoxToAvoid)
    {
        if (playerBoxToAvoid is not { } playerBox) return highest + _random.Next(lowest - highest + 1);

        // Gorf's body runs from HopPixels above the ground to its own height below it.
        var lowestGroundAbovePlayer = Math.Min(lowest, playerBox.Y - CollisionSize.Height - GorfTuning.PlayerClearancePixels);
        var highestGroundBelowPlayer = Math.Max(highest, playerBox.Bottom + HopPixels + GorfTuning.PlayerClearancePixels);
        var roomAbove = Math.Max(0, lowestGroundAbovePlayer - highest + 1);
        var roomBelow = Math.Max(0, lowest - highestGroundBelowPlayer + 1);
        if (roomAbove + roomBelow == 0) return highest + _random.Next(lowest - highest + 1);

        var pick = _random.Next(roomAbove + roomBelow);
        if (pick < roomAbove) return highest + pick;

        return highestGroundBelowPlayer + (pick - roomAbove);
    }

    /// <summary>
    ///     Gets how many grunts this stop drops: its exact share when Gorf was given a number to drop in all. Otherwise
    ///     it is picked the way a spheroid picks its enforcers (a random number from 1 up to the wave's limit, halved and
    ///     rounded up) and is never more than <see cref="GorfTuning.MaxGruntsPerDrop" />.
    /// </summary>
    private int GetDropCount()
    {
        if (_dropCounts.Count > 0) return _dropCounts.Dequeue();

        return Math.Min(GorfTuning.MaxGruntsPerDrop, (_random.Next(_maxDropsX2) + 2) / 2);
    }

    /// <summary>
    ///     Gets the box the grunts of a drop pass through on their way down: from Gorf's body to the ground, as wide as the
    ///     row of grunts.
    /// </summary>
    /// <param name="count">How many grunts the drop has, standing side by side.</param>
    private Rectangle GetDropColumn(int count)
    {
        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var gruntHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        var spacing = gruntWidth + GorfTuning.DropGapPixels;
        var rowWidth = gruntWidth + (count - 1) * spacing;
        var left = _position.X + (CollisionSize.Width - rowWidth) / 2;
        var top = _position.Y + (CollisionSize.Height - gruntHeight) / 2;
        var bottom = _groundY + CollisionSize.Height;
        return new Rectangle(left, top, rowWidth, Math.Max(gruntHeight, bottom - top));
    }

    /// <summary>Drops some grunts side by side from Gorf's body.</summary>
    /// <param name="playField">The playfield, which puts the grunts on the field and holds them back when the wave is full.</param>
    /// <param name="count">How many grunts to drop.</param>
    private void DropGrunts(PlayField playField, int count)
    {
        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var gruntHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        var spacing = gruntWidth + GorfTuning.DropGapPixels;
        var centreX = _position.X + (CollisionSize.Width - gruntWidth) / 2;
        var bodyY = _position.Y + (CollisionSize.Height - gruntHeight) / 2;
        var groundY = _groundY + CollisionSize.Height - gruntHeight;

        for (var index = 0; index < count; index++)
        {
            var sideways = (2 * index - (count - 1)) * spacing / 2;
            playField.SpawnGrunt(new IntVector2(centreX + sideways, bodyY), new IntVector2(centreX + sideways, groundY));
        }
    }

    /// <summary>Moves on to the next of Gorf's animation frames, and goes back to the first after the last one.</summary>
    private void ShowNextAnimationFrame()
    {
        AnimationFrameIndex = (AnimationFrameIndex + 1) % _textureSet.GorfAnimationFrameTextures.Length;
    }
}
