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
    {
        var normalized = NormalizeEmail(email);
        // Read-only auth lookup — skip change tracker overhead.
        return await _context.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == normalized, cancellationToken);
    }

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default)
    {
        player.Email = NormalizeEmail(player.Email);
        await _context.Players.AddAsync(player, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    internal static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
