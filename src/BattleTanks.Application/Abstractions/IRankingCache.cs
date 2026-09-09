namespace BattleTanks.Application.Abstractions;

public interface IRankingCache
{
    Task UpsertScoreAsync(Guid playerId, string playerName, int totalScore, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RankingEntry>> GetTopAsync(int take = 10, CancellationToken cancellationToken = default);

    Task RebuildFromAsync(IEnumerable<RankingEntry> entries, CancellationToken cancellationToken = default);
}

public sealed record RankingEntry(Guid PlayerId, string PlayerName, int TotalScore, long Rank);
