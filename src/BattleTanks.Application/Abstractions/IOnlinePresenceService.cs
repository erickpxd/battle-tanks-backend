namespace BattleTanks.Application.Abstractions;

public interface IOnlinePresenceService
{
    Task PlayerJoinedAsync(
        string roomCode,
        Guid playerId,
        string playerName,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task PlayerLeftAsync(
        string roomCode,
        Guid playerId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnlinePlayerSnapshot>> GetOnlinePlayersAsync(
        string roomCode,
        CancellationToken cancellationToken = default);

    Task SetRoomStatusAsync(
        string roomCode,
        string status,
        int playerCount,
        CancellationToken cancellationToken = default);
}

public sealed record OnlinePlayerSnapshot(
    Guid PlayerId,
    string PlayerName,
    string ConnectionId,
    DateTime JoinedAtUtc);
