using BattleTanks.Contracts.Realtime;

namespace BattleTanks.GameServer.State;

public interface IGameRoomState
{
    IReadOnlyList<RealtimePlayer> Join(
        string roomCode,
        string connectionId,
        Guid playerId,
        string playerName);

    RealtimePlayer Move(string connectionId, PlayerMoveRequest request, DateTime serverReceivedAt);

    RealtimePlayer Hit(string connectionId, PlayerHitRequest request, DateTime serverReceivedAt);

    IReadOnlyList<RealtimePlayer> GetPlayers(string roomCode);

    PlayerDeparture? Leave(string connectionId);
}

public sealed record PlayerDeparture(string RoomCode, IReadOnlyList<RealtimePlayer> RemainingPlayers);
