using BattleTanks.Application.Abstractions;
using BattleTanks.Contracts.Requests.Room;
using BattleTanks.Contracts.Responses.Room;
using BattleTanks.Domain.Entities;

namespace BattleTanks.Application.Lobby;

public class JoinRoomUseCase
{
    private const int MaxPlayersPerRoom = 4;

    private readonly IGameSessionRepository _sessionRepository;
    private readonly IPlayerRepository _playerRepository;

    public JoinRoomUseCase(
        IGameSessionRepository sessionRepository,
        IPlayerRepository playerRepository)
    {
        _sessionRepository = sessionRepository;
        _playerRepository = playerRepository;
    }

    public async Task<JoinRoomResult> ExecuteAsync(
        Guid playerId,
        JoinRoomByCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByRoomCodeAsync(request.RoomCode, cancellationToken);
        if (session is null)
            return JoinRoomResult.NotFound();

        if (session.Status != GameSessionStatus.Waiting)
            return JoinRoomResult.Fail("This room has already started or finished.");

        // Idempotent: player already in the room
        if (session.ConnectedPlayers.Any(p => p.Id == playerId))
            return JoinRoomResult.Success(MapToResponse(session));

        if (session.ConnectedPlayers.Count >= MaxPlayersPerRoom)
            return JoinRoomResult.Fail($"Room is full ({MaxPlayersPerRoom} players maximum).");

        var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
        if (player is null)
            return JoinRoomResult.Fail("Player not found.");

        player.GameSessionId = session.Id;
        session.ConnectedPlayers.Add(player);

        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return JoinRoomResult.Success(MapToResponse(session));
    }

    private static RoomResponse MapToResponse(GameSession session) => new()
    {
        Id          = session.Id,
        RoomCode    = session.RoomCode,
        MapSelected = session.MapSelected,
        Status      = session.Status.ToString(),
        PlayerCount = session.ConnectedPlayers.Count,
        Players     = session.ConnectedPlayers.Select(p => p.Name)
    };
}

/// <summary>
/// Discriminated result — avoids throwing exceptions for expected business outcomes.
/// </summary>
public class JoinRoomResult
{
    public bool Found { get; private init; }
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public RoomResponse? Room { get; private init; }

    public static JoinRoomResult NotFound() => new() { Found = false, Succeeded = false };
    public static JoinRoomResult Fail(string message) => new() { Found = true, Succeeded = false, ErrorMessage = message };
    public static JoinRoomResult Success(RoomResponse room) => new() { Found = true, Succeeded = true, Room = room };
}
