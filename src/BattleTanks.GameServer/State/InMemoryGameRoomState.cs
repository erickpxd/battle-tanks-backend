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

    private static readonly (int X, int Y)[] PowerUpSpawnPoints =
    [
        (4, 3), (10, 2), (15, 3),
        (4, 7), (10, 7), (15, 7),
        (4, 11), (10, 12), (15, 11)
    ];

    private static readonly string[] PowerUpTypePool =
    [
        PowerUpTypes.MachineGun,
        PowerUpTypes.DoubleDamage,
        PowerUpTypes.Shield,
        PowerUpTypes.SpeedBoost,
        PowerUpTypes.RepairKit
    ];

    private static readonly TimeSpan PowerUpLifetime = TimeSpan.FromSeconds(15);

    private readonly object _sync = new();
    private readonly Dictionary<string, Dictionary<Guid, PlayerEntry>> _rooms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConnectionMembership> _connections = [];
    private readonly Dictionary<string, HashSet<string>> _processedHitEventIds =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoomPowerUpState> _powerUps =
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
            EnsurePowerUpRoom(roomCode, DateTime.UtcNow);
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

            var now = serverReceivedAt;
            NormalizeEffects(target, now);

            var remainingDamage = request.Damage;
            var shieldHealth = target.Player.ShieldHealth;
            if (shieldHealth > 0 && target.Player.ShieldExpiresAt > now)
            {
                var absorbed = Math.Min(shieldHealth, remainingDamage);
                shieldHealth -= absorbed;
                remainingDamage -= absorbed;
            }

            var health = Math.Max(target.Player.Health - remainingDamage, 0);
            target.Player = target.Player with
            {
                Health = health,
                Status = health == 0 ? "destroyed" : "active",
                ShieldHealth = shieldHealth,
                ShieldExpiresAt = shieldHealth > 0 ? target.Player.ShieldExpiresAt : null,
                EventId = request.EventId,
                ClientSentAtUnixMs = request.ClientSentAtUnixMs,
                ServerReceivedAt = serverReceivedAt
            };

            return target.Player;
        }
    }

    public PowerUpCollectionResult CollectPowerUp(
        string connectionId,
        CollectPowerUpRequest request,
        DateTime serverReceivedAt)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var membership) ||
                !_rooms.TryGetValue(membership.RoomCode, out var players) ||
                !players.TryGetValue(membership.PlayerId, out var player))
            {
                throw new InvalidOperationException("The connection has not joined a game room.");
            }

            var roomPowerUps = EnsurePowerUpRoom(membership.RoomCode, serverReceivedAt);
            RemoveExpiredPowerUps(roomPowerUps, serverReceivedAt);
            if (!roomPowerUps.Active.Remove(request.PowerUpId, out var powerUp))
            {
                throw new InvalidOperationException("The power-up is no longer available.");
            }
            if (player.Player.Position.X != powerUp.X || player.Player.Position.Y != powerUp.Y)
            {
                roomPowerUps.Active[powerUp.Id] = powerUp;
                throw new InvalidOperationException("The player is not on the power-up position.");
            }

            NormalizeEffects(player, serverReceivedAt);
            player.Player = ApplyPowerUp(player.Player, powerUp.Type, serverReceivedAt) with
            {
                EventId = request.EventId,
                ClientSentAtUnixMs = request.ClientSentAtUnixMs,
                ServerReceivedAt = serverReceivedAt
            };
            roomPowerUps.NextSpawnAt = serverReceivedAt.AddSeconds(Random.Shared.Next(4, 9));
            return new PowerUpCollectionResult(player.Player, SnapshotPowerUps(roomPowerUps));
        }
    }

    public PowerUpUpdate UpdatePowerUps(string roomCode, DateTime now)
    {
        lock (_sync)
        {
            var state = EnsurePowerUpRoom(roomCode, now);
            var changed = RemoveExpiredPowerUps(state, now);
            if (state.Active.Count == 0 && now >= state.NextSpawnAt)
            {
                var occupied = _rooms.TryGetValue(roomCode, out var players)
                    ? players.Values.Select(entry => (entry.Player.Position.X, entry.Player.Position.Y)).ToHashSet()
                    : [];
                var candidates = PowerUpSpawnPoints.Where(point => !occupied.Contains(point)).ToArray();
                if (candidates.Length > 0)
                {
                    var point = candidates[Random.Shared.Next(candidates.Length)];
                    var type = PowerUpTypePool[Random.Shared.Next(PowerUpTypePool.Length)];
                    var powerUp = new RealtimePowerUp(
                        Guid.NewGuid(), type, point.X, point.Y, now.Add(PowerUpLifetime));
                    state.Active[powerUp.Id] = powerUp;
                    changed = true;
                }
            }
            return new PowerUpUpdate(SnapshotPowerUps(state), changed);
        }
    }

    public IReadOnlyList<RealtimePowerUp> GetPowerUps(string roomCode, DateTime now) =>
        UpdatePowerUps(roomCode, now).ActivePowerUps;

    public IReadOnlyList<RoomPowerUpUpdate> UpdateAllPowerUps(DateTime now)
    {
        lock (_sync)
        {
            return _rooms.Keys
                .Select(roomCode => (RoomCode: roomCode, Update: UpdatePowerUps(roomCode, now)))
                .Where(result => result.Update.Changed)
                .Select(result => new RoomPowerUpUpdate(
                    result.RoomCode,
                    result.Update.ActivePowerUps))
                .ToArray();
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
            _powerUps.Remove(membership.RoomCode);
        }

        return new PlayerDeparture(membership.RoomCode, snapshot);
    }

    private static IReadOnlyList<RealtimePlayer> Snapshot(
        Dictionary<Guid, PlayerEntry> players) => players.Values
            .Select(entry => entry.Player)
            .ToArray();

    private RoomPowerUpState EnsurePowerUpRoom(string roomCode, DateTime now)
    {
        if (_powerUps.TryGetValue(roomCode, out var state))
        {
            return state;
        }
        state = new RoomPowerUpState(now.AddSeconds(Random.Shared.Next(2, 5)));
        _powerUps[roomCode] = state;
        return state;
    }

    private static bool RemoveExpiredPowerUps(RoomPowerUpState state, DateTime now)
    {
        var expired = state.Active.Values.Where(item => item.ExpiresAt <= now).Select(item => item.Id).ToArray();
        foreach (var id in expired)
        {
            state.Active.Remove(id);
        }
        if (expired.Length > 0)
        {
            state.NextSpawnAt = now.AddSeconds(Random.Shared.Next(4, 9));
        }
        return expired.Length > 0;
    }

    private static IReadOnlyList<RealtimePowerUp> SnapshotPowerUps(RoomPowerUpState state) =>
        state.Active.Values.ToArray();

    private static RealtimePlayer ApplyPowerUp(RealtimePlayer player, string type, DateTime now) =>
        type switch
        {
            PowerUpTypes.RepairKit => player with { Health = Math.Min(player.Health + 30, player.MaxHealth) },
            PowerUpTypes.Shield => player with { ShieldHealth = 50, ShieldExpiresAt = now.AddSeconds(8) },
            PowerUpTypes.SpeedBoost => player with { SpeedBoostExpiresAt = now.AddSeconds(7) },
            PowerUpTypes.DoubleDamage => player with { DoubleDamageExpiresAt = now.AddSeconds(6) },
            PowerUpTypes.MachineGun => player with { MachineGunExpiresAt = now.AddSeconds(8) },
            _ => throw new InvalidOperationException("Unknown power-up type.")
        };

    private static void NormalizeEffects(PlayerEntry entry, DateTime now)
    {
        var player = entry.Player;
        entry.Player = player with
        {
            ShieldHealth = player.ShieldExpiresAt > now ? player.ShieldHealth : 0,
            ShieldExpiresAt = player.ShieldExpiresAt > now ? player.ShieldExpiresAt : null,
            SpeedBoostExpiresAt = player.SpeedBoostExpiresAt > now ? player.SpeedBoostExpiresAt : null,
            DoubleDamageExpiresAt = player.DoubleDamageExpiresAt > now ? player.DoubleDamageExpiresAt : null,
            MachineGunExpiresAt = player.MachineGunExpiresAt > now ? player.MachineGunExpiresAt : null
        };
    }

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

    private sealed class RoomPowerUpState(DateTime nextSpawnAt)
    {
        public Dictionary<Guid, RealtimePowerUp> Active { get; } = [];
        public DateTime NextSpawnAt { get; set; } = nextSpawnAt;
    }
}
