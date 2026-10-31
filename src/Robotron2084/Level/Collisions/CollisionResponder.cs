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
    private readonly PlayField _playField;
    private readonly Dictionary<Type, Action<CollisionResult>> _responses;

    /// <summary>Makes a responder for one field.</summary>
    /// <param name="playField">The field that responds.</param>
    public CollisionResponder(PlayField playField)
    {
        _playField = playField;
        _responses = new Dictionary<Type, Action<CollisionResult>>
        {
            [typeof(LaserHitResult)] = collisionResult => RespondToLaserHit((LaserHitResult)collisionResult),
            [typeof(GruntHitElectrodeResult)] = collisionResult => RespondToGruntHittingElectrode((GruntHitElectrodeResult)collisionResult),
            [typeof(BerzerkRobotHitElectrodeResult)] = collisionResult => RespondToBerzerkRobotHittingElectrode((BerzerkRobotHitElectrodeResult)collisionResult),
            [typeof(HulkHitElectrodeResult)] = collisionResult => RespondToHulkHittingElectrode((HulkHitElectrodeResult)collisionResult),
            [typeof(PlayerHitElectrodeResult)] = collisionResult => RespondToPlayerHittingElectrode((PlayerHitElectrodeResult)collisionResult),
            [typeof(PlayerTouchedDeadThingResult)] = _ => _playField.KillPlayer(),
            [typeof(BrainLostVictimResult)] = collisionResult => RespondToBrainLosingVictim((BrainLostVictimResult)collisionResult),
            [typeof(BrainCaughtFamilyMemberResult)] = collisionResult => RespondToBrainCatchingFamilyMember((BrainCaughtFamilyMemberResult)collisionResult),
            [typeof(HulkKilledFamilyMemberResult)] = collisionResult => RespondToHulkKillingFamilyMember((HulkKilledFamilyMemberResult)collisionResult),
            [typeof(PlayerRescuedFamilyMemberResult)] = collisionResult => RespondToPlayerRescuingFamilyMember((PlayerRescuedFamilyMemberResult)collisionResult)
        };
    }

    /// <summary>Does what the field does when a collision rule reports a result.</summary>
    /// <param name="collisionResult">What the rule found.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     The result is one the field has no response for. A new result needs a
    ///     response in the constructor.
    /// </exception>
    public void Respond(CollisionResult collisionResult)
    {
        if (!_responses.TryGetValue(collisionResult.GetType(), out var respond))
            throw new ArgumentOutOfRangeException(nameof(collisionResult), collisionResult, "The field has no response for this result.");

        respond(collisionResult);
    }

    /// <summary>The BerzerkRobot and the electrode both die, as a grunt's do, but the other robots do not speed up.</summary>
    /// <param name="berzerkRobotHitElectrodeResult">The hit.</param>
    private void RespondToBerzerkRobotHittingElectrode(BerzerkRobotHitElectrodeResult berzerkRobotHitElectrodeResult)
    {
        berzerkRobotHitElectrodeResult.BerzerkRobot.Kill();
        _playField.SpawnExplosion(berzerkRobotHitElectrodeResult.BerzerkRobot, null);
        berzerkRobotHitElectrodeResult.Electrode.Kill();
        _playField.PlaySoundFrom(SoundTables.PostKill, berzerkRobotHitElectrodeResult.Electrode.GetBounds());
        _playField.PlaySoundFrom(SoundTables.RobotHit, berzerkRobotHitElectrodeResult.BerzerkRobot.GetBounds());
    }

    /// <summary>The brain starts reprogramming the family member it caught.</summary>
    /// <param name="brainCaughtFamilyMemberResult">The catch.</param>
    private void RespondToBrainCatchingFamilyMember(BrainCaughtFamilyMemberResult brainCaughtFamilyMemberResult)
    {
        brainCaughtFamilyMemberResult.Brain.BeginReprogramming(brainCaughtFamilyMemberResult.FamilyMember, _playField.GetPlayfieldBounds());
    }

    /// <summary>The conversion never finishes: the family member is lost, no prog appears, and a skull is left where they stood.</summary>
    /// <param name="brainLostVictimResult">The dead brain.</param>
    private void RespondToBrainLosingVictim(BrainLostVictimResult brainLostVictimResult)
    {
        var victim = brainLostVictimResult.Brain.GetVictim();
        brainLostVictimResult.Brain.ReleaseVictim();
        if (victim is not null) _playField.LeaveSkull(victim.Position);
    }

    /// <summary>
    ///     The grunt and the electrode both die, the surviving grunts speed up, and the electrode's sound is kept over
    ///     the grunt's.
    /// </summary>
    /// <param name="gruntHitElectrodeResult">The hit.</param>
    /// <remarks>
    ///     Original source: <c>RRP8.ASM</c> <c>PSTKIL</c>, which asks for <c>PSKSND</c>, then <c>ROBKIL</c> for
    ///     <c>RBSND</c> in the same frame; the voice keeps the first.
    /// </remarks>
    private void RespondToGruntHittingElectrode(GruntHitElectrodeResult gruntHitElectrodeResult)
    {
        gruntHitElectrodeResult.Grunt.Kill();
        _playField.SpawnExplosion(gruntHitElectrodeResult.Grunt, null);
        gruntHitElectrodeResult.Electrode.Kill();
        _playField.SpeedUpGrunts();
        _playField.PlaySoundFrom(SoundTables.PostKill, gruntHitElectrodeResult.Electrode.GetBounds());
        _playField.PlaySoundFrom(SoundTables.RobotHit, gruntHitElectrodeResult.Grunt.GetBounds());
    }

    /// <summary>The electrode is destroyed. The hulk is not harmed.</summary>
    /// <param name="hulkHitElectrodeResult">The hit.</param>
    private void RespondToHulkHittingElectrode(HulkHitElectrodeResult hulkHitElectrodeResult)
    {
        hulkHitElectrodeResult.Electrode.Kill();
        _playField.PlaySoundFrom(SoundTables.PostKill, hulkHitElectrodeResult.Electrode.GetBounds());
    }

    /// <summary>The family member dies at once and leaves a skull.</summary>
    /// <param name="hulkKilledFamilyMemberResult">The kill.</param>
    private void RespondToHulkKillingFamilyMember(HulkKilledFamilyMemberResult hulkKilledFamilyMemberResult)
    {
        hulkKilledFamilyMemberResult.FamilyMember.Kill();
        _playField.LeaveSkull(hulkKilledFamilyMemberResult.FamilyMember.Position);
        _playField.PlaySoundFrom(SoundTables.KillAHuman, hulkKilledFamilyMemberResult.FamilyMember.GetBounds());
    }

    /// <summary>
    ///     A laser is used up by the robot it hits: the robot's kind says what happens to it and what sound it makes, and
    ///     the kill is scored.
    /// </summary>
    /// <param name="laserHitResult">The hit.</param>
    /// <remarks>
    ///     Original source: <c>RRS22.ASM</c> <c>KILROB</c>/<c>KILLOF</c> only free the object. The explosion, the burst
    ///     and the score are made by the killer's own calls, as here (notes §61, §64).
    /// </remarks>
    private void RespondToLaserHit(LaserHitResult laserHitResult)
    {
        var hitBounds = laserHitResult.TargetEntity.GetBounds();
        laserHitResult.RobotKindInfo.LaserHit(_playField, laserHitResult.TargetEntity, laserHitResult.PlayerLaser.Direction);
        _playField.PlaySoundFrom(laserHitResult.RobotKindInfo.LaserHitSound, hitBounds);
        _playField.AwardScore(laserHitResult.RobotKindInfo.Score);
        laserHitResult.PlayerLaser.Kill();
    }

    /// <summary>The player and the electrode both die.</summary>
    /// <param name="playerHitElectrodeResult">The hit.</param>
    private void RespondToPlayerHittingElectrode(PlayerHitElectrodeResult playerHitElectrodeResult)
    {
        _playField.KillPlayer();
        playerHitElectrodeResult.Electrode.Kill();
    }

    /// <summary>The family member is saved, the rescue is counted and shown, and the bonus is scored.</summary>
    /// <param name="playerRescuedFamilyMemberResult">The rescue.</param>
    private void RespondToPlayerRescuingFamilyMember(PlayerRescuedFamilyMemberResult playerRescuedFamilyMemberResult)
    {
        playerRescuedFamilyMemberResult.FamilyMember.Rescue();
        var rescues = _playField.CountRescuedFamilyMembers();
        _playField.ShowRescueBonus(playerRescuedFamilyMemberResult.FamilyMember.Position);
        _playField.PlaySoundFrom(SoundTables.SaveAHuman, playerRescuedFamilyMemberResult.FamilyMember.GetBounds());
        _playField.AwardRescueBonus(rescues);
    }
}
