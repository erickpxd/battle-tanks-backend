using System.Security.Claims;
using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Chat;
using BattleTanks.Application.GameEvents;
using BattleTanks.Contracts.GameEvents;
using BattleTanks.Contracts.Realtime;
using BattleTanks.Contracts.Responses.Chat;
using BattleTanks.GameServer.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BattleTanks.GameServer;

[Authorize]
public sealed class GameHub : Hub
{
    public const string Route = "/hubs/game";

    private static readonly HashSet<string> ValidDirections =
        new(StringComparer.OrdinalIgnoreCase) { "up", "down", "left", "right" };

    // Matches frontend BattleMap: 20x15 cells with subdivision 3 → 60x45 expanded tiles.
    private const int ExpandedMapColumns = 60;
    private const int ExpandedMapRows = 45;

    private readonly IGameRoomState _gameRoomState;
    private readonly ISimulatedChatRepository _chatRepository;
    private readonly GameEventDispatcher _gameEventDispatcher;
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly IOnlinePresenceService _onlinePresenceService;

    public GameHub(
        IGameRoomState gameRoomState,
        ISimulatedChatRepository chatRepository,
        GameEventDispatcher gameEventDispatcher,
        IGameSessionRepository gameSessionRepository,
        IOnlinePresenceService onlinePresenceService)
    {
        _gameRoomState = gameRoomState;
        _chatRepository = chatRepository;
        _gameEventDispatcher = gameEventDispatcher;
        _gameSessionRepository = gameSessionRepository;
        _onlinePresenceService = onlinePresenceService;
    }

    public async Task JoinRoom(string roomCode)
    {
        var normalizedRoomCode = NormalizeRoomCode(roomCode);
        var (playerId, playerName) = ReadPlayerIdentity();
        var session = await _gameSessionRepository.GetByRoomCodeAsync(
            normalizedRoomCode,
            Context.ConnectionAborted);
        if (session is null || session.ConnectedPlayers.All(player => player.Id != playerId))
        {
            throw new HubException("The authenticated player does not belong to this room.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, normalizedRoomCode);
        Context.Items["PlayerId"] = playerId;
        var players = _gameRoomState.Join(
            normalizedRoomCode,
            Context.ConnectionId,
            playerId,
            playerName);

        await _onlinePresenceService.PlayerJoinedAsync(
            normalizedRoomCode,
            playerId,
            playerName,
            Context.ConnectionId,
            Context.ConnectionAborted);
        await _onlinePresenceService.SetRoomStatusAsync(
            normalizedRoomCode,
            session.Status.ToString(),
            players.Count,
            Context.ConnectionAborted);

        await Clients.Caller.SendAsync("ReceiveLocalPlayerId", playerId);
        await Clients.Caller.SendAsync(
            "ReceiveChatHistory",
            _chatRepository.GetMessages(normalizedRoomCode));
        await Clients.Caller.SendAsync(
            "ReceiveGameEventHistory",
            await _gameEventDispatcher.GetRecentAsync(
                normalizedRoomCode,
                Context.ConnectionAborted));
        await Clients.Group(normalizedRoomCode).SendAsync("ReceivePlayersUpdated", players);
    }

    public async Task<RealtimeAck> MovePlayer(PlayerMoveRequest request)
    {
        ValidateMove(request);
        var roomCode = GetJoinedRoomCode();
        var serverReceivedAt = DateTime.UtcNow;

        MoveResult result;
        try
        {
            result = _gameRoomState.Move(Context.ConnectionId, request, serverReceivedAt);
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }

        // Throttled moves still receive an ACK so the client does not block,
        // but we skip the group broadcast to avoid redundant state pushes.
        if (!result.WasThrottled)
        {
            await Clients.OthersInGroup(roomCode).SendAsync("ReceivePlayerMove", result.Player);
        }

        return new RealtimeAck(request.EventId, request.ClientSentAtUnixMs, serverReceivedAt);
    }

    public async Task<RealtimeAck> SendChatMessage(RealtimeChatRequest request)
    {
        var messageText = request.Message?.Trim() ?? string.Empty;
        if (messageText.Length is < 1 or > 250)
        {
            throw new HubException("Chat messages must contain between 1 and 250 characters.");
        }

        var roomCode = GetJoinedRoomCode();
        var (playerId, playerName) = ReadPlayerIdentity();
        var serverReceivedAt = DateTime.UtcNow;
        var storedMessage = new ChatResponse
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerName = playerName,
            Message = messageText,
            Timestamp = serverReceivedAt
        };
        _chatRepository.AddMessage(roomCode, storedMessage);

        var realtimeMessage = new RealtimeChatMessage(
            storedMessage.Id,
            storedMessage.PlayerId,
            storedMessage.PlayerName,
            storedMessage.Message,
            storedMessage.Timestamp,
            request.EventId,
            request.ClientSentAtUnixMs,
            serverReceivedAt);

        await Clients.Group(roomCode).SendAsync("ReceiveChatMessage", realtimeMessage);
        return new RealtimeAck(request.EventId, request.ClientSentAtUnixMs, serverReceivedAt);
    }

    public async Task<RealtimeAck> HitPlayer(PlayerHitRequest request)
    {
        if (request.TargetPlayerId == Guid.Empty)
        {
            throw new HubException("A valid target player is required.");
        }

        if (request.Damage is < 1 or > 100)
        {
            throw new HubException("Player damage must be between 1 and 100.");
        }

        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            throw new HubException("An event identifier is required.");
        }

        var roomCode = GetJoinedRoomCode();
        var (playerId, _) = ReadPlayerIdentity();
        var serverReceivedAt = DateTime.UtcNow;

        RealtimePlayer player;
        try
        {
            player = _gameRoomState.Hit(Context.ConnectionId, request, serverReceivedAt);
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }

        var collisionEvent = GameEventEnvelope.Create(
            request.EventId,
            GameEventTypes.PlayerHit,
            roomCode,
            playerId,
            request.ClientSentAtUnixMs,
            new DateTimeOffset(serverReceivedAt),
            new PlayerHitEventData(player.Id, request.Damage, player.Health, player.Status));

        // QoS 1: losing an authoritative damage event would leave critical UI notifications stale.
        await Task.WhenAll(
            Clients.Group(roomCode).SendAsync("ReceivePlayerHit", player),
            _gameEventDispatcher.DispatchAsync(
                collisionEvent,
                GameEventQos.AtLeastOnce,
                CancellationToken.None));

        var players = _gameRoomState.GetPlayers(roomCode);
        var activePlayers = players.Where(candidate => candidate.Status == "active").ToArray();
        if (player.Status == "destroyed" && players.Count > 1 && activePlayers.Length <= 1)
        {
            var endedEvent = GameEventEnvelope.Create(
                Guid.NewGuid().ToString("N"),
                GameEventTypes.GameEnded,
                roomCode,
                playerId,
                new DateTimeOffset(serverReceivedAt).ToUnixTimeMilliseconds(),
                new DateTimeOffset(serverReceivedAt),
                new GameEndedEventData(player.Id, activePlayers.SingleOrDefault()?.Id, request.EventId));

            // QoS 1: game over must be delivered; eventId makes duplicate delivery harmless.
            await _gameEventDispatcher.DispatchAsync(
                endedEvent,
                GameEventQos.AtLeastOnce,
                CancellationToken.None);
        }

        return new RealtimeAck(request.EventId, request.ClientSentAtUnixMs, serverReceivedAt);
    }

    public async Task<RealtimeAck> DestroyMapTile(MapTileChangeRequest request)
    {
        if (request.Column is < 0 or >= ExpandedMapColumns ||
            request.Row is < 0 or >= ExpandedMapRows)
        {
            throw new HubException("Map tile coordinates are outside the expanded game map.");
        }

        var roomCode = GetJoinedRoomCode();
        var (playerId, _) = ReadPlayerIdentity();
        var serverReceivedAt = DateTime.UtcNow;
        var mapEvent = GameEventEnvelope.Create(
            request.EventId,
            GameEventTypes.MapTileDestroyed,
            roomCode,
            playerId,
            request.ClientSentAtUnixMs,
            new DateTimeOffset(serverReceivedAt),
            new MapTileDestroyedEventData(request.Column, request.Row));

        // QoS 0: SignalR remains authoritative for the map and this transient comparison event
        // can favor throughput over retransmission.
        await Task.WhenAll(
            Clients.OthersInGroup(roomCode).SendAsync("ReceiveMapTileDestroyed", request),
            _gameEventDispatcher.DispatchAsync(
                mapEvent,
                GameEventQos.AtMostOnce,
                CancellationToken.None));
        return new RealtimeAck(request.EventId, request.ClientSentAtUnixMs, serverReceivedAt);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var departure = _gameRoomState.Leave(Context.ConnectionId);
        if (departure is not null)
        {
            if (Context.Items["PlayerId"] is Guid playerId)
            {
                await _onlinePresenceService.PlayerLeftAsync(
                    departure.RoomCode,
                    playerId,
                    Context.ConnectionId,
                    CancellationToken.None);
            }

            await _onlinePresenceService.SetRoomStatusAsync(
                departure.RoomCode,
                "Waiting",
                departure.RemainingPlayers.Count,
                CancellationToken.None);

            await Clients.Group(departure.RoomCode)
                .SendAsync("ReceivePlayersUpdated", departure.RemainingPlayers);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string GetJoinedRoomCode()
    {
        var roomCode = Context.Items["RoomCode"] as string;
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            throw new HubException("JoinRoom must be called before sending game events.");
        }

        return roomCode;
    }

    private string NormalizeRoomCode(string roomCode)
    {
        var normalized = roomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is < 3 or > 32 ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new HubException("A valid room code is required.");
        }

        Context.Items["RoomCode"] = normalized;
        return normalized;
    }

    private (Guid PlayerId, string PlayerName) ReadPlayerIdentity()
    {
        var playerIdClaim = Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var playerName = Context.User?.FindFirstValue("name")
            ?? Context.User?.FindFirstValue(ClaimTypes.Name);

        if (!Guid.TryParse(playerIdClaim, out var playerId) || string.IsNullOrWhiteSpace(playerName))
        {
            throw new HubException("The authenticated player identity is invalid.");
        }

        return (playerId, playerName);
    }

    private static void ValidateMove(PlayerMoveRequest request)
    {
        if (request.X is < 0 or >= 20 || request.Y is < 0 or >= 15)
        {
            throw new HubException("Player coordinates are outside the game map.");
        }

        if (!ValidDirections.Contains(request.Direction))
        {
            throw new HubException("Player direction is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            throw new HubException("An event identifier is required.");
        }
    }
}
