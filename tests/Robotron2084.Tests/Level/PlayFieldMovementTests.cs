using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// ROM-faithful movement tests for the hulk
/// (arcade-fidelity-notes §11.5 / RRH11): hulks take one step per HLKSPD-tick cycle.
/// </summary>
public sealed class PlayFieldMovementTests
{
    [Fact]
    public void Hulk_StaysPutForOneBeatIntervalThenSteps_PortTicksOfHulkSpeed()
    {
        PlayField field = CreateEmptyField();
        field.SkipWaveStart();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        // Spawn well away from the player (player-vs-hulk contact is lethal),
        // but hunt the playfield center so the aim is unbounded.
        IntVector2 center = new(bounds.X + bounds.Width / 2 - 16, bounds.Y + bounds.Height / 2 - 16);
        IntVector2 spot = new(bounds.X + 100, bounds.Y + 100);
        var hulk = new Hulk(TestSprites.Shared, spot, new Random(7), beatIntervalRomFrames: 8, () => center);
        field.Entities.Hulks.Add(hulk);

        // The game is live (the robots are held until then); this update is the spawn aim.
        field.Update(new GameTime());
        IntVector2 afterAim = hulk.Position; // first unfrozen update = the spawn aim, no move

        // Step period = 8 fiftieths of a second = 9.6 ticks, so the step lands on the 10th.
        int beatIntervalClockUnits = ArcadeClock.ToPortTicksRoundedUp(8);
        for (int i = 1; i < beatIntervalClockUnits; i++)
        {
            field.Update(new GameTime());
            Assert.Equal(afterAim, hulk.Position);
        }

        field.Update(new GameTime());
        Assert.NotEqual(afterAim, hulk.Position);
        Assert.True(IsFullyInside(hulk.GetBounds(), bounds));
    }

    [Fact]
    public void Hulk_PressingTheLeftWall_ReaimsAndNeverLeavesPlayfield()
    {
        PlayField field = CreateEmptyField();
        Rectangle bounds = field.Wall.PlayfieldBounds;
        // Right up against the left wall, hunting a point that keeps it aimed left.
        IntVector2 spot = new(bounds.X + 4, bounds.Y + bounds.Height / 2 - 16);
        var hulk = new Hulk(TestSprites.Shared, spot, new Random(11), beatIntervalRomFrames: 5, () => spot);
        field.Entities.Hulks.Add(hulk);

        field.SkipWaveStart();
        field.Update(new GameTime());

        IntVector2 positionBefore = hulk.Position;
        for (int i = 0; i < 300; i++)
        {
            field.Update(new GameTime());
            Assert.True(IsFullyInside(hulk.GetBounds(), bounds));
        }

        Assert.Equal(EntityLifeState.Alive, hulk.LifeState);
        Assert.NotEqual(positionBefore, hulk.Position);
    }

    private static bool IsFullyInside(Rectangle rect, Rectangle bounds) =>
        rect.Left >= bounds.Left && rect.Right <= bounds.Right && rect.Top >= bounds.Top && rect.Bottom <= bounds.Bottom;

    private static PlayField CreateEmptyField()
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: 0,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0);

        return new PlayFieldBuilder().WithParameters(parameters).WithSeed(99).Build();
    }
}
