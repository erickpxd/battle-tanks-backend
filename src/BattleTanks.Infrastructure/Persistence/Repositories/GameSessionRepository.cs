using BattleTanks.Application.Abstractions;
using BattleTanks.Domain.Entities;
using BattleTanks.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Infrastructure.Persistence.Repositories;

public class GameSessionRepository : IGameSessionRepository
{
    private readonly ApplicationDbContext _context;

    public GameSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GameSession>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .AsNoTracking()
            .Include(s => s.ConnectedPlayers)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<GameSession> Items, int TotalCount)> GetWaitingPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.GameSessions
            .AsNoTracking()
            .Where(s => s.Status == GameSessionStatus.Waiting);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(s => s.ConnectedPlayers)
            .OrderBy(s => s.RoomCode)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .Include(s => s.ConnectedPlayers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<GameSession?> GetByRoomCodeAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRoomCode(roomCode);
        // Tracked: join/leave mutates ConnectedPlayers.
        return await _context.GameSessions
            .Include(s => s.ConnectedPlayers)
            .FirstOrDefaultAsync(s => s.RoomCode == normalized, cancellationToken);
    }

    public async Task<bool> RoomCodeExistsAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRoomCode(roomCode);
        return await _context.GameSessions
            .AsNoTracking()
            .AnyAsync(s => s.RoomCode == normalized, cancellationToken);
    }

    public async Task AddAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        session.RoomCode = NormalizeRoomCode(session.RoomCode);
        await _context.GameSessions.AddAsync(session, cancellationToken);
    }

    public Task UpdateAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        _context.GameSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    internal static string NormalizeRoomCode(string roomCode)
        => roomCode.Trim().ToUpperInvariant();
}
