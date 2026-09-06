using BattleTanks.Application.Abstractions;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Caching;

public sealed class RedisRankingCache : IRankingCache
{
    internal const string GlobalRankingKey = "battle-tanks:ranking:global";
    private const string NamesHashKey = "battle-tanks:ranking:names";

    private readonly IConnectionMultiplexer _connection;

    public RedisRankingCache(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    public async Task UpsertScoreAsync(
        Guid playerId,
        string playerName,
        int totalScore,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        await database.SortedSetAddAsync(GlobalRankingKey, playerId.ToString(), totalScore);
        await database.HashSetAsync(NamesHashKey, playerId.ToString(), playerName);
        await database.KeyExpireAsync(GlobalRankingKey, TimeSpan.FromHours(24));
        await database.KeyExpireAsync(NamesHashKey, TimeSpan.FromHours(24));
    }

    public async Task<IReadOnlyList<RankingEntry>> GetTopAsync(
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        var top = await database.SortedSetRangeByRankWithScoresAsync(
            GlobalRankingKey,
            start: 0,
            stop: Math.Clamp(take, 1, 100) - 1,
            order: Order.Descending);

        if (top.Length == 0)
        {
            return [];
        }

        var ids = top.Select(entry => (RedisValue)entry.Element).ToArray();
        var names = await database.HashGetAsync(NamesHashKey, ids);

        var results = new List<RankingEntry>(top.Length);
        for (var i = 0; i < top.Length; i++)
        {
            if (!Guid.TryParse(top[i].Element, out var playerId))
            {
                continue;
            }

            var playerName = names[i].HasValue ? names[i].ToString() : playerId.ToString("N")[..8];
            results.Add(new RankingEntry(playerId, playerName, (int)top[i].Score, i + 1));
        }

        return results;
    }

    public async Task RebuildFromAsync(
        IEnumerable<RankingEntry> entries,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        await database.KeyDeleteAsync(GlobalRankingKey);
        await database.KeyDeleteAsync(NamesHashKey);

        var materialised = entries.ToList();
        if (materialised.Count == 0)
        {
            return;
        }

        var sortedEntries = materialised
            .Select(entry => new SortedSetEntry(entry.PlayerId.ToString(), entry.TotalScore))
            .ToArray();
        var nameEntries = materialised
            .Select(entry => new HashEntry(entry.PlayerId.ToString(), entry.PlayerName))
            .ToArray();

        await database.SortedSetAddAsync(GlobalRankingKey, sortedEntries);
        await database.HashSetAsync(NamesHashKey, nameEntries);
        await database.KeyExpireAsync(GlobalRankingKey, TimeSpan.FromHours(24));
        await database.KeyExpireAsync(NamesHashKey, TimeSpan.FromHours(24));
    }
}
