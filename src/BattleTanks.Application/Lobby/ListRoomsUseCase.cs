using BattleTanks.Application.Abstractions;
using BattleTanks.Contracts.Responses.Common;
using BattleTanks.Contracts.Responses.Room;
using BattleTanks.Domain.Entities;

namespace BattleTanks.Application.Lobby;

public class ListRoomsUseCase
{
    private readonly IGameSessionRepository _sessionRepository;

    public ListRoomsUseCase(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    /// <summary>
    /// Returns all rooms with Waiting status (legacy, unpaged). Prefer <see cref="ExecutePagedAsync"/>.
    /// </summary>
    public async Task<IEnumerable<RoomResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetAllAsync(cancellationToken);

        return sessions
            .Where(s => s.Status == GameSessionStatus.Waiting)
            .Select(MapToResponse);
    }

    public async Task<PagedResult<RoomResponse>> ExecutePagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _sessionRepository.GetWaitingPagedAsync(
            page,
            pageSize,
            cancellationToken);

        return new PagedResult<RoomResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            TotalCount = totalCount
        };
    }

    private static RoomResponse MapToResponse(GameSession session) => new()
    {
        Id = session.Id,
        RoomCode = session.RoomCode,
        MapSelected = session.MapSelected,
        Status = session.Status.ToString(),
        PlayerCount = session.ConnectedPlayers.Count,
        Players = session.ConnectedPlayers.Select(p => p.Name)
    };
}
