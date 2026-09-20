namespace BattleTanks.Contracts.Responses.Ranking;

public sealed record RankingEntryResponse(
    long Rank,
    Guid PlayerId,
    string PlayerName,
    int TotalScore);
