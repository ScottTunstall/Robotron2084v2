using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     The player's appear effect at the start of a wave (notes §143), from `RRG23.ASM` `PAPPR` (R5 $29F5) and `PDAPPR`
///     ($29D2).
///     At `PLS1` ($2874) the arcade asks for a row fan for every row of the player's sprite (`JSR APST`, "VERTS") and a
///     column
///     fan for every column of it (`JSR HAPST`). Six fiftieths of a second later, at `PLS1A` ($2882), it asks for a pair
///     of leaning fans
///     for every third row. The player's own sprite is held until `PLS2` (STATUS bit 4, "PLAYER OUTPUT"). The player's
///     sprite
///     is 8 pixels by 12 rows, which is 4 of the arcade's columns.
/// </summary>
public sealed class PlayerAppearTests
{
    /// <summary>
    ///     The fiftieth of a second the player appears on in a wave with no robots: the appear loop's 33 passes, then
    ///     `NAP 2` and `NAP 10`.
    /// </summary>
    private const int PlayerAppearArcadeFrame = 44;

    private const int PlayerSpriteRows = 12;

    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
    }

    private static PlayField CreateEmptyField(IPlayerInputSource? playerInputSource = null)
    {
        return new PlayFieldBuilder().WithParameters(new WaveParameters(1)).WithInput(playerInputSource ?? new FakeInputSource())
            .WithSeed(7).Build();
    }

    private static void TickToArcadeFrame(PlayField playField, ref int ticks, int arcadeFrame)
    {
        while (ticks < ArcadeClock.ToPortTicksRoundedUp(arcadeFrame))
        {
            playField.Update(OneTick());
            ticks++;
        }
    }

    private static int Count(PlayField playField, StripEngine engine)
    {
        return playField.FieldEntities.Explosions.Count(effect => effect.GetEngine() == engine);
    }

    [Fact]
    public void WhenThePlayerAppears_ARowFanIsMadeForEveryRow_AndAColumnFanForAsManyColumnsAsThereAreRecords()
    {
        var playField = CreateEmptyField();
        var ticks = 0;

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame - 1);
        Assert.Empty(playField.FieldEntities.Explosions);

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame);

        // The vertical routine takes its records from the object list, so all 12 are made. The sprite is 4 columns
        // wide, but the horizontal routine has 2 records, so only the first 2 asked for are made.
        Assert.Equal(PlayerSpriteRows, Count(playField, StripEngine.Vertical));
        Assert.Equal(StripEffectTuning.HorizontalPoolSize, Count(playField, StripEngine.Horizontal));
        Assert.Equal(0, Count(playField, StripEngine.Diagonal));
        Assert.All(playField.FieldEntities.Explosions, effect => Assert.Equal(StripEffectKind.Appear, effect.Kind));
        Assert.All(playField.FieldEntities.Explosions, effect => Assert.Equal(playField.Player.GetBounds(), effect.GetBounds()));
    }

    [Fact]
    public void SixArcadeFramesLater_APairOfLeaningFansIsMadeForEveryThirdRow()
    {
        var playField = CreateEmptyField();
        var ticks = 0;

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 5);
        Assert.Equal(0, Count(playField, StripEngine.Diagonal));

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 6);

        // The count runs 12, 9, 6, 3, 0 (`SUBA #3 / BPL DPAPP1`), and each one makes two fans (`CLRA`, then `COMA`):
        // ten, which is every record the diagonal routine has.
        Assert.Equal(StripEffectTuning.DiagonalPoolSize, Count(playField, StripEngine.Diagonal));
        Assert.Equal(5, playField.FieldEntities.Explosions.Count(effect => effect.Slope == 1));
        Assert.Equal(5, playField.FieldEntities.Explosions.Count(effect => effect.Slope == -1));
    }

    [Fact]
    public void ThePlayersOwnSprite_IsNotDrawn_UntilTheGameIsLive()
    {
        var playField = CreateEmptyField();
        var ticks = 0;

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 9);
        Assert.True(playField.HasPlayerAppeared());
        Assert.False(playField.IsPlayerVisible());

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 10); // PLS2: CLR STATUS
        Assert.True(playField.IsLive());
        Assert.True(playField.IsPlayerVisible());
        Assert.NotEmpty(playField.FieldEntities.Explosions); // the appear is still running
    }

    [Fact]
    public void OnceTheGameIsLive_TheStripsStayWithThePlayer()
    {
        // AWRIT0 ("SCROLL EM", $5D57): with STATUS clear, each routine takes the player's place again when it redraws.
        var playerInputSource = new FakeInputSource(new PlayerInputState(new IntVector2(1, 0), IntVector2.Zero, false));
        var playField = CreateEmptyField(playerInputSource);
        var ticks = 0;
        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 10);
        var start = playField.Player.GetBounds();

        for (var tick = 0; tick < 12; tick++)
        {
            var spacingBefore = playField.FieldEntities.Explosions.ToDictionary(effect => effect, effect => effect.GetSpacing());
            playField.Update(OneTick());

            foreach (var effect in playField.FieldEntities.Explosions.Where(effect =>
                         effect.GetSpacing() != spacingBefore[effect]))
                Assert.Equal(playField.Player.GetBounds(), effect.GetBounds());
        }

        Assert.NotEqual(start, playField.Player.GetBounds());
        Assert.All(playField.FieldEntities.Explosions, effect => Assert.NotEqual(start, effect.GetBounds()));
    }

    [Fact]
    public void TheAppearIsOver_WhenTheLastColumnFanHasClosedUp()
    {
        // The leaning fans last 15 fiftieths of a second from frame 50, the row fans 29 from frame 44, and the column fans 31.
        var playField = CreateEmptyField();
        var ticks = 0;

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 6 + 15);
        Assert.Equal(0, Count(playField, StripEngine.Diagonal));

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 29);
        Assert.Equal(0, Count(playField, StripEngine.Vertical));
        Assert.Equal(StripEffectTuning.HorizontalPoolSize, Count(playField, StripEngine.Horizontal));

        TickToArcadeFrame(playField, ref ticks, PlayerAppearArcadeFrame + 31);
        Assert.Empty(playField.FieldEntities.Explosions);
    }

    [Fact]
    public void ATestThatSkipsTheStartOfTheWave_GetsNoAppearEffect()
    {
        var playField = CreateEmptyField();
        playField.SkipWaveStart();

        playField.Update(OneTick());

        Assert.Empty(playField.FieldEntities.Explosions);
        Assert.True(playField.IsPlayerVisible());
    }
}
