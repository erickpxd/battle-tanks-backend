using BattleTanks.Application.Abstractions;
using BattleTanks.Domain.Entities;
using BattleTanks.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Infrastructure.Persistence.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly ApplicationDbContext _context;

    public PlayerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Players.FindAsync([id], cancellationToken);

    public async Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _context.Players
            .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default)
        => await _context.Players.AddAsync(player, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
