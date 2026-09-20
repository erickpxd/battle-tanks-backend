namespace BattleTanks.Infrastructure.Messaging;

public interface IRedisEventStore
{
    Task<bool> AppendIfNewAsync(
        string eventListKey,
        string deduplicationKey,
        string eventId,
        string serializedEvent,
        int maxEvents,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ReadAsync(
        string eventListKey,
        CancellationToken cancellationToken = default);
}
