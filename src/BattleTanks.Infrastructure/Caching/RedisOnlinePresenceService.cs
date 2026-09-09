using System.Text.Json;
using BattleTanks.Application.Abstractions;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Caching;

public sealed class RedisOnlinePresenceService : IOnlinePresenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;

    public RedisOnlinePresenceService(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    public async Task PlayerJoinedAsync(
        string roomCode,
        Guid playerId,
        string playerName,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        var snapshot = new OnlinePlayerSnapshot(playerId, playerName, connectionId, DateTime.UtcNow);
        var payload = JsonSerializer.Serialize(snapshot, SerializerOptions);

        var batch = database.CreateBatch();
        _ = batch.HashSetAsync(OnlineKey(roomCode), playerId.ToString(), payload);
        _ = batch.SetAddAsync(GlobalOnlineKey, playerId.ToString());
        batch.Execute();
        await database.KeyExpireAsync(OnlineKey(roomCode), TimeSpan.FromHours(6));
    }

    public async Task PlayerLeftAsync(
        string roomCode,
        Guid playerId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        var existing = await database.HashGetAsync(OnlineKey(roomCode), playerId.ToString());
        if (existing.HasValue)
        {
            var snapshot = JsonSerializer.Deserialize<OnlinePlayerSnapshot>(existing!, SerializerOptions);
            if (snapshot is not null &&
                !string.Equals(snapshot.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return;
            }
        }

        await database.HashDeleteAsync(OnlineKey(roomCode), playerId.ToString());
        await database.SetRemoveAsync(GlobalOnlineKey, playerId.ToString());
    }

    public async Task<IReadOnlyList<OnlinePlayerSnapshot>> GetOnlinePlayersAsync(
        string roomCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await _connection.GetDatabase().HashGetAllAsync(OnlineKey(roomCode));
        return entries
            .Select(entry => JsonSerializer.Deserialize<OnlinePlayerSnapshot>(entry.Value!, SerializerOptions))
            .Where(snapshot => snapshot is not null)
            .Cast<OnlinePlayerSnapshot>()
            .ToArray();
    }

    public async Task SetRoomStatusAsync(
        string roomCode,
        string status,
        int playerCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _connection.GetDatabase();
        await database.HashSetAsync(
            RoomMetaKey(roomCode),
            [
                new HashEntry("status", status),
                new HashEntry("playerCount", playerCount),
                new HashEntry("updatedAtUtc", DateTime.UtcNow.ToString("O"))
            ]);
        await database.KeyExpireAsync(RoomMetaKey(roomCode), TimeSpan.FromHours(6));
    }

    internal static string OnlineKey(string roomCode) => $"battle-tanks:room:{roomCode}:online";
    internal static string RoomMetaKey(string roomCode) => $"battle-tanks:room:{roomCode}:meta";
    internal const string GlobalOnlineKey = "battle-tanks:players:online";
}
