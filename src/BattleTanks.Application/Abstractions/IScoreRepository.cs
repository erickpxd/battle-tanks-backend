using BattleTanks.Domain.Entities;

namespace BattleTanks.Application.Abstractions;

public interface IScoreRepository
{
    Task BulkInsertAsync(IEnumerable<Score> scores, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Score>> GetTopScoresAsync(
        int take,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecordedPlayerScore>> RecordMatchResultAsync(
        string roomCode,
        IReadOnlyCollection<Guid> participantIds,
        Guid? winnerPlayerId,
        int victoryPoints,
        DateTime completedAt,
        CancellationToken cancellationToken = default);
}

public sealed record RecordedPlayerScore(
    Guid PlayerId,
    string PlayerName,
    int PointsAwarded,
    int TotalScore,
    int GamesPlayed,
    int Wins);
