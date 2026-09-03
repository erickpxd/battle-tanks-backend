using BattleTanks.Backend.Domain.Enums;

namespace BattleTanks.Backend.Domain.Entities;

public class RoomPlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public PlayerTeam Team { get; set; } = PlayerTeam.Neutral;
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int Score { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Room Room { get; set; } = null!;
    public User User { get; set; } = null!;
}
