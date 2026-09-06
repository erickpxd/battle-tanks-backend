using BattleTanks.Domain.Entities;

namespace BattleTanks.Application.Abstractions;

public interface IGameSessionRepository
{
    Task<IEnumerable<GameSession>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<GameSession> Items, int TotalCount)> GetWaitingPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<GameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a session by the player-facing room code.
    /// Room codes are stored uppercase; the argument is normalized before query.
    /// </summary>
    Task<GameSession?> GetByRoomCodeAsync(string roomCode, CancellationToken cancellationToken = default);

    Task<bool> RoomCodeExistsAsync(string roomCode, CancellationToken cancellationToken = default);

    Task AddAsync(GameSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(GameSession session, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
