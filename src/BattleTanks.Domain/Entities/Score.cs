using System;

namespace BattleTanks.Domain.Entities;

public class Score
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Foreign key to Player
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }
    
    public int Points { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
