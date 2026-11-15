using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core.Models;
using Robotron2084.Core;
using Robotron2084.Entities.Collections;
using Robotron2084.Entities.Extensions;
using Robotron2084.Entities.Models;
using Robotron2084.Entities;
using Robotron2084.Level.Models;
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
            Sparks, TankShells, CruiseMissiles, FamilyList, Skulls, RescueBonusMarkers, Explosions, ScoreBursts
        ];
        _drawOrderBehindShots =
        [
            Electrodes, Skulls, RescueBonusMarkers, FamilyList, Grunts, BerzerkRobots, Gorfs, Hulks, Spheroids, Enforcers,
            Quarks, Tanks, Brains, Progs
        ];
        _drawOrderInFrontOfShots =
        [
            Sparks, TankShells, CruiseMissiles, Explosions, ScoreBursts
        ];
    }

    /// <summary>The BerzerkRobots.</summary>
    public BerzerkRobotList BerzerkRobots { get; } = new();

    /// <summary>The brains.</summary>
    public BrainList Brains { get; } = new();

    /// <summary>The cruise missiles the brains have fired.</summary>
    public CruiseMissileList CruiseMissiles { get; } = new();

    /// <summary>
    ///     The lists drawn behind the player's lasers, in order: the electrodes, the family and their markers, then the
    ///     robots.
    /// </summary>
    public IReadOnlyList<IEntityList> DrawOrderBehindShots => _drawOrderBehindShots;

    /// <summary>The lists drawn in front of the player's lasers, in order: the enemy shots, the explosions, then the bursts.</summary>
    public IReadOnlyList<IEntityList> DrawOrderInFrontOfShots => _drawOrderInFrontOfShots;

    /// <summary>The electrodes.</summary>
    public ElectrodeList Electrodes { get; } = new();

    /// <summary>The enforcers the spheroids have dropped.</summary>
    public EnforcerList Enforcers { get; } = new();

    /// <summary>The strip explosions and the wave-start appear effects, which share one pool in the arcade.</summary>
    public StripEffectList Explosions { get; } = new();

    /// <summary>The family: who is on the field and the places the brains and hulks pick them by.</summary>
    public FamilyList FamilyList { get; } = new();

    /// <summary>The Gorfs.</summary>
    public GorfList Gorfs { get; } = new();

    /// <summary>The grunts.</summary>
    public GruntList Grunts { get; } = new();

    /// <summary>The hulks.</summary>
    public HulkList Hulks { get; } = new();

    /// <summary>The progs: family members the brains have reprogrammed.</summary>
    public ProgList Progs { get; } = new();

    /// <summary>The quarks.</summary>
    public QuarkList Quarks { get; } = new();

    /// <summary>The bonus scores shown where family members were rescued.</summary>
    public RescueBonusMarkerList RescueBonusMarkers { get; } = new();

    /// <summary>The bursts a spheroid or a quark leaves when it dies, in place of a strip explosion.</summary>
    /// <remarks>
    ///     Original source: <c>CIRKP</c> and <c>CIRKV</c>. They take a free object, not one of the ten shared explosion
    ///     records, so there is no limit on them (notes §64).
    /// </remarks>
    public ScoreBurstList ScoreBursts { get; } = new();

    /// <summary>The skulls left where family members were killed.</summary>
    public SkullMarkerList Skulls { get; } = new();

    /// <summary>The sparks the enforcers have fired.</summary>
    public SparkList Sparks { get; } = new();

    /// <summary>The spheroids.</summary>
    public SpheroidList Spheroids { get; } = new();

    /// <summary>The tanks the quarks have dropped.</summary>
    public TankList Tanks { get; } = new();

    /// <summary>The shells the tanks have fired.</summary>
    public TankShellList TankShells { get; } = new();

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
        IEntityList[] enemyLists = [Grunts, BerzerkRobots, Gorfs, Spheroids, Enforcers, Quarks, Tanks, Brains];
        return enemyLists.All(list => list.AreAllDead());
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

    /// <summary>Finds where the nearest living robot to a point is, for the attract demo's player to steer by.</summary>
    /// <param name="from">The point to measure from.</param>
    /// <returns>The robot's position, or null when there is none.</returns>
    /// <remarks>
    ///     Only the kinds the demo's player chases are looked at (<see cref="RobotKindRow.IsChasedByDemoPlayer" />).
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
