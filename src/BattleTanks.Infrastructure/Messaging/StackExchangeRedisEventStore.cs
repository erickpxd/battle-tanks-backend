using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Messaging;

public sealed class StackExchangeRedisEventStore : IRedisEventStore
{
    private const string AppendScript = """
        if redis.call('SADD', KEYS[2], ARGV[1]) == 0 then
            return 0
        end
        redis.call('LPUSH', KEYS[1], ARGV[2])
        redis.call('LTRIM', KEYS[1], 0, tonumber(ARGV[3]) - 1)
        redis.call('EXPIRE', KEYS[1], tonumber(ARGV[4]))
        redis.call('EXPIRE', KEYS[2], tonumber(ARGV[4]))
        return 1
        """;

    private readonly IConnectionMultiplexer _connection;
    private readonly RedisHistoryOptions _options;
    private readonly ILogger<StackExchangeRedisEventStore> _logger;

    public StackExchangeRedisEventStore(
        IConnectionMultiplexer connection,
        RedisHistoryOptions options,
        ILogger<StackExchangeRedisEventStore> logger)
    {
        _connection = connection;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> AppendIfNewAsync(
        string eventListKey,
        string deduplicationKey,
        string eventId,
        string serializedEvent,
        int maxEvents,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
            AppendScript,
            [eventListKey, deduplicationKey],
            [eventId, serializedEvent, maxEvents, Math.Max(1, (long)timeToLive.TotalSeconds)]);
        return (long)result == 1;
    }

    public async Task<IReadOnlyList<string>> ReadAsync(
        string eventListKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        var values = await database.ListRangeAsync(eventListKey, 0, _options.MaxEventsPerRoom - 1);
        _logger.LogDebug("Redis read {Count} events from {Key}", values.Length, eventListKey);
        return values
            .Reverse()
            .Where(value => value.HasValue)
            .Select(value => value.ToString())
            .ToArray();
    }
}
