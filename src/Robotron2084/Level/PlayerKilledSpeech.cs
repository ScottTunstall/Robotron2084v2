using Robotron2084.Events;
using Robotron2084.Events.Models;

namespace Robotron2084.Level;

/// <summary>
///     Has the robots say they have got the player the moment the player is killed, not once the death animation has
///     finished (<see cref="RobotTaunts.SayPlayerKilled" />).
/// </summary>
/// <param name="gameSession">The game, whose robots speak.</param>
public sealed class PlayerKilledSpeech(GameSession gameSession) : IGameEventListener
{
    /// <summary>Speaks when the event is a player being killed; any other event is left alone.</summary>
    /// <param name="gameEvent">What happened.</param>
    public void Handle(GameEvent gameEvent)
    {
        if (gameEvent.Kind != GameEventKind.PlayerKilled) return;

        gameSession.RobotTaunts.SayPlayerKilled(gameSession);
    }
}
