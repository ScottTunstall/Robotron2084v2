namespace Robotron2084.Events;

/// <summary>The kinds of thing that happen on the field that the rest of the game may want to react to.</summary>
/// <remarks>
///     A kind says what happened, never what should be done about it. Port only: the arcade has no such list.
/// </remarks>
public enum GameEventKind
{
    /// <summary>The player has been killed and has lost a life.</summary>
    PlayerKilled = 0,

    /// <summary>The player has been given one more life.</summary>
    ExtraLifeAwarded = 1,

    /// <summary>The player has saved a family member.</summary>
    FamilyMemberRescued = 2,

    /// <summary>A family member has been killed.</summary>
    FamilyMemberKilled = 3,

    /// <summary>A brain has caught a family member and started to reprogram them.</summary>
    FamilyMemberReprogrammingBegan = 4,

    /// <summary>A brain has finished reprogramming a family member, who is now a prog.</summary>
    FamilyMemberReprogrammed = 5,

    /// <summary>
    ///     A robot, an electrode or an enemy shot has been killed. <see cref="GameEvent.Robot" /> says which kind. Gorf is not
    ///     a robot kind: see <see cref="GorfKilled" />.
    /// </summary>
    RobotKilled = 6,

    /// <summary>A wave has begun: the field has had its first tick.</summary>
    WaveStarted = 7,

    /// <summary>A wave has been cleared and the game is moving on from it.</summary>
    WaveCompleted = 8,

    /// <summary>A hulk has been hit by a laser and shoved. It is not hurt.</summary>
    HulkKnockedBack = 9,

    /// <summary>Gorf has been killed. Gorf leaving at the end of its path is not a kill. Port only.</summary>
    GorfKilled = 10,

    /// <summary>One of the player's lasers has been used up: it hit something or reached the wall.</summary>
    PlayerLaserDestroyed = 11
}
