using BattleTanks.Application.Abstractions;
using BattleTanks.Domain.Entities;
using BattleTanks.Infrastructure.Persistence.Context;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Infrastructure.Persistence.Repositories;

public class ScoreRepository : IScoreRepository
{
    private readonly ApplicationDbContext _context;

    public ScoreRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(IEnumerable<Score> scores, CancellationToken cancellationToken = default)
    {
        var batch = scores.ToList();
        if (batch.Count == 0)
        {
            return;
        }

        await _context.BulkInsertAsync(batch, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Score>> GetTopScoresAsync(
        int take,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Scores.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .OrderByDescending(score => score.Points)
            .ThenByDescending(score => score.Date)
            .Take(Math.Clamp(take, 1, 1_000))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _context.Scores.AsNoTracking().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<RecordedPlayerScore>> RecordMatchResultAsync(
        string roomCode,
        IReadOnlyCollection<Guid> participantIds,
        Guid? winnerPlayerId,
        int victoryPoints,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoomCode = roomCode.Trim().ToUpperInvariant();
        var session = await _context.GameSessions
            .SingleOrDefaultAsync(
                item => item.RoomCode == normalizedRoomCode,
                cancellationToken);
        if (session is null || session.Status != GameSessionStatus.InGame)
        {
            return [];
        }

        var uniqueParticipantIds = participantIds.Distinct().ToArray();
        var players = await _context.Players
            .Where(player => uniqueParticipantIds.Contains(player.Id))
            .ToListAsync(cancellationToken);

        session.Status = GameSessionStatus.Finished;
        foreach (var player in players)
        {
            var points = player.Id == winnerPlayerId ? victoryPoints : 0;
            player.GamesPlayed += 1;
            if (player.Id == winnerPlayerId)
            {
                player.Wins += 1;
                player.TotalScore += points;
            }
            _context.Scores.Add(new Score
            {
                PlayerId = player.Id,
                Points = points,
                Date = completedAt
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return players
            .Select(player => new RecordedPlayerScore(
                player.Id,
                player.Name,
                player.Id == winnerPlayerId ? victoryPoints : 0,
                player.TotalScore,
                player.GamesPlayed,
                player.Wins))
            .ToArray();
    }
}
