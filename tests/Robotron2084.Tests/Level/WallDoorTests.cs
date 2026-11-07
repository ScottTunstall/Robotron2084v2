using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     The doors in the playfield's walls (port only; notes §151): two panels that slide apart from the middle, and grunts
///     that come through the opening en masse.
/// </summary>
public sealed class WallDoorTests
{
    private const int TicksLimit = 20_000;

    private static readonly Rectangle Playfield = new(40, 40, 600, 320);

    private static PlayfieldWall CreateWall()
    {
        return new PlayfieldWall(Playfield, new WallColourCycle());
    }

    private static WallDoor CreateDoor(WallDoorSide side)
    {
        return new WallDoor(side, 300, 80);
    }

    private static int GetArea(IEnumerable<Rectangle> rectangles)
    {
        return rectangles.Sum(rectangle => rectangle.Width * rectangle.Height);
    }

    [Fact]
    public void ADoorSlidesOpenWholly_HoldsWideOpen_ThenSlidesShutAgain()
    {
        var door = CreateDoor(WallDoorSide.North);
        Assert.Equal(0, door.GetOpenLength());

        var lengths = new List<int>();
        var wasWideOpen = false;
        for (var tick = 0; tick < TicksLimit && !door.IsFinished(); tick++)
        {
            door.Tick();
            lengths.Add(door.GetOpenLength());
            wasWideOpen |= door.IsWideOpen();
        }

        Assert.True(door.IsFinished());
        Assert.True(wasWideOpen);
        Assert.Equal(door.GapLength, lengths.Max());
        Assert.Equal(0, lengths[^1]);

        var opening = lengths.Take(WallDoorTuning.OpenTicks).ToList();
        Assert.Equal(opening.Order(), opening); // it only ever opens further while it is opening
    }

    [Theory]
    [InlineData(WallDoorSide.North)]
    [InlineData(WallDoorSide.South)]
    [InlineData(WallDoorSide.East)]
    [InlineData(WallDoorSide.West)]
    public void AShutDoor_LeavesTheWallWholeBecauseItsPanelsFillTheGap(WallDoorSide side)
    {
        var wall = CreateWall();
        var withoutDoors = GetArea(wall.GetWallPieces([]));
        WallDoor[] doors = [CreateDoor(side)];

        var wallArea = GetArea(wall.GetWallPieces(doors));
        var panelArea = GetArea(wall.GetDoorPanels(doors));

        Assert.Equal(withoutDoors, wallArea + panelArea);
    }

    [Fact]
    public void AWideOpenDoor_HasNoPanels_AndLeavesAGapInTheWall()
    {
        var wall = CreateWall();
        var door = CreateDoor(WallDoorSide.South);
        for (var tick = 0; tick < WallDoorTuning.OpenTicks + 1; tick++) door.Tick();
        WallDoor[] doors = [door];

        Assert.Empty(wall.GetDoorPanels(doors));
        Assert.Equal(GetArea(wall.GetWallPieces([])) - door.GapLength * PlayfieldWall.Thickness,
            GetArea(wall.GetWallPieces(doors)));
    }

    [Fact]
    public void ThePanelsSlideApartFromTheMiddleOfTheOpening()
    {
        var wall = CreateWall();
        var door = CreateDoor(WallDoorSide.North);
        for (var tick = 0; tick < WallDoorTuning.OpenTicks / 2; tick++) door.Tick();

        var panels = wall.GetDoorPanels([door]);

        var left = panels.OrderBy(panel => panel.X).First();
        var right = panels.OrderBy(panel => panel.X).Last();
        Assert.Equal(door.GetStart(), left.X); // each panel stays at its own end of the opening
        Assert.Equal(door.GetEnd(), right.Right);
        Assert.True(left.Right < door.Centre && right.X > door.Centre); // the middle of the opening is clear
    }

    [Fact]
    public void AWaveMakesItsOpenings_EachInARandomWallOutOfTheCorners_AndLetsAllTheGruntsIn()
    {
        var grunts = new List<IntVector2>();
        var doors = new WallDoors(Playfield, new Random(7), grunts.Add);
        var sidesSeen = new HashSet<WallDoorSide>();
        var openingsSeen = 0;
        var previousDoorCount = 0;

        for (var tick = 0; tick < TicksLimit && doors.IsBusy(); tick++)
        {
            doors.Update();
            foreach (var door in doors.Doors) sidesSeen.Add(door.Side);
            if (doors.Doors.Count > previousDoorCount) openingsSeen++;

            previousDoorCount = doors.Doors.Count;
        }

        Assert.False(doors.IsBusy());
        Assert.Equal(WallDoorTuning.OpeningsPerWave, openingsSeen);
        Assert.Equal(WallDoorTuning.OpeningsPerWave * WallDoorTuning.GruntsPerOpening, grunts.Count);
    }

    [Fact]
    public void TheGruntsAllComeInsideThePlayfield()
    {
        var grunts = new List<IntVector2>();
        var doors = new WallDoors(Playfield, new Random(3), grunts.Add);

        for (var tick = 0; tick < TicksLimit && doors.IsBusy(); tick++) doors.Update();

        var gruntWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var gruntHeight = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        Assert.All(grunts, grunt =>
        {
            Assert.InRange(grunt.X, Playfield.X, Playfield.Right - gruntWidth);
            Assert.InRange(grunt.Y, Playfield.Y, Playfield.Bottom - gruntHeight);
        });
        Assert.True(grunts.Count > 1);
    }

    [Fact]
    public void ADoorOnlyLetsGruntsThroughWhileItIsWideOpen()
    {
        var gruntTicks = new List<int>();
        var tick = 0;
        var doors = new WallDoors(Playfield, new Random(11), _ => gruntTicks.Add(tick));
        var wideOpenTicks = new List<int>();

        for (; tick < TicksLimit && doors.IsBusy(); tick++)
        {
            doors.Update();
            if (doors.Doors.Any(door => door.IsWideOpen())) wideOpenTicks.Add(tick);
        }

        Assert.All(gruntTicks, gruntTick => Assert.Contains(gruntTick, wideOpenTicks));
    }

    [Fact]
    public void AGruntThroughADoor_StartsAtTheSlowestTheWaveAllows_WhateverTheOthersHaveBeenSpeededUpTo()
    {
        var waveParameters = new WaveParameters(1, 6, GruntMoveDelay: 15);
        var playField = new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(5).Build();
        var gameTime = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));
        for (var tick = 0; tick < 400 && playField.RobotsFrozen(); tick++) playField.Update(gameTime);

        // The grunts already on the field are speeded up, as they are every time a grunt is killed.
        foreach (var grunt in playField.FieldEntities.Grunts) grunt.SpeedUpAsWaveGoesOn(4, 8);
        Assert.All(playField.FieldEntities.Grunts, grunt => Assert.True(grunt.MoveIntervalMaxBeats < 15));
        var knownGrunts = playField.FieldEntities.Grunts.ToList();

        playField.SpawnDoorwayGrunt(new IntVector2(Playfield.X + 100, Playfield.Y + 100));

        var newcomer = Assert.Single(playField.FieldEntities.Grunts.Except(knownGrunts));
        Assert.Equal(waveParameters.GruntMoveDelay, newcomer.MoveIntervalMaxBeats);
    }

    [Fact]
    public void NoTwoGruntsThroughTheSameOpeningComeInOnTopOfEachOther()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var grunts = new List<IntVector2>();
            var openings = new List<List<IntVector2>>();
            var doors = new WallDoors(Playfield, new Random(seed), grunt =>
            {
                grunts.Add(grunt);
            });
            var previousDoorCount = 0;
            for (var tick = 0; tick < TicksLimit && doors.IsBusy(); tick++)
            {
                doors.Update();
                if (doors.Doors.Count > previousDoorCount) openings.Add([]);

                previousDoorCount = doors.Doors.Count;
                if (openings.Count > 0) openings[^1].AddRange(grunts);

                grunts.Clear();
            }

            Assert.All(openings, opening => Assert.Equal(opening.Count, opening.Distinct().Count()));
        }
    }
}
