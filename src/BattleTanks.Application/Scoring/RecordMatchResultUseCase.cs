using BattleTanks.Application.Abstractions;

namespace BattleTanks.Application.Scoring;

public sealed class RecordMatchResultUseCase
{
    public const int VictoryPoints = 100;

    private readonly IScoreRepository _scoreRepository;
    private readonly IRankingCache _rankingCache;

    public RecordMatchResultUseCase(
        IScoreRepository scoreRepository,
        IRankingCache rankingCache)
    {
        _scoreRepository = scoreRepository;
        _rankingCache = rankingCache;
    }

    public async Task<IReadOnlyList<RecordedPlayerScore>> ExecuteAsync(
        string roomCode,
        IReadOnlyCollection<Guid> participantIds,
        Guid? winnerPlayerId,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        var results = await _scoreRepository.RecordMatchResultAsync(
            roomCode,
            participantIds,
            winnerPlayerId,
            VictoryPoints,
            completedAt,
            cancellationToken);

        foreach (var result in results)
        {
            await _rankingCache.UpsertScoreAsync(
                result.PlayerId,
                result.PlayerName,
                result.TotalScore,
                cancellationToken);
        }

        return results;
    }
}
