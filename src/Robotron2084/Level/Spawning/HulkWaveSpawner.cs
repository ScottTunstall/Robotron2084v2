using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>Puts the wave's hulks on the field, not too near where the player starts, and gives each one something to head for.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c> <c>HULKST</c>; the beat interval is the wave's <c>HLKSPD</c></item>
/// <item>Disassembly: <c>HULK_INITIALISE</c> (<c>$017C</c>) and <c>GET_FAMILY_MEMBER_FROM_LIST</c> (<c>$0237</c>)</item>
/// </list>
///
/// About one hulk in four is given the last place in the family list. That place is empty, so the hulk hunts the player
/// for the whole wave.
///
/// The other three in four search the family list. But the hulks are made before this wave's family, so the list they
/// search is the one left over from the last wave or life: the places of the family members who were still on the field
/// when it ended. Each hulk that searches takes the next of those places in turn. It then stalks whoever is put in that
/// place this time, and hunts the player once that family member has been rescued or killed. It never picks anyone else.
///
/// When nobody was left on the field last time, the search finds nothing. The arcade then follows a pointer that leads
/// nowhere, and the hulk heads for a spot off the top-right corner (<see cref="Hulk.GetPhantomTargetPosition"/>). This is
/// why many hulks wander off instead of hunting (notes §144).
/// </remarks>
public sealed class HulkWaveSpawner : IWaveSpawner
{
    /// <summary>Each hulk picks a random number from 0 up to one less than this, to decide what it heads for (see <see cref="SearchFamilyListRollAtMost"/>).</summary>
    /// <remarks>Disassembly: <c>$01B3</c>, <c>LDA $84</c>, one random byte.</remarks>
    private const int TargetRollSides = 256;

    /// <summary>A hulk whose random number is this or less searches the family list. That is about three hulks in four. The rest are given <see cref="FamilyList.LastSlot"/>.</summary>
    /// <remarks>Disassembly: <c>$01B5</c>, <c>CMPA #$C0 / BLS</c>.</remarks>
    private const int SearchFamilyListRollAtMost = 0xC0;

    /// <inheritdoc/>
    public void Spawn(WaveSpawnContext context)
    {
        PlayField field = context.Field;
        IntVector2 phantomTargetPosition = Hulk.GetPhantomTargetPosition(field.GetPlayfieldBounds());
        int nextSlotToSearch = FamilyList.FirstSlot;
        for (int i = 0; i < field.Parameters.HulkCount; i++)
        {
            IntVector2 position = context.Placement.FindSpawnPointAwayFrom(context.PlayerStart, SpawnTuning.HulkMinDistanceFromPlayer);
            int? slot = context.Random.Next(TargetRollSides) <= SearchFamilyListRollAtMost
                ? TakeNextLeftOverSlot(context.FamilySlotsLeftOver, ref nextSlotToSearch)
                : FamilyList.LastSlot;
            Func<IntVector2> getTargetPosition = slot is { } stalkedSlot
                ? () => field.GetFamilyMemberInSlot(stalkedSlot)?.Position ?? field.GetPlayerPosition()
                : () => phantomTargetPosition;
            context.Entities.Add(new Hulk(field.Sprites, position, context.Random, field.Parameters.HulkBeatIntervalRomFrames, getTargetPosition));
        }
    }

    /// <summary>Finds the next place in the left-over family list that held a family member, going round the list from where the last search stopped.</summary>
    /// <param name="familySlotsLeftOver">The places that still held a family member when the last wave or life ended.</param>
    /// <param name="nextSlotToSearch">The place to start looking from. It is moved on to the place after the one that is found.</param>
    /// <returns>The place, or null when no place held anyone.</returns>
    /// <remarks>Disassembly: <c>GET_FAMILY_MEMBER_FROM_LIST</c> (<c>$0237</c>), whose cursor is kept at <c>$9849</c>.</remarks>
    private static int? TakeNextLeftOverSlot(IReadOnlyList<int> familySlotsLeftOver, ref int nextSlotToSearch)
    {
        for (int searched = 0; searched < FamilyList.SlotCount; searched++)
        {
            int slot = (nextSlotToSearch + searched) % FamilyList.SlotCount;
            if (familySlotsLeftOver.Contains(slot))
            {
                nextSlotToSearch = slot + 1;
                return slot;
            }
        }

        return null;
    }
}
