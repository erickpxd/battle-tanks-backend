using System.Text.Json;
using BattleTanks.Application.GameEvents;
using BattleTanks.Contracts.GameEvents;
using Microsoft.Extensions.Logging;

namespace BattleTanks.Infrastructure.Messaging;

public sealed class RedisGameEventHistory : IGameEventHistory
{
    private readonly IRedisEventStore _store;
    private readonly RedisHistoryOptions _options;
    private readonly ILogger<RedisGameEventHistory> _logger;

    public RedisGameEventHistory(
        IRedisEventStore store,
        RedisHistoryOptions options,
        ILogger<RedisGameEventHistory> logger)
    {
        _store = store;
        _options = options;
        _logger = logger;
    }

    public async Task AppendAsync(
        GameEventEnvelope gameEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roomId = MqttGameTopic.NormalizeRoomId(gameEvent.RoomId);
            var added = await _store.AppendIfNewAsync(
                EventsKey(roomId),
                DeduplicationKey(roomId),
                gameEvent.EventId,
                JsonSerializer.Serialize(gameEvent, GameEventJson.SerializerOptions),
                _options.MaxEventsPerRoom,
                _options.TimeToLive,
                cancellationToken);
            _logger.LogInformation(
                added
                    ? "Redis stored event {EventId} for room {RoomId}"
                    : "Redis ignored duplicate event {EventId} for room {RoomId}",
                gameEvent.EventId,
                roomId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Redis history is recoverable data; an outage must not interrupt live gameplay.
            _logger.LogError(exception, "Redis failed to store event {EventId}", gameEvent.EventId);
        }
    }

    public async Task<IReadOnlyList<GameEventEnvelope>> GetRecentAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedRoomId = MqttGameTopic.NormalizeRoomId(roomId);
            var serializedEvents = await _store.ReadAsync(EventsKey(normalizedRoomId), cancellationToken);
            return serializedEvents
                .Select(serialized => JsonSerializer.Deserialize<GameEventEnvelope>(
                    serialized,
                    GameEventJson.SerializerOptions))
                .Where(gameEvent => gameEvent is not null)
                .Cast<GameEventEnvelope>()
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Redis failed to read event history for room {RoomId}", roomId);
            return [];
        }
    }

    public static string EventsKey(string roomId) => $"battle-tanks:room:{roomId}:events";

    public static string DeduplicationKey(string roomId) =>
        $"battle-tanks:room:{roomId}:event-ids";
}
