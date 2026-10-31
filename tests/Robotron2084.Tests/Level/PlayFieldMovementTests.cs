using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     ROM-faithful movement tests for the hulk
///     (arcade-fidelity-notes §11.5 / RRH11): hulks take one step per HLKSPD-tick cycle.
/// </summary>
public sealed class PlayFieldMovementTests
{
    [Fact]
    public void Hulk_StaysPutForOneBeatIntervalThenSteps_PortTicksOfHulkSpeed()
    {
        var playField = CreateEmptyField();
        playField.SkipWaveStart();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        // Spawn well away from the player (player-vs-hulk contact is lethal),
        // but hunt the playfield centre so the aim is unbounded.
        IntVector2 centre = new(bounds.X + bounds.Width / 2 - 16, bounds.Y + bounds.Height / 2 - 16);
        IntVector2 spot = new(bounds.X + 100, bounds.Y + 100);
        var hulk = new Hulk(TestTextures.SharedTextureSet, spot, new Random(7), 8, () => centre);
        playField.FieldEntities.Hulks.Add(hulk);

        // The game is live (the robots are held until then); this update is the spawn aim.
        playField.Update(new GameTime());
        var afterAim = hulk.Position; // first unfrozen update = the spawn aim, no move

        // Step period = 8 fiftieths of a second = 9.6 ticks, so the step lands on the 10th.
        var beatIntervalClockUnits = ArcadeClock.ToPortTicksRoundedUp(8);
        for (var i = 1; i < beatIntervalClockUnits; i++)
        {
            playField.Update(new GameTime());
            Assert.Equal(afterAim, hulk.Position);
        }

        playField.Update(new GameTime());
        Assert.NotEqual(afterAim, hulk.Position);
        Assert.True(IsFullyInside(hulk.GetBounds(), bounds));
    }

    [Fact]
    public void Hulk_PressingTheLeftWall_TurnsAndNeverLeavesPlayfield()
    {
        var playField = CreateEmptyField();
        var bounds = playField.PlayfieldWall.PlayfieldBounds;
        // Right up against the left wall, hunting a point that keeps it aimed left.
        IntVector2 spot = new(bounds.X + 4, bounds.Y + bounds.Height / 2 - 16);
        var hulk = new Hulk(TestTextures.SharedTextureSet, spot, new Random(11), 5, () => spot);
        playField.FieldEntities.Hulks.Add(hulk);

        playField.SkipWaveStart();
        playField.Update(new GameTime());

        var positionBefore = hulk.Position;
        for (var i = 0; i < 300; i++)
        {
            playField.Update(new GameTime());
            Assert.True(IsFullyInside(hulk.GetBounds(), bounds));
        }

        Assert.Equal(EntityLifeState.Alive, hulk.LifeState);
        Assert.NotEqual(positionBefore, hulk.Position);
    }

    private static bool IsFullyInside(Rectangle innerBounds, Rectangle outerBounds)
    {
        return innerBounds.Left >= outerBounds.Left && innerBounds.Right <= outerBounds.Right && innerBounds.Top >= outerBounds.Top &&
               innerBounds.Bottom <= outerBounds.Bottom;
    }

    private static PlayField CreateEmptyField()
    {
        var waveParameters = new WaveParameters(
            1,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0);

        return new PlayFieldBuilder().WithParameters(waveParameters).WithSeed(99).Build();
    }
}
