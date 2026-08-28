using BattleTanks.Application.Abstractions;
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
    /// Returns all rooms with Waiting status, mapped to RoomResponse.
    /// </summary>
    public async Task<IEnumerable<RoomResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetAllAsync(cancellationToken);

        return sessions
            .Where(s => s.Status == GameSessionStatus.Waiting)
            .Select(s => new RoomResponse
            {
                Id          = s.Id,
                RoomCode    = s.RoomCode,
                MapSelected = s.MapSelected,
                Status      = s.Status.ToString(),
                PlayerCount = s.ConnectedPlayers.Count,
                Players     = s.ConnectedPlayers.Select(p => p.Name)
            });
    }
}
