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
}
