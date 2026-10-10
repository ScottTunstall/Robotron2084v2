using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// What a hulk heads for, as the arcade decides it (notes §144; disassembly <c>HULK_INITIALISE</c> at <c>$017C</c>):
/// about one hulk in four hunts the player, and the rest take a place left over in the family list from the last wave or
/// life, or head for a spot off the top-right corner when no place was left.
/// </summary>
public sealed class HulkTargetTests
{
    private const int ManyHulks = 200;

    [Fact]
    public void ThePhantomTarget_IsOffTheTopRightCornerOfThePlayfield()
    {
        var bounds = new Rectangle(40, 60, 500, 300);

        IntVector2 phantom = Hulk.GetPhantomTargetPosition(bounds);

        // Disassembly: the bytes $C8 and $13 at $7E05/$7E06, against the right wall at column $8F and the top wall at row $18.
        Assert.Equal(bounds.Right + ScreenSize.ToPortPixelsFromColumns(0xC8 - 0x8F), phantom.X);
        Assert.Equal(bounds.Y - ScreenSize.ToPortPixelsFromArcadePixels(0x18 - 0x13), phantom.Y);
    }

    [Fact]
    public void WithNobodyLeftOver_AboutThreeHulksInFourHeadForThePhantom_AndTheRestHuntThePlayer()
    {
        PlayField field = CreateField(mikeys: 1, mommies: 1, daddies: 1);
        IntVector2 phantom = Hulk.GetPhantomTargetPosition(field.GetPlayfieldBounds());
        IntVector2 player = field.GetPlayerPosition();

        int phantoms = field.Entities.Hulks.Count(hulk => hulk.GetTargetPosition() == phantom);
        int hunters = field.Entities.Hulks.Count(hulk => hulk.GetTargetPosition() == player);

        // Nobody stalks a family member, even though there is a family on the field.
        Assert.Equal(ManyHulks, phantoms + hunters);
        // 193 in 256 search the list and find nothing: about 151 of 200.
        Assert.InRange(phantoms, 130, 170);
    }

    [Fact]
    public void AHulkThatTookALeftOverPlace_StalksWhoeverIsInThatPlaceNow_ThenThePlayer()
    {
        PlayField field = CreateField(mikeys: 1, mommies: 1, daddies: 1, 1);
        IntVector2 player = field.GetPlayerPosition();
        Human mommy = field.GetFamilyMemberInSlot(1)!;
        Assert.Equal(HumanKind.Mommy, mommy.Kind);

        int stalkers = field.Entities.Hulks.Count(hulk => hulk.GetTargetPosition() == mommy.Position);
        int hunters = field.Entities.Hulks.Count(hulk => hulk.GetTargetPosition() == player);
        Assert.Equal(ManyHulks, stalkers + hunters);
        Assert.InRange(stalkers, 130, 170);

        // Once she is gone her place is empty, and every hulk hunts the player. None picks another family member.
        mommy.Kill();
        Assert.All(field.Entities.Hulks, hulk => Assert.Equal(player, hulk.GetTargetPosition()));
    }

    [Fact]
    public void TheHulksTakeTheLeftOverPlacesInTurn()
    {
        PlayField field = CreateField(mikeys: 1, mommies: 1, daddies: 1, 0, 2);
        IntVector2 mikey = field.GetFamilyMemberInSlot(0)!.Position;
        IntVector2 daddy = field.GetFamilyMemberInSlot(2)!.Position;

        int mikeyStalkers = field.Entities.Hulks.Count(hulk => hulk.GetTargetPosition() == mikey);
        int daddyStalkers = field.Entities.Hulks.Count(hulk => hulk.GetTargetPosition() == daddy);

        // The search goes round the list, so the two places are handed out alternately.
        Assert.InRange(mikeyStalkers - daddyStalkers, 0, 1);
        Assert.InRange(mikeyStalkers + daddyStalkers, 130, 170);
    }

    [Fact]
    public void ALeftOverPlaceThatIsEmptyThisTime_SendsTheHulkAfterThePlayer()
    {
        // Place 5 held someone last time, but this wave's three family members only fill places 0 to 2.
        PlayField field = CreateField(mikeys: 1, mommies: 1, daddies: 1, 5);
        IntVector2 player = field.GetPlayerPosition();

        Assert.All(field.Entities.Hulks, hulk => Assert.Equal(player, hulk.GetTargetPosition()));
    }

    [Fact]
    public void TheField_ReportsThePlacesThatStillHoldAFamilyMember()
    {
        PlayField field = new PlayFieldBuilder()
            .WithParameters(new LevelParameters(1, MikeyCount: 1, MommyCount: 1, DaddyCount: 1))
            .Build();
        Assert.Equal([0, 1, 2], field.GetOccupiedFamilySlots());

        field.GetFamilyMemberInSlot(0)!.Rescue();
        field.GetFamilyMemberInSlot(2)!.BeginReprogramming();

        Assert.Equal([1], field.GetOccupiedFamilySlots());
    }

    [Fact]
    public void AHulkAimsUpToSixteenColumnsWideOfItsTarget_SoItSometimesTurnsAwayFromATargetTenColumnsOff()
    {
        int lefts = CountFirstDirections(Direction8.Left, targetColumnsToTheRight: 10);

        // The aim is the target plus -16 to +15 columns. Seven of those 32 land at or left of the hulk.
        Assert.InRange(lefts, 20, 70);
    }

    [Fact]
    public void AHulkNeverTurnsAwayFromATargetMoreThanSixteenColumnsOff()
    {
        Assert.Equal(0, CountFirstDirections(Direction8.Left, targetColumnsToTheRight: 17));
    }

    [Fact]
    public void AHulkWithNothingToStalk_DriftsToTheTopRightOfThePlayfield()
    {
        PlayField field = new PlayFieldBuilder().Build();
        field.SkipWaveStart();
        Rectangle bounds = field.GetPlayfieldBounds();
        IntVector2 phantom = Hulk.GetPhantomTargetPosition(bounds);
        var hulk = new Hulk(TestSprites.Shared, new IntVector2(bounds.X + 40, bounds.Bottom - 80), new Random(5), beatIntervalRomFrames: 2, () => phantom);
        field.Entities.Hulks.Add(hulk);
        field.Player.TeleportTo(new IntVector2(bounds.X + 4, bounds.Bottom - 40));

        long sumX = 0;
        long sumY = 0;
        const int Samples = 4000;
        for (int tick = 0; tick < 6000 + Samples; tick++)
        {
            field.Update(new GameTime());
            if (tick >= 6000)
            {
                sumX += hulk.Position.X;
                sumY += hulk.Position.Y;
            }
        }

        // On average it sits in the right-hand half and the top half.
        Assert.True(sumX / Samples > bounds.X + (bounds.Width / 2), $"average x {sumX / Samples}");
        Assert.True(sumY / Samples < bounds.Y + (bounds.Height / 2), $"average y {sumY / Samples}");
    }

    /// <summary>Makes a field with many hulks and a small family, with the given places left over from the last field.</summary>
    private static PlayField CreateField(int mikeys, int mommies, int daddies, params int[] familySlotsLeftOver) =>
        new PlayFieldBuilder()
            .WithParameters(new LevelParameters(1, MikeyCount: mikeys, MommyCount: mommies, DaddyCount: daddies, HulkCount: ManyHulks))
            .WithFamilySlotsLeftOver(familySlotsLeftOver)
            .Build();

    /// <summary>Counts, over many hulks, how many first turn a given way when their target is a number of columns to their right.</summary>
    private static int CountFirstDirections(Direction8 direction, int targetColumnsToTheRight)
    {
        PlayField field = new PlayFieldBuilder().Build();
        field.SkipWaveStart();
        Rectangle bounds = field.GetPlayfieldBounds();
        var spot = new IntVector2(bounds.X + 200, bounds.Y + 100);
        var target = new IntVector2(spot.X + ScreenSize.ToPortPixelsFromColumns(targetColumnsToTheRight), spot.Y);
        var hulks = new List<Hulk>();
        for (int seed = 0; seed < ManyHulks; seed++)
        {
            var hulk = new Hulk(TestSprites.Shared, spot, new Random(seed), beatIntervalRomFrames: 8, () => target);
            hulks.Add(hulk);
            field.Entities.Hulks.Add(hulk);
        }

        // The first update after the game goes live is the hulk's first aim, which is always left or right.
        field.Update(new GameTime());

        return hulks.Count(hulk => hulk.Direction == direction);
    }
}
