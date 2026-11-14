using Robotron2084.Core.Models;
using Robotron2084.Core;
using Robotron2084.Level.Models;
using Robotron2084.Level;

namespace Robotron2084.Events.Models;

/// <summary>Something that happened on the field: what it was and where.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Position">
///     Where it happened: the top-left corner of the entity it happened to. <see cref="IntVector2.Zero" /> for a kind that is not about a
///     place, such as <see cref="GameEventKind.WaveStarted" />.
/// </param>
/// <param name="Robot">
///     Which kind of robot it happened to, for <see cref="GameEventKind.RobotKilled" />. Null for every other kind.
/// </param>
public readonly record struct GameEvent(GameEventKind Kind, IntVector2 Position, RobotKind? Robot = null);
