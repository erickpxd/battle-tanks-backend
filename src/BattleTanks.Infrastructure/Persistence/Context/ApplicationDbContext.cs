using BattleTanks.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Infrastructure.Persistence.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<Score> Scores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Ensure table names are explicitly set (optional but good practice)
        modelBuilder.Entity<Player>().ToTable("Players");
        modelBuilder.Entity<GameSession>().ToTable("GameSessions");
        modelBuilder.Entity<Score>().ToTable("Scores");
    }
}
