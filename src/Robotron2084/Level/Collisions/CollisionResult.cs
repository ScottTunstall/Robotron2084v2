namespace Robotron2084.Level.Collisions;

/// <summary>
///     What a collision rule found: two things touching, and which things. It says what happened and nothing about
///     what to do.
/// </summary>
/// <remarks>
///     The field decides what each result means for the game, through <see cref="CollisionResponder" />. A rule never
///     changes
///     the field, so how the field responds can change without touching a rule, and a rule can be tested by looking at
///     what it reports.
/// </remarks>
internal abstract record CollisionResult;
