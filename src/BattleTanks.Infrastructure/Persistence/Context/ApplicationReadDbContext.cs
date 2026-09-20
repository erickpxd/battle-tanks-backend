using BattleTanks.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Infrastructure.Persistence.Context;

/// <summary>
/// Read-only DbContext wired to the read connection string (replica when available).
/// </summary>
public sealed class ApplicationReadDbContext : DbContext
{
    public ApplicationReadDbContext(DbContextOptions<ApplicationReadDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Score> Scores => Set<Score>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>().ToTable("Players");
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.ToTable("GameSessions");
            entity.Property(session => session.Region)
                .HasMaxLength(32)
                .HasDefaultValue("sa-east-1");
        });
        modelBuilder.Entity<Score>().ToTable("Scores");
    }

    public override int SaveChanges()
        => throw new InvalidOperationException("ApplicationReadDbContext is read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("ApplicationReadDbContext is read-only.");
}
