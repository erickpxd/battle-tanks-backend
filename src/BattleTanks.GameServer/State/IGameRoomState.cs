using BattleTanks.Contracts.Realtime;

namespace BattleTanks.GameServer.State;

public interface IGameRoomState
{
    IReadOnlyList<RealtimePlayer> Join(
        string roomCode,
        string connectionId,
        Guid playerId,
        string playerName);

    MoveResult Move(string connectionId, PlayerMoveRequest request, DateTime serverReceivedAt);

    RealtimePlayer Hit(string connectionId, PlayerHitRequest request, DateTime serverReceivedAt);

    PowerUpCollectionResult CollectPowerUp(
        string connectionId,
        CollectPowerUpRequest request,
        DateTime serverReceivedAt);

    PowerUpUpdate UpdatePowerUps(string roomCode, DateTime now);

    IReadOnlyList<RealtimePowerUp> GetPowerUps(string roomCode, DateTime now);

    IReadOnlyList<RoomPowerUpUpdate> UpdateAllPowerUps(DateTime now);

    IReadOnlyList<RealtimePlayer> GetPlayers(string roomCode);

    PlayerDeparture? Leave(string connectionId);
}

public sealed record PlayerDeparture(string RoomCode, IReadOnlyList<RealtimePlayer> RemainingPlayers);

/// <summary>
/// Encapsulates the result of a <see cref="IGameRoomState.Move"/> call.
/// <para>
/// <see cref="WasThrottled"/> is set when the event arrived faster than the minimum
/// inter-event interval (30 ms). The Hub uses this flag to skip the broadcast,
/// but still returns a <see cref="RealtimeAck"/> to the caller so the client
/// does not block waiting for a response.
/// </para>
/// </summary>
public sealed record MoveResult(RealtimePlayer Player, bool WasThrottled);

public sealed record PowerUpUpdate(
    IReadOnlyList<RealtimePowerUp> ActivePowerUps,
    bool Changed);

public sealed record RoomPowerUpUpdate(
    string RoomCode,
    IReadOnlyList<RealtimePowerUp> ActivePowerUps);
