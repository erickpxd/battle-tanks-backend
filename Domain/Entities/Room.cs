using BattleTanks.Backend.Domain.Enums;

namespace BattleTanks.Backend.Domain.Entities;

public class Room
{
    public const int DefaultMaxPlayers = 4;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = DefaultMaxPlayers;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<RoomPlayer> Players { get; set; } = [];

    public bool IsFull => Players.Count >= MaxPlayers;
}
