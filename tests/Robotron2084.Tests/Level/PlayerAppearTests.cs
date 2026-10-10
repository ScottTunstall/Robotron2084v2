using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The player's appear effect at the start of a wave (notes §143), from `RRG23.ASM` `PAPPR` (R5 $29F5) and `PDAPPR` ($29D2).
/// At `PLS1` ($2874) the arcade asks for a row fan for every row of the player's sprite (`JSR APST`, "VERTS") and a column
/// fan for every column of it (`JSR HAPST`). Six fiftieths of a second later, at `PLS1A` ($2882), it asks for a pair of leaning fans
/// for every third row. The player's own sprite is held until `PLS2` (STATUS bit 4, "PLAYER OUTPUT"). The player's sprite
/// is 8 pixels by 12 rows, which is 4 of the arcade's columns.
/// </summary>
public sealed class PlayerAppearTests
{
    /// <summary>The fiftieth of a second the player appears on in a wave with no robots: the appear loop's 33 passes, then `NAP 2` and `NAP 10`.</summary>
    private const int PlayerAppearRomFrame = 44;

    private const int PlayerSpriteRows = 12;

    private static GameTime Frame() => new(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));

    private static PlayField CreateEmptyField(IPlayerInputSource? input = null) =>
        new PlayFieldBuilder().WithParameters(new LevelParameters(LevelNumber: 1)).WithInput(input ?? new FakeInputSource()).WithSeed(7).Build();

    private static void TickToRomFrame(PlayField field, ref int ticks, int romFrame)
    {
        while (ticks < ArcadeClock.ToPortTicksRoundedUp(romFrame))
        {
            field.Update(Frame());
            ticks++;
        }
    }

    private static int Count(PlayField field, StripEngine engine) => field.Entities.Explosions.Count(effect => effect.GetEngine() == engine);

    [Fact]
    public void WhenThePlayerAppears_ARowFanIsMadeForEveryRow_AndAColumnFanForAsManyColumnsAsThereAreRecords()
    {
        PlayField field = CreateEmptyField();
        int ticks = 0;

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame - 1);
        Assert.Empty(field.Entities.Explosions);

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame);

        // The vertical routine takes its records from the object list, so all 12 are made. The sprite is 4 columns
        // wide, but the horizontal routine has 2 records, so only the first 2 asked for are made.
        Assert.Equal(PlayerSpriteRows, Count(field, StripEngine.Vertical));
        Assert.Equal(StripExplosionTuning.HorizontalPoolSize, Count(field, StripEngine.Horizontal));
        Assert.Equal(0, Count(field, StripEngine.Diagonal));
        Assert.All(field.Entities.Explosions, effect => Assert.Equal(StripEffectKind.Appear, effect.Kind));
        Assert.All(field.Entities.Explosions, effect => Assert.Equal(field.Player.GetBounds(), effect.GetBounds()));
    }

    [Fact]
    public void SixRomFramesLater_APairOfLeaningFansIsMadeForEveryThirdRow()
    {
        PlayField field = CreateEmptyField();
        int ticks = 0;

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 5);
        Assert.Equal(0, Count(field, StripEngine.Diagonal));

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 6);

        // The count runs 12, 9, 6, 3, 0 (`SUBA #3 / BPL DPAPP1`), and each one makes two fans (`CLRA`, then `COMA`):
        // ten, which is every record the diagonal routine has.
        Assert.Equal(StripExplosionTuning.DiagonalPoolSize, Count(field, StripEngine.Diagonal));
        Assert.Equal(5, field.Entities.Explosions.Count(effect => effect.Slope == 1));
        Assert.Equal(5, field.Entities.Explosions.Count(effect => effect.Slope == -1));
    }

    [Fact]
    public void ThePlayersOwnSprite_IsNotDrawn_UntilTheGameIsLive()
    {
        PlayField field = CreateEmptyField();
        int ticks = 0;

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 9);
        Assert.True(field.HasPlayerAppeared());
        Assert.False(field.IsPlayerDrawn());

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 10); // PLS2: CLR STATUS
        Assert.True(field.IsLive());
        Assert.True(field.IsPlayerDrawn());
        Assert.NotEmpty(field.Entities.Explosions); // the appear is still running
    }

    [Fact]
    public void OnceTheGameIsLive_TheStripsStayWithThePlayer()
    {
        // AWRIT0 ("SCROLL EM", $5D57): with STATUS clear, each routine takes the player's place again when it redraws.
        var input = new FakeInputSource(new PlayerInputState(new IntVector2(1, 0), IntVector2.Zero, FireHeld: false));
        PlayField field = CreateEmptyField(input);
        int ticks = 0;
        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 10);
        Rectangle start = field.Player.GetBounds();

        for (int tick = 0; tick < 12; tick++)
        {
            Dictionary<StripEffect, int> spacingBefore = field.Entities.Explosions.ToDictionary(effect => effect, effect => effect.GetSpacing());
            field.Update(Frame());

            foreach (StripEffect effect in field.Entities.Explosions.Where(effect => effect.GetSpacing() != spacingBefore[effect]))
            {
                Assert.Equal(field.Player.GetBounds(), effect.GetBounds());
            }
        }

        Assert.NotEqual(start, field.Player.GetBounds());
        Assert.All(field.Entities.Explosions, effect => Assert.NotEqual(start, effect.GetBounds()));
    }

    [Fact]
    public void TheAppearIsOver_WhenTheLastColumnFanHasClosedUp()
    {
        // The leaning fans last 15 fiftieths of a second from frame 50, the row fans 29 from frame 44, and the column fans 31.
        PlayField field = CreateEmptyField();
        int ticks = 0;

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 6 + 15);
        Assert.Equal(0, Count(field, StripEngine.Diagonal));

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 29);
        Assert.Equal(0, Count(field, StripEngine.Vertical));
        Assert.Equal(StripExplosionTuning.HorizontalPoolSize, Count(field, StripEngine.Horizontal));

        TickToRomFrame(field, ref ticks, PlayerAppearRomFrame + 31);
        Assert.Empty(field.Entities.Explosions);
    }

    [Fact]
    public void ATestThatSkipsTheStartOfTheWave_GetsNoAppearEffect()
    {
        PlayField field = CreateEmptyField();
        field.SkipWaveStart();

        field.Update(Frame());

        Assert.Empty(field.Entities.Explosions);
        Assert.True(field.IsPlayerDrawn());
    }
}
