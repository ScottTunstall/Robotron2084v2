using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>Gorf hops across the screen in a string of small jumps, from one side to the other, dropping grunts as it goes. It does not shoot.</summary>
/// <remarks>
/// A new kind of robot of the author's own with no arcade routine behind it (notes §138.2). It starts off the screen, on a random side and at a random
/// height, and hops to the far side (<see cref="GorfPath"/>), then goes. Every hop is the same height, 16 pixels for now (<see cref="GorfTuning.HopRows"/>). On the way it drops grunts, evenly spaced along its path. How many it
/// drops is rolled as a spheroid rolls its enforcers: from the wave's <c>ENFNUM</c> (<see cref="LevelParameters.MaxDropsX2"/>), so it grows with the wave and the
/// difficulty. A Gorf that gets across is gone for good and scores nothing; one that is shot scores as a grunt does.
/// </remarks>
public sealed class Gorf : IExplodable, IRemovable
{
    /// <summary>The robot's own box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.GorfCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.GorfCollisionSize.Height));

    /// <summary>How many timer units between steps (a tick adds 5; an arcade frame is 6 units).</summary>
    private static readonly int StepClockUnits = ArcadeClock.ToClockUnits(GorfTuning.StepRomFrames);

    /// <summary>How many timer units each animation frame shows for.</summary>
    private static readonly int AnimationFrameClockUnits = ArcadeClock.ToClockUnits(GorfTuning.AnimationFrameRomFrames);

    /// <summary>How far each step goes sideways, in port pixels.</summary>
    private static readonly int StepPixels = ScreenSize.ToPortPixelsFromColumns(GorfTuning.StepColumns);

    /// <summary>How high every hop goes, in port pixels.</summary>
    private static readonly int HopPixels = ScreenSize.ToPortPixels(GorfTuning.HopRows);

    private readonly int _direction;
    private readonly Queue<int> _dropSteps = new();
    private readonly int _groundY;
    private readonly Rectangle _playfield;
    private readonly SpriteSet _sprites;
    private readonly int _totalSteps;
    private int _animationFrameIndex;
    private int _animationTimer;
    private IntVector2 _position;
    private int _step;
    private int _stepInHop;
    private int _stepTimer;

    /// <summary>Creates a Gorf just off the screen, on a random side and at a random height, with its drops already rolled.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="random">The random source: the side, the height and the drops.</param>
    /// <param name="playfield">The inside of the wall, in port pixels. Gorf starts and ends outside it.</param>
    /// <param name="maxDropsX2">This wave's drop bound, rolled as a spheroid's is.</param>
    public Gorf(SpriteSet sprites, Random random, Rectangle playfield, int maxDropsX2)
    {
        _sprites = sprites;
        _playfield = playfield;
        _direction = random.Next(2) == 0 ? 1 : -1;
        int highest = playfield.Y + HopPixels;
        int lowest = Math.Max(highest, playfield.Bottom - CollisionSize.Height);
        _groundY = highest + random.Next(lowest - highest + 1);
        _position = new IntVector2(_direction > 0 ? playfield.X - CollisionSize.Width : playfield.Right, _groundY);
        _totalSteps = (playfield.Width + CollisionSize.Width) / StepPixels;

        // The drops: a random roll from 1 to the bound, halved and rounded up, as a spheroid's enforcers are.
        int drops = (random.Next(maxDropsX2) + 2) / 2;
        for (int drop = 1; drop <= drops; drop++)
        {
            _dropSteps.Enqueue(drop * _totalSteps / (drops + 1));
        }
    }

    /// <summary>The robot's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or across the screen; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the Gorf.</summary>
    public IntVector2 Position => _position;

    /// <summary>How many grunts it has still to drop (test hook).</summary>
    internal int DropsRemaining => _dropSteps.Count;

    /// <summary>Which of the two animation frames is showing, 0 or 1 (test hook).</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Gets the animation frame the Gorf is showing, for drawing and for the appear and explosion effects.</summary>
    /// <returns>The texture for the current frame.</returns>
    public Texture2D GetCurrentAnimationFrame() => _sprites.GorfAnimationFrames[_animationFrameIndex];

    /// <summary>Draws the part of the current animation frame that is inside the wall, so Gorf comes in from off the screen.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        Texture2D frame = GetCurrentAnimationFrame();
        Rectangle drawn = BlitterDraw.DrawnRect(Bounds, frame);
        Rectangle visible = Rectangle.Intersect(drawn, _playfield);
        if (visible.IsEmpty)
        {
            return;
        }

        int pixel = ScreenSize.ToPortPixels(1);
        var source = new Rectangle((visible.X - drawn.X) / pixel, (visible.Y - drawn.Y) / pixel, visible.Width / pixel, visible.Height / pixel);
        _sprites.Blitter.UsePassThrough();
        spriteBatch.Draw(frame, visible, source, Color.White);
    }

    /// <summary>Kills the Gorf outright: no flash, no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick: animates, and on each step moves along its hop and drops a grunt when one is due.</summary>
    /// <param name="gameTime">Unused — the steps are counted in ticks.</param>
    /// <param name="field">The playfield, which the dropped grunts are put on.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive() || field.RobotsFrozen)
        {
            return;
        }

        Animate();
        _stepTimer += ArcadeClock.UnitsPerPortTick;
        if (_stepTimer < StepClockUnits)
        {
            return;
        }

        _stepTimer -= StepClockUnits;
        Step(field);
    }

    private void Animate()
    {
        _animationTimer += ArcadeClock.UnitsPerPortTick;
        if (_animationTimer < AnimationFrameClockUnits)
        {
            return;
        }

        _animationTimer -= AnimationFrameClockUnits;
        _animationFrameIndex = (_animationFrameIndex + 1) % _sprites.GorfAnimationFrames.Length;
    }

    private void Step(PlayField field)
    {
        _step++;
        _stepInHop++;
        int height = GorfPath.GetHopHeight(_stepInHop, GorfTuning.HopSteps, HopPixels);
        _position = new IntVector2(_position.X + (_direction * StepPixels), _groundY - height);
        if (_stepInHop == GorfTuning.HopSteps)
        {
            _stepInHop = 0;
        }

        if (_dropSteps.Count > 0 && _step >= _dropSteps.Peek())
        {
            _dropSteps.Dequeue();
            field.SpawnGrunt(_position);
        }

        if (_step >= _totalSteps)
        {
            Kill();
        }
    }
}
