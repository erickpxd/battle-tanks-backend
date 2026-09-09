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
}
