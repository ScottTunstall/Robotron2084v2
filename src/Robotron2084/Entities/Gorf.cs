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
    private static readonly int StepPixels = ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns);

    private readonly ArcadeFrameTimer _animationArcadeFrameTimer = new();
    private readonly ArcadeFrameTimer _beatArcadeFrameTimer = new();
    private readonly int _directionSignX;
    private readonly Queue<int> _dropSteps = new();
    private readonly int _groundY;
    private readonly int _maxDropsX2;
    private readonly Rectangle _playfieldBounds;
    private readonly Random _random;
    private readonly TextureSet _textureSet;
    private readonly int _totalSteps;
    private IntVector2 _position;
    private int _stepCount;
    private int _stepInHop;

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
    public Gorf(TextureSet textureSet, Random random, Rectangle playfieldBounds, int maxDropsX2)
    {
        _textureSet = textureSet;
        _playfieldBounds = playfieldBounds;
        _random = random;
        _maxDropsX2 = maxDropsX2;
        _directionSignX = -1;
        var isEnteringFromLeft = random.Next(2) == 0;
        if (isEnteringFromLeft) _directionSignX = 1;

        var highest = playfieldBounds.Y + HopPixels;
        var lowest = Math.Max(highest, playfieldBounds.Bottom - CollisionSize.Height);
        _groundY = highest + random.Next(lowest - highest + 1);
        var startX = playfieldBounds.Right;
        if (isEnteringFromLeft) startX = playfieldBounds.X - CollisionSize.Width;

        _position = new IntVector2(startX, _groundY);
        _totalSteps = (playfieldBounds.Width + CollisionSize.Width) / StepPixels;

        for (var stop = 1; stop <= GorfTuning.DropStops; stop++)
            _dropSteps.Enqueue(stop * _totalSteps / (GorfTuning.DropStops + 1));
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

    /// <summary>Kills Gorf at once, with no flash and no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
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

        if (_animationArcadeFrameTimer.Tick(GorfTuning.AnimationIntervalArcadeFrames)) ShowNextAnimationFrame();
        if (_beatArcadeFrameTimer.Tick(GorfTuning.BeatIntervalArcadeFrames)) ActOnBeat(playField);
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
        _stepInHop++;
        var height = GorfPath.GetHopHeight(_stepInHop, GorfTuning.HopSteps, HopPixels);
        _position = new IntVector2(_position.X + _directionSignX * StepPixels, _groundY - height);
        if (_stepInHop == GorfTuning.HopSteps) _stepInHop = 0;

        if (_dropSteps.Count > 0 && _stepCount >= _dropSteps.Peek())
        {
            _dropSteps.Dequeue();
            DropGrunts(playField);
        }

        if (_stepCount >= _totalSteps) Kill();
    }

    /// <summary>Drops some grunts side by side from Gorf's body.</summary>
    /// <param name="playField">The playfield, which puts the grunts on the field and holds them back when the wave is full.</param>
    /// <remarks>
    ///     How many is picked the way a spheroid picks its enforcers: a random number from 1 up to the wave's limit, halved
    ///     and rounded up. It is never more than <see cref="GorfTuning.MaxGruntsPerDrop" />.
    /// </remarks>
    private void DropGrunts(PlayField playField)
    {
        var count = Math.Min(GorfTuning.MaxGruntsPerDrop, (_random.Next(_maxDropsX2) + 2) / 2);
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
