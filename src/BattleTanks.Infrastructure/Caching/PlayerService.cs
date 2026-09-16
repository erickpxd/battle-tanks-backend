using System.Text.Json;
using BattleTanks.Application.Abstractions;
using BattleTanks.Domain.Entities;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="IPlayerRepository"/> that introduces a Redis read-through cache
/// for the hot authentication path (<see cref="GetByEmailAsync"/>).
///
/// Placement rationale: Redis is an infrastructure concern. The Application layer defines only
/// the <see cref="IPlayerRepository"/> contract; use cases (<c>LoginPlayerUseCase</c>,
/// <c>RegisterPlayerUseCase</c>) are unaware of this decorator and require zero changes.
/// This class is composed in <see cref="DependencyInjection"/> via the Decorator pattern.
/// </summary>
public sealed class PlayerService : IPlayerRepository
{
    // Key format mirrors the project's existing convention (see RedisRankingCache, RedisJwtBlacklistService).
    private const string KeyPrefix = "battle-tanks:player:email:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IPlayerRepository _inner;
    private readonly IConnectionMultiplexer _redis;

    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PlayerService(IPlayerRepository inner, IConnectionMultiplexer redis)
    {
        _inner = inner;
        _redis = redis;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cache-aside strategy:
    ///   1. Check Redis — return immediately on hit (no DB round-trip).
    ///   2. On miss: delegate to the inner repository (PostgreSQL).
    ///   3. Populate the cache so the next login for the same e-mail is served from Redis.
    /// </remarks>
    public async Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(email);
        var db = _redis.GetDatabase();

        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<Player>(cached!, SerializerOptions);
        }

        var player = await _inner.GetByEmailAsync(email, cancellationToken);
        if (player is not null)
        {
            var json = JsonSerializer.Serialize(player, SerializerOptions);
            await db.StringSetAsync(key, json, CacheTtl);
        }

        return player;
    }

    /// <inheritdoc />
    /// <remarks>
    /// After persisting a new player the e-mail cache key is proactively invalidated.
    /// This guarantees that a subsequent <see cref="GetByEmailAsync"/> for that e-mail
    /// fetches the freshly written row (with the real DB-assigned ID) instead of a stale hit.
    /// </remarks>
    public async Task AddAsync(Player player, CancellationToken cancellationToken = default)
    {
        await _inner.AddAsync(player, cancellationToken);

        // Invalidate before the row is committed so there is no window where
        // the cache holds a player object without a valid persisted state.
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(player.Email));
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _inner.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// GetByIdAsync is not cached: it is used in internal flows that do not hit
    /// the hot authentication path and where freshness is more critical than speed.
    /// </remarks>
    public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _inner.GetByIdAsync(id, cancellationToken);

    private static string BuildKey(string email)
        => KeyPrefix + email.Trim().ToLowerInvariant();
}
