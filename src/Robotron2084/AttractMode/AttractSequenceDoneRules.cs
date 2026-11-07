namespace Robotron2084.AttractMode;

/// <summary>
///     What "done" means for each part of the attract movie (<see cref="AttractSequence" />). Port only: the arcade's
///     page script waits by time alone (notes §147).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             <see cref="AttractSequence.Family" />: no family member is in view. They have walked across and gone,
///             and come back later for the hulk's part.
///         </item>
///         <item><see cref="AttractSequence.Grunts" />: no grunt is left.</item>
///         <item>
///             <see cref="AttractSequence.Hulk" />: no family member and no skull is left. The hulk has caught Daddy and
///             Mikey. The hulk itself walks off during the next page, as in the arcade.
///         </item>
///         <item><see cref="AttractSequence.SpheroidsAndQuarks" />: no spheroid, quark, tank or enforcer is left.</item>
///         <item><see cref="AttractSequence.Electrodes" />: the hero has blown up.</item>
///     </list>
///     "Left" counts every object that is still alive, whether it is in view or hidden.
/// </remarks>
public static class AttractSequenceDoneRules
{
    private static readonly MovieAnimation[] Family = [MovieAnimation.Mommy, MovieAnimation.Daddy, MovieAnimation.Mikey];

    private static readonly MovieAnimation[] FamilyAndSkull =
        [MovieAnimation.Mommy, MovieAnimation.Daddy, MovieAnimation.Mikey, MovieAnimation.Skull];

    private static readonly MovieAnimation[] SpheroidPageRobots =
    [
        MovieAnimation.Spheroid, MovieAnimation.Quark, MovieAnimation.TankGrow, MovieAnimation.Tank,
        MovieAnimation.Enforcer
    ];

    /// <summary>Gets whether a part of the movie is done.</summary>
    /// <param name="attractSequence">The part.</param>
    /// <param name="attractObjectMachine">The movie's characters.</param>
    public static bool IsDone(AttractSequence attractSequence, AttractObjectMachine attractObjectMachine)
    {
        if (attractSequence == AttractSequence.Family)
            return !Family.Any(animation => attractObjectMachine.GetFirstObjectShowing(animation) is not null);

        return !GetAnimationsThatMustBeGone(attractSequence).Any(attractObjectMachine.HasLiveObjectWith);
    }

    /// <summary>Gets the animations no live object may have for a part to be done.</summary>
    private static MovieAnimation[] GetAnimationsThatMustBeGone(AttractSequence attractSequence)
    {
        return attractSequence switch
        {
            AttractSequence.Grunts => [MovieAnimation.Grunt],
            AttractSequence.Hulk => FamilyAndSkull,
            AttractSequence.SpheroidsAndQuarks => SpheroidPageRobots,
            AttractSequence.Electrodes => [MovieAnimation.Player],
            _ => []
        };
    }
}
