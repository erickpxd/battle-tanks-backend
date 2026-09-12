using BattleTanks.Contracts.Realtime;

namespace BattleTanks.GameServer.State;

public sealed class InMemoryGameRoomState : IGameRoomState
{
    private static readonly PlayerPosition[] SpawnPositions =
    [
        new(1, 1, "down"),
        new(18, 1, "left"),
        new(1, 13, "right"),
        new(18, 13, "up")
    ];

    private readonly object _sync = new();
    private readonly Dictionary<string, Dictionary<Guid, PlayerEntry>> _rooms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConnectionMembership> _connections = [];
    private readonly Dictionary<string, HashSet<string>> _processedHitEventIds =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RealtimePlayer> Join(
        string roomCode,
        string connectionId,
        Guid playerId,
        string playerName)
    {
        lock (_sync)
        {
            RemoveConnection(connectionId);

            if (!_rooms.TryGetValue(roomCode, out var players))
            {
                players = [];
                _rooms[roomCode] = players;
            }

            if (!players.TryGetValue(playerId, out var entry))
            {
                var spawn = SpawnPositions[players.Count % SpawnPositions.Length];
                entry = new PlayerEntry(new RealtimePlayer(playerId, playerName, spawn));
                players[playerId] = entry;
            }

            entry.ConnectionIds.Add(connectionId);
            _connections[connectionId] = new ConnectionMembership(roomCode, playerId);
            return Snapshot(players);
        }
    }

    /// <summary>
    /// Updates the player's position. Returns a <see cref="MoveResult"/> indicating
    /// whether the move was accepted or throttled.
    /// <para>
    /// Moves arriving within 30 ms of the last accepted move are throttled: the state
    /// is not mutated and the caller is informed via <see cref="MoveResult.WasThrottled"/>.
    /// This prevents lock contention and redundant SignalR broadcasts on the hot path.
    /// </para>
    /// </summary>
    public MoveResult Move(
        string connectionId,
        PlayerMoveRequest request,
        DateTime serverReceivedAt)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var membership) ||
                !_rooms.TryGetValue(membership.RoomCode, out var players) ||
                !players.TryGetValue(membership.PlayerId, out var entry))
            {
                throw new InvalidOperationException("The connection has not joined a game room.");
            }

            // Throttle: drop events arriving faster than 30 ms from the last accepted move.
            const long MinIntervalMs = 30;
            if (request.ClientSentAtUnixMs - entry.LastMovedAtUnixMs < MinIntervalMs)
            {
                return new MoveResult(entry.Player, WasThrottled: true);
            }

            entry.Player = entry.Player with
            {
                Position = new PlayerPosition(request.X, request.Y, request.Direction),
                EventId = request.EventId,
                ClientSentAtUnixMs = request.ClientSentAtUnixMs,
                ServerReceivedAt = serverReceivedAt
            };
            entry.LastMovedAtUnixMs = request.ClientSentAtUnixMs;

            return new MoveResult(entry.Player, WasThrottled: false);
        }
    }

    public RealtimePlayer Hit(
        string connectionId,
        PlayerHitRequest request,
        DateTime serverReceivedAt)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var membership) ||
                !_rooms.TryGetValue(membership.RoomCode, out var players))
            {
                throw new InvalidOperationException("The connection has not joined a game room.");
            }

            if (membership.PlayerId == request.TargetPlayerId)
            {
                throw new InvalidOperationException("A player cannot hit their own tank.");
            }

            if (!players.TryGetValue(request.TargetPlayerId, out var target))
            {
                throw new InvalidOperationException("The target player is not connected to this room.");
            }

            if (target.Player.Status == "destroyed")
            {
                throw new InvalidOperationException("The target player is already destroyed.");
            }

            if (!_processedHitEventIds.TryGetValue(membership.RoomCode, out var processedEvents))
            {
                processedEvents = [];
                _processedHitEventIds[membership.RoomCode] = processedEvents;
            }
            if (!processedEvents.Add(request.EventId))
            {
                return target.Player;
            }

            var health = Math.Max(target.Player.Health - request.Damage, 0);
            target.Player = target.Player with
            {
                Health = health,
                Status = health == 0 ? "destroyed" : "active",
                EventId = request.EventId,
                ClientSentAtUnixMs = request.ClientSentAtUnixMs,
                ServerReceivedAt = serverReceivedAt
            };

            return target.Player;
        }
    }

    public IReadOnlyList<RealtimePlayer> GetPlayers(string roomCode)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomCode, out var players)
                ? Snapshot(players)
                : [];
        }
    }

    public PlayerDeparture? Leave(string connectionId)
    {
        lock (_sync)
        {
            return RemoveConnection(connectionId);
        }
    }

    private PlayerDeparture? RemoveConnection(string connectionId)
    {
        if (!_connections.Remove(connectionId, out var membership) ||
            !_rooms.TryGetValue(membership.RoomCode, out var players) ||
            !players.TryGetValue(membership.PlayerId, out var entry))
        {
            return null;
        }

        entry.ConnectionIds.Remove(connectionId);
        if (entry.ConnectionIds.Count == 0)
        {
            players.Remove(membership.PlayerId);
        }

        var snapshot = Snapshot(players);
        if (players.Count == 0)
        {
            _rooms.Remove(membership.RoomCode);
            _processedHitEventIds.Remove(membership.RoomCode);
        }

        return new PlayerDeparture(membership.RoomCode, snapshot);
    }

    private static IReadOnlyList<RealtimePlayer> Snapshot(
        Dictionary<Guid, PlayerEntry> players) => players.Values
            .Select(entry => entry.Player)
            .ToArray();

    private sealed class PlayerEntry(RealtimePlayer player)
    {
        public RealtimePlayer Player { get; set; } = player;
        public HashSet<string> ConnectionIds { get; } = [];

        /// <summary>
        /// Timestamp (Unix ms) of the last accepted <see cref="IGameRoomState.Move"/> call.
        /// Used by the throttle logic to discard moves arriving faster than 30 ms.
        /// </summary>
        public long LastMovedAtUnixMs { get; set; }
    }

    private sealed record ConnectionMembership(string RoomCode, Guid PlayerId);
}
