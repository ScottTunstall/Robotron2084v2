using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>The family on the field: Mommy, Daddy and Mikey, each in a numbered place that the brains and hulks pick them by.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c> <c>HTAB</c>, the family list that
/// <c>HUMSTV</c> fills, and <c>RRB10.ASM</c> <c>GETHTG</c>, which searches it</item>
/// <item>Disassembly: <c>asm/robomame.asm</c>, the list at <c>$B354</c> and
/// <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>)</item>
/// </list> A member who is dead or in
/// a brain's hold is off the ROM's list, so their place reads as empty.</item>
/// </list>
/// </remarks>
public sealed class FamilyList
{
    /// <summary>The first place in the family list.</summary>
    /// <remarks>Disassembly: <c>$B354</c>.</remarks>
    internal const int FirstSlot = 0;

    /// <summary>How many places the family list has.</summary>
    /// <remarks>Disassembly: the list runs from <c>$B354</c> up to, but not including, <c>$B3A4</c>, at two bytes a place.</remarks>
    internal const int SlotCount = 40;

    /// <summary>The last place in the family list. It is empty unless the wave has <see cref="SlotCount"/> family members, so a hulk that is given it hunts the player.</summary>
    /// <remarks>Disassembly: <c>$B3A2</c>, which <c>HULK_INITIALISE</c> gives to about one hulk in four (<c>$01B9</c>).</remarks>
    internal const int LastSlot = SlotCount - 1;

    private readonly EntityList<Human> _members = new();

    /// <summary>The next place to give out. The places are given out in order, starting from the first.</summary>
    private int _nextSlot;

    /// <summary>Every member, in the order they joined, whether or not they are still on the field.</summary>
    internal EntityList<Human> Members => _members;

    /// <summary>Puts a member in the next free place.</summary>
    /// <param name="human">The member to add.</param>
    /// <remarks>Original source: <c>RRH11.ASM</c> <c>HUMSTV</c>.</remarks>
    internal void Add(Human human)
    {
        human.FamilySlot = _nextSlot++;
        _members.Add(human);
    }

    /// <summary>Says whether any member is still standing on the field and free.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNL0</c>, <c>MOMCNT + DADCNT + KIDCNT</c> not being zero.</remarks>
    internal bool AnyAvailable() => _members.Any(human => human.IsGraspable());

    /// <summary>Finds the member in a place, if they are still standing and free.</summary>
    /// <param name="slot">The place to look in.</param>
    /// <returns>The member, or null when the place reads as empty. That is how a brain loses its target and looks for another.</returns>
    internal Human? GetMemberInSlot(int slot) =>
        _members.FirstOrDefault(human => human.FamilySlot == slot && human.IsGraspable());

    /// <summary>Finds the place of the member nearest a point. When two are as near as each other, the later place wins.</summary>
    /// <param name="from">The point to measure from, such as a brain's top-left corner.</param>
    /// <returns>The place, or <see cref="FirstSlot"/> when every place is empty.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRB10.ASM</c> <c>GETHTG</c></item>
    /// <item>Disassembly: <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>)</item>
    /// </list> This is
    /// where the arcade's "all the brains chase Mikey" bug lives. A brain picks its target as it is
    /// made, before the family exists, so every brain leaves with the first place in hand; Mikey
    /// fills that place, because the Mikeys are made first. Each brain keeps the place until it
    /// empties (notes §18.8).</item>
    /// </list>
    /// </remarks>
    internal int GetNearestSlot(IntVector2 from)
    {
        int nearestSlot = FirstSlot;
        int nearestDistance = int.MaxValue;
        foreach (Human human in _members)
        {
            if (!human.IsGraspable())
            {
                continue;
            }

            int distance = ScreenSize.ToColumnAndRowDistance(human.Position, from);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestSlot = human.FamilySlot;
            }
        }

        return nearestSlot;
    }

    /// <summary>Lists the places that still hold a member who is standing on the field and free, lowest place first.</summary>
    /// <returns>The places. When a wave or a life ends, these are the places the arcade's list is left holding, and the next wave's hulks pick what to stalk from them.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HTAB</c>, which is only cleared when the next family is put on.</item>
    /// <item>Disassembly: <c>CLEAR_FAMILY_MEMBER_LIST</c> (<c>$0200</c>), called only from
    /// <c>INITIALISE_FAMILY_MEMBERS</c> (<c>$02B2</c>), which runs after the hulks are made (<c>$2831</c>, then <c>$283A</c>).</item>
    /// </list>
    /// </remarks>
    internal IReadOnlyList<int> GetOccupiedSlots() =>
        [.. _members.Where(human => human.IsGraspable()).Select(human => human.FamilySlot).Order()];
}
