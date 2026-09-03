using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Backend.Dtos.Rooms;

public sealed class CreateRoomRequest
{
    [Required, MinLength(3), MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Range(2, 8)]
    public int MaxPlayers { get; set; } = 4;

    [MaxLength(50)]
    public string? Nickname { get; set; }
}
