using Robotron2084.Audio;

namespace Robotron2084.Level.Collisions;

/// <summary>Works out what the field does about each thing a collision rule reports.</summary>
/// <remarks>
///     The rules only say what touched what. What that means for the game is decided here, on the field's behalf, so it
///     can
///     change without a rule changing. Each response is the arcade's own; the original source and the disassembly are
///     named on it.
/// </remarks>
internal sealed class CollisionResponder
{
    private readonly PlayField _field;
    private readonly Dictionary<Type, Action<CollisionResult>> _responses;

    /// <summary>Makes a responder for one field.</summary>
    /// <param name="field">The field that responds.</param>
    public CollisionResponder(PlayField field)
    {
        _field = field;
        _responses = new Dictionary<Type, Action<CollisionResult>>
        {
            [typeof(LaserHitResult)] = result => RespondToLaserHit((LaserHitResult)result),
            [typeof(GruntHitElectrodeResult)] =
                result => RespondToGruntHittingElectrode((GruntHitElectrodeResult)result),
            [typeof(BerzerkRobotHitElectrodeResult)] = result =>
                RespondToBerzerkRobotHittingElectrode((BerzerkRobotHitElectrodeResult)result),
            [typeof(HulkHitElectrodeResult)] = result => RespondToHulkHittingElectrode((HulkHitElectrodeResult)result),
            [typeof(PlayerHitElectrodeResult)] =
                result => RespondToPlayerHittingElectrode((PlayerHitElectrodeResult)result),
            [typeof(PlayerTouchedDeadThingResult)] = _ => _field.KillPlayer(),
            [typeof(BrainLostVictimResult)] = result => RespondToBrainLosingVictim((BrainLostVictimResult)result),
            [typeof(BrainCaughtHumanResult)] = result => RespondToBrainCatchingHuman((BrainCaughtHumanResult)result),
            [typeof(HulkKilledHumanResult)] = result => RespondToHulkKillingHuman((HulkKilledHumanResult)result),
            [typeof(PlayerRescuedHumanResult)] =
                result => RespondToPlayerRescuingHuman((PlayerRescuedHumanResult)result)
        };
    }

    /// <summary>Does what the field does when a collision rule reports a result.</summary>
    /// <param name="result">What the rule found.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     The result is one the field has no response for. A new result needs a
    ///     response in the constructor.
    /// </exception>
    public void Respond(CollisionResult result)
    {
        if (!_responses.TryGetValue(result.GetType(), out var respond))
            throw new ArgumentOutOfRangeException(nameof(result), result, "The field has no response for this result.");

        respond(result);
    }

    /// <summary>The player and the electrode both die.</summary>
    /// <param name="hit">The hit.</param>
    private void RespondToPlayerHittingElectrode(PlayerHitElectrodeResult hit)
    {
        _field.KillPlayer();
        hit.Electrode.Kill();
    }

    /// <summary>The brain starts reprogramming the human it caught.</summary>
    /// <param name="caught">The catch.</param>
    private void RespondToBrainCatchingHuman(BrainCaughtHumanResult caught)
    {
        caught.Brain.BeginReprogramming(caught.Human, _field.GetPlayfieldBounds());
    }

    /// <summary>
    ///     A laser is used up by the robot it hits: the robot's kind says what happens to it and what sound it makes, and
    ///     the kill is scored.
    /// </summary>
    /// <param name="hit">The hit.</param>
    private void RespondToLaserHit(LaserHitResult hit)
    {
        var hitBounds = hit.Target.GetBounds();
        hit.KindInfo.LaserHit(_field, hit.Target, hit.Laser.Direction);
        _field.PlaySoundFrom(hit.KindInfo.LaserHitSound, hitBounds);
        _field.AwardScore(hit.KindInfo.Score);
        hit.Laser.Kill();
    }

    /// <summary>
    ///     The grunt and the electrode both die, the surviving grunts speed up, and the electrode's sound is kept over
    ///     the grunt's.
    /// </summary>
    /// <param name="hit">The hit.</param>
    /// <remarks>
    ///     Original source: <c>RRP8.ASM</c> <c>PSTKIL</c>, which asks for <c>PSKSND</c>, then <c>ROBKIL</c> for
    ///     <c>RBSND</c> in the same frame; the voice keeps the first.
    /// </remarks>
    private void RespondToGruntHittingElectrode(GruntHitElectrodeResult hit)
    {
        hit.Grunt.Kill();
        _field.SpawnExplosion(hit.Grunt, null);
        hit.Electrode.Kill();
        _field.SpeedUpGrunts();
        _field.PlaySoundFrom(SoundTables.PostKill, hit.Electrode.GetBounds());
        _field.PlaySoundFrom(SoundTables.RobotHit, hit.Grunt.GetBounds());
    }

    /// <summary>The BerzerkRobot and the electrode both die, as a grunt's do, but the other robots do not speed up.</summary>
    /// <param name="hit">The hit.</param>
    private void RespondToBerzerkRobotHittingElectrode(BerzerkRobotHitElectrodeResult hit)
    {
        hit.Robot.Kill();
        _field.SpawnExplosion(hit.Robot, null);
        hit.Electrode.Kill();
        _field.PlaySoundFrom(SoundTables.PostKill, hit.Electrode.GetBounds());
        _field.PlaySoundFrom(SoundTables.RobotHit, hit.Robot.GetBounds());
    }

    /// <summary>The electrode is destroyed. The hulk is not harmed.</summary>
    /// <param name="hit">The hit.</param>
    private void RespondToHulkHittingElectrode(HulkHitElectrodeResult hit)
    {
        hit.Electrode.Kill();
        _field.PlaySoundFrom(SoundTables.PostKill, hit.Electrode.GetBounds());
    }

    /// <summary>The conversion never finishes: the human is lost, no prog appears, and a skull is left where they stood.</summary>
    /// <param name="lost">The dead brain.</param>
    private void RespondToBrainLosingVictim(BrainLostVictimResult lost)
    {
        if (lost.Brain.ReleaseVictim() is { } released) _field.LeaveSkull(released.Position);
    }

    /// <summary>The human dies at once and leaves a skull.</summary>
    /// <param name="killed">The kill.</param>
    private void RespondToHulkKillingHuman(HulkKilledHumanResult killed)
    {
        killed.Human.Kill();
        _field.LeaveSkull(killed.Human.Position);
        _field.PlaySoundFrom(SoundTables.KillAHuman, killed.Human.GetBounds());
    }

    /// <summary>The human is saved, the rescue is counted and shown, and the bonus is scored.</summary>
    /// <param name="rescued">The rescue.</param>
    private void RespondToPlayerRescuingHuman(PlayerRescuedHumanResult rescued)
    {
        rescued.Human.Rescue();
        var rescues = _field.CountRescuedFamilyMembers();
        _field.ShowRescueScore(rescued.Human.Position);
        _field.PlaySoundFrom(SoundTables.SaveAHuman, rescued.Human.GetBounds());
        _field.AwardRescueBonus(rescues);
    }
}
