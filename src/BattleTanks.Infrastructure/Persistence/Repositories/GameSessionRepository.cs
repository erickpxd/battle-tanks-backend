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
            .Include(s => s.ConnectedPlayers)
            .ToListAsync(cancellationToken);

    public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .Include(s => s.ConnectedPlayers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<GameSession?> GetByRoomCodeAsync(string roomCode, CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .Include(s => s.ConnectedPlayers)
            .FirstOrDefaultAsync(
                s => s.RoomCode.ToUpper() == roomCode.ToUpper(),
                cancellationToken);

    public async Task<bool> RoomCodeExistsAsync(string roomCode, CancellationToken cancellationToken = default)
        => await _context.GameSessions
            .AnyAsync(s => s.RoomCode.ToUpper() == roomCode.ToUpper(), cancellationToken);

    public async Task AddAsync(GameSession session, CancellationToken cancellationToken = default)
        => await _context.GameSessions.AddAsync(session, cancellationToken);

    public Task UpdateAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        _context.GameSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
