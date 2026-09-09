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

        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("Players");
            // Login/register lookup — store normalized lowercase e-mails.
            entity.HasIndex(player => player.Email)
                .IsUnique()
                .HasDatabaseName("IX_Players_Email");
            entity.HasIndex(player => player.TotalScore)
                .HasDatabaseName("IX_Players_TotalScore");
        });

        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.ToTable("GameSessions");
            // Join-by-code path — store uppercase codes (e.g. ABCD-1234).
            entity.HasIndex(session => session.RoomCode)
                .IsUnique()
                .HasDatabaseName("IX_GameSessions_RoomCode");
            entity.HasIndex(session => session.Status)
                .HasDatabaseName("IX_GameSessions_Status");
            entity.HasIndex(session => session.Region)
                .HasDatabaseName("IX_GameSessions_Region");
            entity.Property(session => session.Region)
                .HasMaxLength(32)
                .HasDefaultValue("sa-east-1");
        });

        modelBuilder.Entity<Score>(entity =>
        {
            entity.ToTable("Scores");
            entity.HasIndex(score => score.Points)
                .HasDatabaseName("IX_Scores_Points");
            entity.HasIndex(score => new { score.PlayerId, score.Date })
                .HasDatabaseName("IX_Scores_PlayerId_Date");
        });
    }
}
