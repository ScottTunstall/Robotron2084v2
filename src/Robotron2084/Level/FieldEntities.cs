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
            Sparks, TankShells, CruiseMissiles, FamilyList.Members, Skulls, RescueBonusMarkers, Explosions, ScoreBursts
        ];
        _drawOrderBehindShots =
        [
            Electrodes, Skulls, RescueBonusMarkers, FamilyList.Members, Grunts, BerzerkRobots, Gorfs, Hulks, Spheroids, Enforcers,
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
    public FamilyList FamilyList { get; } = new();

    /// <summary>The Gorfs.</summary>
    public EntityList<Gorf> Gorfs { get; } = new();

    /// <summary>The grunts.</summary>
    public EntityList<Grunt> Grunts { get; } = new();

    /// <summary>The hulks.</summary>
    public EntityList<Hulk> Hulks { get; } = new();

    /// <summary>The progs: family members the brains have reprogrammed.</summary>
    public EntityList<Prog> Progs { get; } = new();

    /// <summary>The quarks.</summary>
    public EntityList<Quark> Quarks { get; } = new();

    /// <summary>The bonus scores shown where family members were rescued.</summary>
    public EntityList<RescueBonusMarker> RescueBonusMarkers { get; } = new();

    /// <summary>The bursts a spheroid or a quark leaves when it dies, in place of a strip explosion.</summary>
    /// <remarks>
    ///     Original source: <c>CIRKP</c> and <c>CIRKV</c>. They take a free object, not one of the ten shared explosion
    ///     records, so there is no limit on them (notes §64).
    /// </remarks>
    public EntityList<ScoreBurst> ScoreBursts { get; } = new();

    /// <summary>The skulls left where family members were killed.</summary>
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

    /// <summary>Puts a Brain on the field.</summary>
    /// <param name="brain">The Brain to add.</param>
    public void Add(Brain brain)
    {
        Brains.Add(brain);
    }

    /// <summary>Puts a BerzerkRobot on the field.</summary>
    /// <param name="berzerkRobot">The BerzerkRobot to add.</param>
    public void Add(BerzerkRobot berzerkRobot)
    {
        BerzerkRobots.Add(berzerkRobot);
    }

    /// <summary>Puts a CruiseMissile on the field.</summary>
    /// <param name="cruiseMissile">The CruiseMissile to add.</param>
    public void Add(CruiseMissile cruiseMissile)
    {
        CruiseMissiles.Add(cruiseMissile);
    }

    /// <summary>Puts an Electrode on the field.</summary>
    /// <param name="electrode">The Electrode to add.</param>
    public void Add(Electrode electrode)
    {
        Electrodes.Add(electrode);
    }

    /// <summary>Puts an Enforcer on the field.</summary>
    /// <param name="enforcer">The Enforcer to add.</param>
    public void Add(Enforcer enforcer)
    {
        Enforcers.Add(enforcer);
    }

    /// <summary>Puts a Gorf on the field.</summary>
    /// <param name="gorf">The Gorf to add.</param>
    public void Add(Gorf gorf)
    {
        Gorfs.Add(gorf);
    }

    /// <summary>Puts a Grunt on the field.</summary>
    /// <param name="grunt">The Grunt to add.</param>
    public void Add(Grunt grunt)
    {
        Grunts.Add(grunt);
    }

    /// <summary>Puts a Hulk on the field.</summary>
    /// <param name="hulk">The Hulk to add.</param>
    public void Add(Hulk hulk)
    {
        Hulks.Add(hulk);
    }

    /// <summary>Puts a Prog on the field.</summary>
    /// <param name="prog">The Prog to add.</param>
    public void Add(Prog prog)
    {
        Progs.Add(prog);
    }

    /// <summary>Puts a Quark on the field.</summary>
    /// <param name="quark">The Quark to add.</param>
    public void Add(Quark quark)
    {
        Quarks.Add(quark);
    }

    /// <summary>Puts a Spark on the field.</summary>
    /// <param name="spark">The Spark to add.</param>
    public void Add(Spark spark)
    {
        Sparks.Add(spark);
    }

    /// <summary>Puts a Spheroid on the field.</summary>
    /// <param name="spheroid">The Spheroid to add.</param>
    public void Add(Spheroid spheroid)
    {
        Spheroids.Add(spheroid);
    }

    /// <summary>Puts a Tank on the field.</summary>
    /// <param name="tank">The Tank to add.</param>
    public void Add(Tank tank)
    {
        Tanks.Add(tank);
    }

    /// <summary>Puts a TankShell on the field.</summary>
    /// <param name="tankShell">The TankShell to add.</param>
    public void Add(TankShell tankShell)
    {
        TankShells.Add(tankShell);
    }

    /// <summary>Puts a SkullMarker on the field.</summary>
    /// <param name="skullMarker">The SkullMarker to add.</param>
    public void Add(SkullMarker skullMarker)
    {
        Skulls.Add(skullMarker);
    }

    /// <summary>Puts a RescueBonusMarker on the field.</summary>
    /// <param name="rescueBonusMarker">The RescueBonusMarker to add.</param>
    public void Add(RescueBonusMarker rescueBonusMarker)
    {
        RescueBonusMarkers.Add(rescueBonusMarker);
    }

    /// <summary>Puts a ScoreBurst on the field.</summary>
    /// <param name="scoreBurst">The ScoreBurst to add.</param>
    public void Add(ScoreBurst scoreBurst)
    {
        ScoreBursts.Add(scoreBurst);
    }

    /// <summary>Puts a StripEffect on the field.</summary>
    /// <param name="stripEffect">The StripEffect to add.</param>
    public void Add(StripEffect stripEffect)
    {
        Explosions.Add(stripEffect);
    }

    /// <summary>Puts a family member on the field, in the next free place in the family list.</summary>
    /// <param name="familyMember">The family member to add.</param>
    public void Add(FamilyMember familyMember)
    {
        FamilyList.Add(familyMember);
    }

    /// <summary>Says whether any family member is standing on the field and free.</summary>
    public bool AnyFamilyMemberAvailable()
    {
        return FamilyList.AnyAvailable();
    }

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
        IEntityList[] enemyLists = [Grunts, BerzerkRobots, Gorfs, Spheroids, Enforcers, Quarks, Tanks, Brains];
        return enemyLists.All(list => list.FieldEntities.All(entity => entity.IsDead()));
    }

    /// <summary>Draws the lists that go behind the player's lasers.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="playField">The field, which decides what may be drawn yet.</param>
    public void DrawBehindShots(SpriteBatch spriteBatch, PlayField playField)
    {
        DrawAll(_drawOrderBehindShots, spriteBatch, playField);
    }

    /// <summary>Draws the lists that go in front of the player's lasers.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="playField">The field, which decides what may be drawn yet.</param>
    public void DrawInFrontOfShots(SpriteBatch spriteBatch, PlayField playField)
    {
        DrawAll(_drawOrderInFrontOfShots, spriteBatch, playField);
    }

    /// <summary>Counts the cruise missiles in flight.</summary>
    public int GetCruiseMissileCount()
    {
        return CruiseMissiles.Count(entity => !entity.IsDead());
    }

    /// <summary>Counts the enforcers on the field.</summary>
    public int GetEnforcerCount()
    {
        return Enforcers.Count(entity => !entity.IsDead());
    }

    /// <summary>Finds every entity of one kind of robot.</summary>
    /// <param name="kind">The kind to look for.</param>
    public IEnumerable<IEntity> GetEntities(RobotKind kind)
    {
        return GetList(kind).FieldEntities;
    }

    /// <summary>Finds the family member in a place in the family list, if they are standing and free.</summary>
    /// <param name="slot">The place to look in.</param>
    public FamilyMember? GetFamilyMemberInSlot(int slot)
    {
        return FamilyList.GetMemberInSlot(slot);
    }

    /// <summary>Lists every family member who has been put on the field.</summary>
    public IReadOnlyList<FamilyMember> GetFamilyMembers()
    {
        return FamilyList.Members;
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

    /// <summary>Finds the family list place of the member nearest a point.</summary>
    /// <param name="from">The point to measure from.</param>
    public int GetNearestFamilySlot(IntVector2 from)
    {
        return FamilyList.GetNearestSlot(from);
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
            .SelectMany(kind => GetList(kind.Kind).FieldEntities)
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

    /// <summary>
    ///     Lists the places in the family list that still hold a family member who is standing on the field and free,
    ///     lowest place first.
    /// </summary>
    public IReadOnlyList<int> GetOccupiedFamilySlots()
    {
        return FamilyList.GetOccupiedSlots();
    }

    /// <summary>Counts the sparks in flight.</summary>
    public int GetSparkCount()
    {
        return Sparks.Count(spark => spark.IsAlive());
    }

    /// <summary>Counts the tanks on the field.</summary>
    public int GetTankCount()
    {
        return Tanks.Count(entity => !entity.IsDead());
    }

    /// <summary>Says whether any grunt took a step in the last tick.</summary>
    public bool HasGruntStepped()
    {
        return Grunts.Any(grunt => grunt.SteppedThisUpdate);
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
        return StripEffectTuning.GetPoolSize(engine) is not { } poolSize ||
               Explosions.Count(stripEffect => stripEffect.GetEngine() == engine) < poolSize;
    }

    /// <summary>Says whether no electrode touches a box, so that something can be put there.</summary>
    /// <param name="bounds">The box to test, in port pixels.</param>
    public bool IsClearOfElectrodes(Rectangle bounds)
    {
        return Electrodes.All(electrode => !electrode.GetBounds().Intersects(bounds));
    }

    /// <summary>Takes the dead out of every list.</summary>
    public void PruneDead()
    {
        foreach (var list in _updateOrder) list.PruneDead();
    }

    /// <summary>Moves every list on by one tick, in the order the arcade does them.</summary>
    /// <param name="gameTime">The time for this tick.</param>
    /// <param name="playField">The field the entities are on.</param>
    public void UpdateAll(GameTime gameTime, PlayField playField)
    {
        foreach (var list in _updateOrder) list.UpdateAll(gameTime, playField);
    }

    /// <summary>Draws some lists in order.</summary>
    /// <param name="lists">The lists to draw.</param>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="playField">The field, which decides what may be drawn yet.</param>
    private static void DrawAll(IEntityList[] lists, SpriteBatch spriteBatch, PlayField playField)
    {
        foreach (var list in lists) list.DrawAll(spriteBatch, playField);
    }
}
