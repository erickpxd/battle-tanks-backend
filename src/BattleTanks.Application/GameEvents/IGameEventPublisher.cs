using BattleTanks.Contracts.GameEvents;

namespace BattleTanks.Application.GameEvents;

public enum GameEventQos
{
    AtMostOnce = 0,
    AtLeastOnce = 1
}

public interface IGameEventPublisher
{
    Task PublishAsync(
        GameEventEnvelope gameEvent,
        GameEventQos qos,
        CancellationToken cancellationToken = default);
}
