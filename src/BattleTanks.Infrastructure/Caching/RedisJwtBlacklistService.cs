using BattleTanks.Application.Abstractions;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Caching;

public sealed class RedisJwtBlacklistService : IJwtBlacklistService
{
    private readonly IConnectionMultiplexer _connection;

    public RedisJwtBlacklistService(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    public async Task BlacklistAsync(string jti, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var ttl = timeToLive <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : timeToLive;
        await _connection.GetDatabase().StringSetAsync(Key(jti), "1", ttl);
    }

    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await _connection.GetDatabase().KeyExistsAsync(Key(jti));
    }

    internal static string Key(string jti) => $"battle-tanks:jwt:blacklist:{jti}";
}
