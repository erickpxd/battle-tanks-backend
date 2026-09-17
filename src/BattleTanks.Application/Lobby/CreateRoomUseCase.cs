using BattleTanks.Application.Abstractions;
using BattleTanks.Contracts.Requests.Room;
using BattleTanks.Contracts.Responses.Room;
using BattleTanks.Domain.Entities;

namespace BattleTanks.Application.Lobby;

public class CreateRoomUseCase
{
    private static readonly HashSet<string> SupportedMaps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "battleground", "fortress", "swamp", "ruins"
        };
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IPlayerRepository _playerRepository;

    public CreateRoomUseCase(
        IGameSessionRepository sessionRepository,
        IPlayerRepository playerRepository)
    {
        _sessionRepository = sessionRepository;
        _playerRepository = playerRepository;
    }

    /// <summary>
    /// Creates a new room, auto-joins the creator, and returns RoomResponse with the friendly RoomCode.
    /// </summary>
    public async Task<RoomResponse> ExecuteAsync(
        Guid creatorPlayerId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var creator = await _playerRepository.GetByIdAsync(creatorPlayerId, cancellationToken)
            ?? throw new InvalidOperationException("Player not found.");

        var mapSelected = request.MapSelected?.Trim().ToLowerInvariant() ?? string.Empty;
        mapSelected = mapSelected == "default" ? "battleground" : mapSelected;
        if (!SupportedMaps.Contains(mapSelected))
        {
            throw new ArgumentException("Unsupported map selection.", nameof(request));
        }

        var roomCode = await GenerateUniqueRoomCodeAsync(cancellationToken);

        var session = new GameSession
        {
            RoomCode    = roomCode,
            MapSelected = mapSelected,
            Region      = string.IsNullOrWhiteSpace(request.Region) ? "sa-east-1" : request.Region.Trim().ToLowerInvariant(),
            Status      = GameSessionStatus.Waiting
        };

        session.ConnectedPlayers.Add(creator);
        creator.GameSessionId = session.Id;

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return new RoomResponse
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
    /// Generates a random 9-char code (XXXX-XXXX format) and ensures uniqueness.
    /// </summary>
    private async Task<string> GenerateUniqueRoomCodeAsync(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I to avoid confusion
        string code;

        do
        {
            var part1 = new string(Enumerable.Range(0, 4).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
            var part2 = new string(Enumerable.Range(0, 4).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
            code = $"{part1}-{part2}";
        }
        while (await _sessionRepository.RoomCodeExistsAsync(code, cancellationToken));

        return code;
    }
}
