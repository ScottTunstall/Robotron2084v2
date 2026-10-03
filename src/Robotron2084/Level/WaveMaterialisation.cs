using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// RRG23's <c>APPEAR</c>: the wave's robots MATERIALISE. The ROM walks the robot list and creates ONE appear record
/// per frame (<c>LDA #1 / PSHS A ... NAP 1,APL</c>), every fourth one with the HORIZONTAL (column) fan
/// (<c>LDA PD,U / ANDA #3 / CMPA #3 / BNE AP1 / JSR HAPST</c>), while the robots themselves are OFF
/// (<c>ROBOFF</c>) until the sequence finishes (notes §61.4).
/// </summary>
public sealed class WaveMaterialisation
{
    /// <summary>Every fourth robot of the sequence (<c>ANDA #3 / CMPA #3</c>) uses the column fan.</summary>
    private const int ColumnFanSequenceMask = 3;

    private readonly Dictionary<IEntity, StripEffect?> _assembling = [];
    private readonly Queue<IEntity> _pending = new();
    private int _sequenceNumber;

    /// <summary>Robots still waiting for their appear record.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// One frame of the sequence: creates ONE appear record for the next queued robot (its strips converge onto
    /// the robot's centre — the ROM's <c>APCENT</c>), then retires the robots whose record has finished
    /// converging. The record pool is the shared ten, so a full pool simply delays the next one.
    /// </summary>
    /// <param name="explosions">The field's strip records, which the appear records share.</param>
    /// <param name="clip">The strip engine's clip rectangle.</param>
    public void Advance(EntityList<StripEffect> explosions, StripClip clip)
    {
        if (_pending.Count > 0 && explosions.Count < StripExplosionTuning.MaxConcurrent)
        {
            StartNextAppear(explosions, clip);
        }

        RetireConverged();
    }

    /// <summary>
    /// True while this entity is still assembling: it does not act and is NOT drawn — its appear records are
    /// drawing it.
    /// </summary>
    /// <param name="entity">The entity to test.</param>
    public bool IsAssembling(IEntity entity) => _assembling.ContainsKey(entity);

    /// <summary>
    /// Queues a wave-start robot. It counts as assembling from THIS moment — the ROM holds the robots OFF for the
    /// whole sequence, not just once their own record starts.
    /// </summary>
    /// <param name="robot">The robot to bring in.</param>
    public void Queue(IEntity robot)
    {
        _pending.Enqueue(robot);
        _assembling[robot] = null;
    }

    private void RetireConverged()
    {
        List<IEntity> converged = [.. _assembling
            .Where(pair => pair.Value is { LifeState: not EntityLifeState.Alive })
            .Select(pair => pair.Key)];

        foreach (IEntity robot in converged)
        {
            _assembling.Remove(robot);
        }
    }

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
