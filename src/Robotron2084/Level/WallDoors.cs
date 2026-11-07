using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
///     Opens doors in the playfield's four walls at random, one after another, and lets grunts through each opening.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade's playfield has no doors. A wave has them when its flag says so (notes §151).</item>
///         <item>
///             Each opening is in a random wall, at a random place along it that is not in a corner
///             (<see cref="WallDoorTuning.CornerMarginArcadePixels" />).
///         </item>
///         <item>
///             A wave makes <see cref="WallDoorTuning.OpeningsPerWave" /> openings, a random wait apart. Each lets
///             <see cref="WallDoorTuning.GruntsPerOpening" /> grunts in en masse while it is wide open: a row as wide as
///             the opening comes through every <see cref="WallDoorTuning.GruntIntervalTicks" /> ticks.
///         </item>
///         <item>It is told where to put a grunt, and does not know how the grunt is made (the callback it is given).</item>
///     </list>
/// </remarks>
public sealed class WallDoors
{
    private readonly List<WallDoor> _doors = [];
    private readonly Action<IntVector2> _spawnGrunt;
    private readonly Rectangle _playfieldBounds;
    private readonly Random _random;
    private int _gruntsLeftForThisDoor;
    private int _rowsLetThroughThisDoor;
    private int _gruntTimerTicks;
    private int _openingsMade;
    private int _ticksUntilNextOpening;

    /// <summary>Makes the doors for a playfield, with the first opening still to come.</summary>
    /// <param name="playfieldBounds">The play area inside the wall, in port pixels.</param>
    /// <param name="random">Where the random numbers come from. They pick each opening's wall and place and the waits.</param>
    /// <param name="spawnGrunt">What to call to put a grunt on the field, given the place for its top-left corner.</param>
    public WallDoors(Rectangle playfieldBounds, Random random, Action<IntVector2> spawnGrunt)
    {
        _playfieldBounds = playfieldBounds;
        _random = random;
        _spawnGrunt = spawnGrunt;
        _ticksUntilNextOpening = GetRandomWaitTicks();
    }

    /// <summary>The doors that are open or opening or closing now.</summary>
    public IReadOnlyList<WallDoor> Doors => _doors;

    /// <summary>
    ///     Gets whether the doors still have something to do: an opening still to come, a door that has not shut, or a
    ///     grunt still to come through.
    /// </summary>
    public bool IsBusy()
    {
        var hasOpeningsToCome = _openingsMade < WallDoorTuning.OpeningsPerWave;
        return hasOpeningsToCome || _doors.Count > 0 || _gruntsLeftForThisDoor > 0;
    }

    /// <summary>Moves the doors on by one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    public void Update()
    {
        foreach (var door in _doors) door.Tick();

        LetGruntsThrough();
        _doors.RemoveAll(door => door.IsFinished());
        OpenTheNextDoorWhenDue();
    }

    /// <summary>Gets a random wait before the next opening, in ticks.</summary>
    private int GetRandomWaitTicks()
    {
        return WallDoorTuning.MinTicksBetweenOpenings + _random.Next(WallDoorTuning.ExtraTicksBetweenOpeningsRange);
    }

    /// <summary>Gets how many grunts stand side by side in a door's opening: a row.</summary>
    /// <param name="door">The door.</param>
    private static int GetLaneCount(WallDoor door)
    {
        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var gruntHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        var gruntLength = gruntHeight;
        if (door.Side is WallDoorSide.North or WallDoorSide.South) gruntLength = gruntWidth;

        return Math.Max(1, door.GapLength / (gruntLength + WallDoorTuning.GruntGapPixels));
    }

    /// <summary>Gets where a grunt's top-left corner goes when it comes through a door: just inside the wall, in the opening.</summary>
    /// <param name="door">The door.</param>
    /// <param name="lane">Which place across the opening, counted from 0. The grunts of a row stand side by side.</param>
    /// <param name="row">
    ///     Which row, counted from 0. Each row is a grunt's depth further in than the one before, so a grunt never comes
    ///     through on top of one that has not yet walked away from the opening.
    /// </param>
    private IntVector2 GetGruntPlace(WallDoor door, int lane, int row)
    {
        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var gruntHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        var isInTopOrBottomWall = door.Side is WallDoorSide.North or WallDoorSide.South;
        var gruntLength = gruntHeight;
        if (isInTopOrBottomWall) gruntLength = gruntWidth;

        var spacing = gruntLength + WallDoorTuning.GruntGapPixels;
        var lanes = GetLaneCount(door);
        var offsetFromMiddle = (2 * lane - (lanes - 1)) * spacing / 2;
        var alongTheWall = door.Centre + offsetFromMiddle - gruntLength / 2;

        var gruntDepth = gruntWidth;
        if (isInTopOrBottomWall) gruntDepth = gruntHeight;

        var inward = row * (gruntDepth + WallDoorTuning.GruntGapPixels);

        return door.Side switch
        {
            WallDoorSide.North => new IntVector2(alongTheWall, _playfieldBounds.Y + inward),
            WallDoorSide.South => new IntVector2(alongTheWall, _playfieldBounds.Bottom - gruntHeight - inward),
            WallDoorSide.West => new IntVector2(_playfieldBounds.X + inward, alongTheWall),
            _ => new IntVector2(_playfieldBounds.Right - gruntWidth - inward, alongTheWall)
        };
    }

    /// <summary>While a door is wide open, lets its next row of grunts through each time the grunt timer runs out.</summary>
    private void LetGruntsThrough()
    {
        var wideOpenDoor = _doors.FirstOrDefault(door => door.IsWideOpen());
        if (wideOpenDoor is null || _gruntsLeftForThisDoor <= 0) return;

        _gruntTimerTicks--;
        if (_gruntTimerTicks > 0) return;

        var gruntsInThisRow = Math.Min(GetLaneCount(wideOpenDoor), _gruntsLeftForThisDoor);
        for (var lane = 0; lane < gruntsInThisRow; lane++)
            _spawnGrunt(GetGruntPlace(wideOpenDoor, lane, _rowsLetThroughThisDoor));

        _rowsLetThroughThisDoor++;
        _gruntsLeftForThisDoor -= gruntsInThisRow;
        _gruntTimerTicks = WallDoorTuning.GruntIntervalTicks;
    }

    /// <summary>Opens a door in a random wall at a random place, when the last one has shut and the wait is over.</summary>
    private void OpenTheNextDoorWhenDue()
    {
        var isDoorStillMoving = _doors.Count > 0 || _gruntsLeftForThisDoor > 0;
        var hasOpeningsToCome = _openingsMade < WallDoorTuning.OpeningsPerWave;
        if (isDoorStillMoving || !hasOpeningsToCome) return;

        _ticksUntilNextOpening--;
        if (_ticksUntilNextOpening > 0) return;

        var side = (WallDoorSide)_random.Next(Enum.GetValues<WallDoorSide>().Length);
        var gapLength = ScreenSize.ToPortPixelsFromArcadePixels(WallDoorTuning.GapArcadePixels);
        _doors.Add(new WallDoor(side, GetRandomCentre(side, gapLength), gapLength));
        _openingsMade++;
        _gruntsLeftForThisDoor = WallDoorTuning.GruntsPerOpening;
        _rowsLetThroughThisDoor = 0;
        _gruntTimerTicks = WallDoorTuning.GruntIntervalTicks;
        _ticksUntilNextOpening = GetRandomWaitTicks();
    }

    /// <summary>Gets a random place for the middle of an opening along a wall, keeping the whole opening out of the corners.</summary>
    /// <param name="side">The wall.</param>
    /// <param name="gapLength">How long the opening is, in port pixels.</param>
    private int GetRandomCentre(WallDoorSide side, int gapLength)
    {
        var margin = ScreenSize.ToPortPixelsFromArcadePixels(WallDoorTuning.CornerMarginArcadePixels);
        var isInTopOrBottomWall = side is WallDoorSide.North or WallDoorSide.South;
        var wallStart = _playfieldBounds.Y;
        var wallLength = _playfieldBounds.Height;
        if (isInTopOrBottomWall)
        {
            wallStart = _playfieldBounds.X;
            wallLength = _playfieldBounds.Width;
        }

        var firstCentre = wallStart + margin + gapLength / 2;
        var centreRange = wallLength - 2 * margin - gapLength + 1;
        return firstCentre + _random.Next(centreRange);
    }
}
