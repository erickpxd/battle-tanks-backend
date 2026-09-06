using BattleTanks.Contracts.GameEvents;

namespace BattleTanks.Application.GameEvents;

public interface IGameEventHistory
{
    Task AppendAsync(
        GameEventEnvelope gameEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameEventEnvelope>> GetRecentAsync(
        string roomId,
        CancellationToken cancellationToken = default);
}
