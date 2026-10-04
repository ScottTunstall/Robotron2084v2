using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Brings the robots on at the start of a wave: they appear one after another, each one forming out of strips of its own sprite. On a brain wave they are beamed in instead.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, which makes one appear effect each ROM frame (<c>NAP 1,APL</c>), and every fourth one fans in by columns (<c>HAPST</c>); the robots stay switched off (<c>ROBOFF</c>) until the whole sequence finishes</item>
/// <item>Disassembly: not separately labelled</item>
/// </list>
/// A robot waits, still and unseen, from the moment it is queued (notes §61.4).
/// </remarks>
public sealed class WaveMaterialisation
{
    /// <summary>Picks every fourth robot to fan in by columns instead of rows. It is used on <see cref="_sequenceNumber"/>, which counts the robots given an appear effect: the robot fans in by columns when the last two bits of that count are both set.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, <c>ANDA #3 / CMPA #3</c>. Disassembly: not separately labelled.</remarks>
    private const int ColumnFanSequenceMask = 3;

    /// <summary>The robots that are not yet on the field, each with the effect forming it. The effect is null until the robot's turn comes.</summary>
    private readonly Dictionary<IEntity, StripEffect?> _assembling = [];

    /// <summary>The robots waiting for their turn to appear.</summary>
    private readonly Queue<IEntity> _pending = new();

    /// <summary>The robots waiting to be beamed in together, when the wave is a brain wave.</summary>
    private readonly List<IEntity> _transportQueue = [];

    /// <summary>The robots being beamed in.</summary>
    private readonly List<IEntity> _transported = [];

    /// <summary>The transporter that beams the robots in, or null when the wave is not a brain wave.</summary>
    private readonly RobotTransporter? _transporter;

    /// <summary>How many robots have been given an appear effect so far, which decides whether the next fans in by columns.</summary>
    private int _sequenceNumber;

    /// <summary>True once the beaming in has started.</summary>
    private bool _transportBegun;

    /// <summary>Makes the sequence for one wave.</summary>
    /// <param name="random">The field's random source, which the transporter's sparkle uses.</param>
    /// <param name="beamIn">True on a brain wave, where the robots are beamed in by the transporter instead of appearing strip by strip.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLS00</c> and the test of <c>BRNCNT</c> before it ("BRAIN WAVE???").</remarks>
    public WaveMaterialisation(Random random, bool beamIn) => _transporter = beamIn ? new RobotTransporter(random) : null;

    /// <summary>The number of robots that have not yet been given their turn to appear.</summary>
    public int PendingCount => _pending.Count + _transportQueue.Count;

    /// <summary>Moves the sequence on by one tick: starts the appear effect of the next robot in the queue, and lets go of the robots whose effect has finished.</summary>
    /// <remarks>
    /// Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, with the strips closing in on the robot's centre (<c>APCENT</c>). Disassembly: not separately labelled.
    /// The appear effects share a pool with the explosions, so when the pool is full the next robot waits.
    /// </remarks>
    /// <param name="explosions">The field's strip records, which the appear records share.</param>
    /// <param name="clip">The strip engine's clip rectangle.</param>
    public void Advance(EntityList<StripEffect> explosions, StripClip clip)
    {
        if (_transporter is not null)
        {
            AdvanceTransport(_transporter);
        }

        if (_pending.Count > 0 && explosions.Count < StripExplosionTuning.MaxConcurrent)
        {
            StartNextAppear(explosions, clip);
        }

        RetireConverged();
    }

    /// <summary>Draws the robots being beamed in, when this is a brain wave and the beaming is under way.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set.</param>
    public void DrawTransport(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (_transportBegun && _transporter is { IsFinished: false })
        {
            _transporter.Draw(spriteBatch, sprites);
        }
    }

    /// <summary>Says whether a robot is still appearing. It does not act and is not drawn, because its appear effect draws it.</summary>
    /// <param name="entity">The entity to test.</param>
    public bool IsAssembling(IEntity entity) => _assembling.ContainsKey(entity);

    /// <summary>Adds a robot to the sequence. It counts as appearing from now, because the arcade keeps every robot off until the whole sequence is done.</summary>
    /// <param name="robot">The robot to bring in.</param>
    public void Queue(IEntity robot)
    {
        _assembling[robot] = null;
        if (_transporter is not null && !_transportBegun)
        {
            _transportQueue.Add(robot);
            return;
        }

        _pending.Enqueue(robot);
    }

    /// <summary>Starts the beaming in once the robots are all queued, moves it on, and lets the robots go when it has finished.</summary>
    /// <param name="transporter">The transporter that does the beaming in.</param>
    private void AdvanceTransport(RobotTransporter transporter)
    {
        if (!_transportBegun && _transportQueue.Count > 0)
        {
            transporter.Begin(_transportQueue);
            _transported.AddRange(_transportQueue);
            _transportQueue.Clear();
            _transportBegun = true;
        }

        if (!_transportBegun)
        {
            return;
        }

        transporter.Update();
        if (transporter.IsFinished)
        {
            foreach (IEntity robot in _transported)
            {
                _assembling.Remove(robot);
            }

            _transported.Clear();
        }
    }

    /// <summary>Lets go of the robots whose appear effect has finished, so they start acting.</summary>
    private void RetireConverged()
    {
        List<IEntity> converged = [.. _assembling
            .Where(pair => pair.Value is { } effect && !effect.IsAlive())
            .Select(pair => pair.Key)];

        foreach (IEntity robot in converged)
        {
            _assembling.Remove(robot);
        }
    }

    /// <summary>Gives the next robot in the queue its appear effect.</summary>
    /// <param name="explosions">The list of strip effects that the new one joins.</param>
    /// <param name="clip">The edges the strips are cut off at.</param>
    private void StartNextAppear(EntityList<StripEffect> explosions, StripClip clip)
    {
        IEntity robot = _pending.Dequeue();

        // The ROM does not set A before the APST call in this loop, so the row fan's slope is taken as 0 (no lean)
        // rather than guessed.
        StripFanAxis axis = (_sequenceNumber & ColumnFanSequenceMask) == ColumnFanSequenceMask
            ? StripFanAxis.Columns
            : StripFanAxis.Rows;
        _sequenceNumber++;

        if (robot is IAnimationFrameSource frameSource)
        {
            StripEffect appear = StripEffect.CreateAppear(frameSource, robot.Bounds, axis, slope: 0, clip);
            explosions.Add(appear);
            _assembling[robot] = appear;
        }
    }
}
