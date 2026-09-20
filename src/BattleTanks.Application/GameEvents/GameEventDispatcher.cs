using BattleTanks.Contracts.GameEvents;

namespace BattleTanks.Application.GameEvents;

public sealed class GameEventDispatcher
{
    private readonly IGameEventPublisher _publisher;
    private readonly IGameEventHistory _history;

    public GameEventDispatcher(IGameEventPublisher publisher, IGameEventHistory history)
    {
        _publisher = publisher;
        _history = history;
    }

    public Task DispatchAsync(
        GameEventEnvelope gameEvent,
        GameEventQos qos,
        CancellationToken cancellationToken = default) => Task.WhenAll(
            _history.AppendAsync(gameEvent, cancellationToken),
            _publisher.PublishAsync(gameEvent, qos, cancellationToken));

    public Task<IReadOnlyList<GameEventEnvelope>> GetRecentAsync(
        string roomId,
        CancellationToken cancellationToken = default) =>
        _history.GetRecentAsync(roomId, cancellationToken);
}
