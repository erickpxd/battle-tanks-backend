using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Domain.Entities;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hashed password — plain text is never stored.
    /// </summary>
    [MaxLength(72)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int TotalScore { get; set; }

    // Navigation — nullable: a player may not be in a session
    public Guid? GameSessionId { get; set; }
    public GameSession? GameSession { get; set; }
}
