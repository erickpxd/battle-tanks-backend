using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Messaging;

public sealed class StackExchangeRedisEventStore : IRedisEventStore, IAsyncDisposable
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

    private readonly RedisHistoryOptions _options;
    private readonly ILogger<StackExchangeRedisEventStore> _logger;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnectionMultiplexer? _connection;

    public StackExchangeRedisEventStore(
        RedisHistoryOptions options,
        ILogger<StackExchangeRedisEventStore> logger)
    {
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
        var database = await GetDatabaseAsync(cancellationToken);
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
        var database = await GetDatabaseAsync(cancellationToken);
        var values = await database.ListRangeAsync(eventListKey, 0, _options.MaxEventsPerRoom - 1);
        return values
            .Reverse()
            .Where(value => value.HasValue)
            .Select(value => value.ToString())
            .ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionGate.Dispose();
    }

    private async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsConnected: true })
        {
            return _connection.GetDatabase();
        }

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not { IsConnected: true })
            {
                _connection?.Dispose();
                var configuration = ConfigurationOptions.Parse(_options.ConnectionString);
                configuration.AbortOnConnectFail = false;
                configuration.ConnectTimeout = Math.Min(configuration.ConnectTimeout, 2_000);
                _connection = await ConnectionMultiplexer.ConnectAsync(configuration);
                // EVIDÊNCIA LAB 06: tirar screenshot deste log e das chaves no redis-cli.
                _logger.LogInformation("Redis event history connected to {Endpoints}",
                    string.Join(',', configuration.EndPoints));
            }

            return _connection.GetDatabase();
        }
        finally
        {
            _connectionGate.Release();
        }
    }
}
