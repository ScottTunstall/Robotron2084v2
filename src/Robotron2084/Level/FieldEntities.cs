using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
///     Everything on the field apart from the player: one list for each kind of thing, and the orders they are moved
///     and drawn in.
/// </summary>
/// <remarks>
///     The arcade keeps a separate linked list for each kind of object and walks them in a fixed order. A new kind joins
///     the field's passes by having a list here and a place in the three orders.
/// </remarks>
public sealed class FieldEntities
{
    private readonly IEntityList[] _drawOrderBehindShots;
    private readonly IEntityList[] _drawOrderInFrontOfShots;
    private readonly IEntityList[] _updateOrder;

    /// <summary>Makes the empty lists and sets the orders they are walked in.</summary>
    public FieldEntities()
    {
        _updateOrder =
        [
            Electrodes, Grunts, BerzerkRobots, Gorfs, Hulks, Spheroids, Enforcers, Quarks, Tanks, Brains, Progs,
            Sparks, TankShells, CruiseMissiles, Family.Members, Skulls, RescueScores, Explosions, ScoreBursts
        ];
        _drawOrderBehindShots =
        [
            Electrodes, Skulls, RescueScores, Family.Members, Grunts, BerzerkRobots, Gorfs, Hulks, Spheroids, Enforcers,
            Quarks, Tanks, Brains, Progs
        ];
        _drawOrderInFrontOfShots =
        [
            Sparks, TankShells, CruiseMissiles, Explosions, ScoreBursts
        ];
    }

    /// <summary>The BerzerkRobots.</summary>
    public EntityList<BerzerkRobot> BerzerkRobots { get; } = new();

    /// <summary>The brains.</summary>
    public EntityList<Brain> Brains { get; } = new();

    /// <summary>The cruise missiles the brains have fired.</summary>
    public EntityList<CruiseMissile> CruiseMissiles { get; } = new();

    /// <summary>
    ///     The lists drawn behind the player's lasers, in order: the electrodes, the family and their markers, then the
    ///     robots.
    /// </summary>
    public IReadOnlyList<IEntityList> DrawOrderBehindShots => _drawOrderBehindShots;

    /// <summary>The lists drawn in front of the player's lasers, in order: the enemy shots, the explosions, then the bursts.</summary>
    public IReadOnlyList<IEntityList> DrawOrderInFrontOfShots => _drawOrderInFrontOfShots;

    /// <summary>The electrodes.</summary>
    public EntityList<Electrode> Electrodes { get; } = new();

    /// <summary>The enforcers the spheroids have dropped.</summary>
    public EntityList<Enforcer> Enforcers { get; } = new();

    /// <summary>The strip explosions and the wave-start appear effects, which share one pool in the arcade.</summary>
    public EntityList<StripEffect> Explosions { get; } = new();

    /// <summary>The family: who is on the field and the places the brains and hulks pick them by.</summary>
    public FamilyList Family { get; } = new();

    /// <summary>The Gorfs.</summary>
    public EntityList<Gorf> Gorfs { get; } = new();

    /// <summary>The grunts.</summary>
    public EntityList<Grunt> Grunts { get; } = new();

    /// <summary>The hulks.</summary>
    public EntityList<Hulk> Hulks { get; } = new();

    /// <summary>The progs: humans the brains have reprogrammed.</summary>
    public EntityList<Prog> Progs { get; } = new();

    /// <summary>The quarks.</summary>
    public EntityList<Quark> Quarks { get; } = new();

    /// <summary>The bonus scores shown where humans were rescued.</summary>
    public EntityList<RescueScoreMarker> RescueScores { get; } = new();

    /// <summary>The bursts a spheroid or a quark leaves when it dies, in place of a strip explosion.</summary>
    /// <remarks>
    ///     Original source: <c>CIRKP</c> and <c>CIRKV</c>. They take a free object, not one of the ten shared explosion
    ///     records, so there is no limit on them (notes §64).
    /// </remarks>
    public EntityList<ScoreBurst> ScoreBursts { get; } = new();

    /// <summary>The skulls left where humans were killed.</summary>
    public EntityList<SkullMarker> Skulls { get; } = new();

    /// <summary>The sparks the enforcers have fired.</summary>
    public EntityList<Spark> Sparks { get; } = new();

    /// <summary>The spheroids.</summary>
    public EntityList<Spheroid> Spheroids { get; } = new();

    /// <summary>The tanks the quarks have dropped.</summary>
    public EntityList<Tank> Tanks { get; } = new();

    /// <summary>The shells the tanks have fired.</summary>
    public EntityList<TankShell> TankShells { get; } = new();

    /// <summary>Every list the field moves on and clears of the dead, in the order the arcade does them.</summary>
    public IReadOnlyList<IEntityList> UpdateOrder => _updateOrder;

    /// <summary>Says whether every enemy that must be cleared to finish the wave is gone.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Hulks cannot be killed and electrodes are obstacles, so neither counts. Nor do progs and
    ///             cruise missiles: the arcade only counts the grunts, spheroids, enforcers, brains, tanks and
    ///             quarks, and clears away anything else when the wave ends. Original source: <c>RRG23.ASM</c>
    ///             <c>WVCHEK</c>.
    ///         </item>
    ///         <item>Disassembly: <c>COUNT_ENEMIES_ON_SCREEN</c> (<c>$2A73</c>).</item>
    ///     </list>
    /// </remarks>
    public bool AreEnemiesGone()
    {
        return Grunts.GetLiveCount() == 0 && BerzerkRobots.GetLiveCount() == 0 && Gorfs.GetLiveCount() == 0 &&
               Spheroids.GetLiveCount() == 0 && Enforcers.GetLiveCount() == 0 && Quarks.GetLiveCount() == 0
               && Tanks.GetLiveCount() == 0 && Brains.GetLiveCount() == 0;
    }

    /// <summary>Draws the lists that go behind the player's lasers.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="field">The field, which decides what may be drawn yet.</param>
    public void DrawBehindShots(SpriteBatch spriteBatch, PlayField field)
    {
        DrawAll(_drawOrderBehindShots, spriteBatch, field);
    }

    /// <summary>Draws the lists that go in front of the player's lasers.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="field">The field, which decides what may be drawn yet.</param>
    public void DrawInFrontOfShots(SpriteBatch spriteBatch, PlayField field)
    {
        DrawAll(_drawOrderInFrontOfShots, spriteBatch, field);
    }

    /// <summary>Finds the list a kind of robot lives in. This is the one link from the kinds to the lists.</summary>
    /// <param name="kind">The kind to look up.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no list. A new kind needs one here.</exception>
    public IEntityList GetList(RobotKind kind)
    {
        return kind switch
        {
            RobotKind.Electrode => Electrodes,
            RobotKind.Grunt => Grunts,
            RobotKind.BerzerkRobot => BerzerkRobots,
            RobotKind.Gorf => Gorfs,
            RobotKind.Hulk => Hulks,
            RobotKind.Spheroid => Spheroids,
            RobotKind.Enforcer => Enforcers,
            RobotKind.Quark => Quarks,
            RobotKind.Tank => Tanks,
            RobotKind.Brain => Brains,
            RobotKind.Prog => Progs,
            RobotKind.Spark => Sparks,
            RobotKind.TankShell => TankShells,
            RobotKind.CruiseMissile => CruiseMissiles,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No list holds this robot kind.")
        };
    }

    /// <summary>Finds where the nearest living robot to a point is, for the attract demo's player to steer by.</summary>
    /// <param name="from">The point to measure from.</param>
    /// <returns>The robot's position, or null when there is none.</returns>
    /// <remarks>
    ///     Only the kinds the demo's player chases are looked at (<see cref="RobotKindInfo.IsChasedByDemoPlayer" />).
    ///     The distance is <see cref="IntVector2.GetManhattanDistance" /> (notes §94).
    /// </remarks>
    public IntVector2? GetNearestLivingRobotPosition(IntVector2 from)
    {
        var living = RobotKinds.All
            .Where(kind => kind.IsChasedByDemoPlayer)
            .SelectMany(kind => GetList(kind.Kind).Entities)
            .Where(entity => entity.IsAlive());

        IntVector2? nearest = null;
        var nearestDistance = int.MaxValue;
        foreach (var entity in living)
        {
            var distance = entity.Position.GetManhattanDistance(from);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entity.Position;
            }
        }

        return nearest;
    }

    /// <summary>Puts a Brain on the field.</summary>
    /// <param name="entity">The Brain to add.</param>
    public void Add(Brain entity)
    {
        Brains.Add(entity);
    }

    /// <summary>Puts a BerzerkRobot on the field.</summary>
    /// <param name="entity">The BerzerkRobot to add.</param>
    public void Add(BerzerkRobot entity)
    {
        BerzerkRobots.Add(entity);
    }

    /// <summary>Puts a CruiseMissile on the field.</summary>
    /// <param name="entity">The CruiseMissile to add.</param>
    public void Add(CruiseMissile entity)
    {
        CruiseMissiles.Add(entity);
    }

    /// <summary>Puts an Electrode on the field.</summary>
    /// <param name="entity">The Electrode to add.</param>
    public void Add(Electrode entity)
    {
        Electrodes.Add(entity);
    }

    /// <summary>Puts an Enforcer on the field.</summary>
    /// <param name="entity">The Enforcer to add.</param>
    public void Add(Enforcer entity)
    {
        Enforcers.Add(entity);
    }

    /// <summary>Puts a Gorf on the field.</summary>
    /// <param name="entity">The Gorf to add.</param>
    public void Add(Gorf entity)
    {
        Gorfs.Add(entity);
    }

    /// <summary>Puts a Grunt on the field.</summary>
    /// <param name="entity">The Grunt to add.</param>
    public void Add(Grunt entity)
    {
        Grunts.Add(entity);
    }

    /// <summary>Puts a Hulk on the field.</summary>
    /// <param name="entity">The Hulk to add.</param>
    public void Add(Hulk entity)
    {
        Hulks.Add(entity);
    }

    /// <summary>Puts a Prog on the field.</summary>
    /// <param name="entity">The Prog to add.</param>
    public void Add(Prog entity)
    {
        Progs.Add(entity);
    }

    /// <summary>Puts a Quark on the field.</summary>
    /// <param name="entity">The Quark to add.</param>
    public void Add(Quark entity)
    {
        Quarks.Add(entity);
    }

    /// <summary>Puts a Spark on the field.</summary>
    /// <param name="entity">The Spark to add.</param>
    public void Add(Spark entity)
    {
        Sparks.Add(entity);
    }

    /// <summary>Puts a Spheroid on the field.</summary>
    /// <param name="entity">The Spheroid to add.</param>
    public void Add(Spheroid entity)
    {
        Spheroids.Add(entity);
    }

    /// <summary>Puts a Tank on the field.</summary>
    /// <param name="entity">The Tank to add.</param>
    public void Add(Tank entity)
    {
        Tanks.Add(entity);
    }

    /// <summary>Puts a TankShell on the field.</summary>
    /// <param name="entity">The TankShell to add.</param>
    public void Add(TankShell entity)
    {
        TankShells.Add(entity);
    }

    /// <summary>Puts a SkullMarker on the field.</summary>
    /// <param name="entity">The SkullMarker to add.</param>
    public void Add(SkullMarker entity)
    {
        Skulls.Add(entity);
    }

    /// <summary>Puts a RescueScoreMarker on the field.</summary>
    /// <param name="entity">The RescueScoreMarker to add.</param>
    public void Add(RescueScoreMarker entity)
    {
        RescueScores.Add(entity);
    }

    /// <summary>Puts a ScoreBurst on the field.</summary>
    /// <param name="entity">The ScoreBurst to add.</param>
    public void Add(ScoreBurst entity)
    {
        ScoreBursts.Add(entity);
    }

    /// <summary>Puts a StripEffect on the field.</summary>
    /// <param name="entity">The StripEffect to add.</param>
    public void Add(StripEffect entity)
    {
        Explosions.Add(entity);
    }

    /// <summary>Puts a family member on the field, in the next free place in the family list.</summary>
    /// <param name="human">The family member to add.</param>
    public void Add(Human human)
    {
        Family.Add(human);
    }

    /// <summary>Says whether any family member is standing on the field and free.</summary>
    public bool AnyFamilyMemberAvailable()
    {
        return Family.AnyAvailable();
    }

    /// <summary>Counts the cruise missiles in flight.</summary>
    public int GetCruiseMissileCount()
    {
        return CruiseMissiles.GetLiveCount();
    }

    /// <summary>Counts the enforcers on the field.</summary>
    public int GetEnforcerCount()
    {
        return Enforcers.GetLiveCount();
    }

    /// <summary>Finds every entity of one kind of robot.</summary>
    /// <param name="kind">The kind to look for.</param>
    public IEnumerable<IEntity> GetEntities(RobotKind kind)
    {
        return GetList(kind).Entities;
    }

    /// <summary>Finds the family member in a place in the family list, if they are standing and free.</summary>
    /// <param name="slot">The place to look in.</param>
    public Human? GetFamilyMemberInSlot(int slot)
    {
        return Family.GetMemberInSlot(slot);
    }

    /// <summary>Lists every family member who has been put on the field.</summary>
    public IReadOnlyList<Human> GetFamilyMembers()
    {
        return Family.Members;
    }

    /// <summary>
    ///     Lists the places in the family list that still hold a family member who is standing on the field and free,
    ///     lowest place first.
    /// </summary>
    public IReadOnlyList<int> GetOccupiedFamilySlots()
    {
        return Family.GetOccupiedSlots();
    }

    /// <summary>Finds the family list place of the member nearest a point.</summary>
    /// <param name="from">The point to measure from.</param>
    public int GetNearestFamilySlot(IntVector2 from)
    {
        return Family.GetNearestSlot(from);
    }

    /// <summary>Counts the sparks in flight.</summary>
    public int GetSparkCount()
    {
        return Sparks.Count(spark => spark.IsAlive());
    }

    /// <summary>Counts the tanks on the field.</summary>
    public int GetTankCount()
    {
        return Tanks.GetLiveCount();
    }

    /// <summary>Says whether any grunt took a step in the last tick.</summary>
    public bool HasGruntStepped()
    {
        return Grunts.Any(grunt => grunt.SteppedThisUpdate);
    }

    /// <summary>Says whether no electrode touches a box, so that something can be put there.</summary>
    /// <param name="box">The box to test, in port pixels.</param>
    public bool IsClearOfElectrodes(Rectangle box)
    {
        return Electrodes.All(electrode => !electrode.GetBounds().Intersects(box));
    }

    /// <summary>Says whether one of the arcade's strip routines has a record free for another explosion or appear effect.</summary>
    /// <param name="engine">The strip routine that would run the effect.</param>
    /// <returns>True when the routine can run one more effect.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>GETBLK</c> and <c>GETAP</c> in <c>RRX7.ASM</c>, <c>RRHX4.ASM</c> and
    ///             <c>RRDX2.ASM</c>, each with its own free list.
    ///         </item>
    ///         <item>Disassembly: <c>$5B6C</c>, <c>$F03A</c> and the diagonal routine's at <c>$46B2</c> onwards.</item>
    ///     </list>
    /// </remarks>
    public bool HasRoomForStripEffect(StripEngine engine)
    {
        return StripExplosionTuning.GetPoolSize(engine) is not { } poolSize ||
               Explosions.Count(effect => effect.GetEngine() == engine) < poolSize;
    }

    /// <summary>Takes the dead out of every list.</summary>
    public void PruneDead()
    {
        foreach (var list in _updateOrder) list.PruneDead();
    }

    /// <summary>Moves every list on by one tick, in the order the arcade does them.</summary>
    /// <param name="gameTime">The time for this tick.</param>
    /// <param name="field">The field the entities are on.</param>
    public void UpdateAll(GameTime gameTime, PlayField field)
    {
        foreach (var list in _updateOrder) list.UpdateAll(gameTime, field);
    }

    /// <summary>Draws some lists in order.</summary>
    /// <param name="lists">The lists to draw.</param>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="field">The field, which decides what may be drawn yet.</param>
    private static void DrawAll(IEntityList[] lists, SpriteBatch spriteBatch, PlayField field)
    {
        foreach (var list in lists) list.DrawAll(spriteBatch, field);
    }
}
