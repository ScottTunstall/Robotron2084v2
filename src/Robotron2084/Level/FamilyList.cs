using Robotron2084.Core;
using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>The family on the field: Mommy, Daddy and Mikey, each in a numbered place that the brains and hulks pick them by.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c> <c>HTAB</c>, the family list that <c>HUMSTV</c> fills, and <c>RRB10.ASM</c> <c>GETHTG</c>, which searches it</item>
/// <item>Disassembly: <c>asm/robomame.asm</c>, the list at <c>$B354</c> and <c>FIND_NEAREST_FAMILY_MEMBER_TO_PROG</c> (<c>$1B95</c>)</item>
/// </list>
/// A member who is dead or in a brain's hold is off the ROM's list, so their place reads as empty.
/// </remarks>
public sealed class FamilyList
{
    /// <summary>The first place in the family list.</summary>
    /// <remarks>Disassembly: <c>$B354</c>.</remarks>
    internal const int FirstSlot = 0;

    private readonly EntityList<Human> _members = new();

    /// <summary>The next place to hand out. The ROM fills the list upward from the first place.</summary>
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
    /// </list>
    /// This is where the arcade's "all the brains chase Mikey" bug lives. A brain picks its target as it is made, before
    /// the family exists, so every brain leaves with the first place in hand; Mikey fills that place, because the Mikeys
    /// are made first. Each brain keeps the place until it empties (notes §18.8).
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

    /// <summary>Finds where the last member to join is standing, if they are still alive.</summary>
    /// <returns>Their position, or null when there is no such member. A hulk that gets null goes for the player instead.</returns>
    /// <remarks>Original source: <c>RRH11.ASM</c>, the hulk's "last slot" target. Disassembly: <c>$010D</c> and <c>$0113</c>, the fall back to the player.</remarks>
    internal IntVector2? GetLastMemberPosition() =>
        _members.Count > 0 && _members.Last.IsAlive() ? _members.Last.Position : null;
}
