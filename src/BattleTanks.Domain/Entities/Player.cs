using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Domain.Entities;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(32)]
    public string Password { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int TotalScore { get; set; }
}
