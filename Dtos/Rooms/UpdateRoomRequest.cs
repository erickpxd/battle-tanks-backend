using System.ComponentModel.DataAnnotations;
using BattleTanks.Backend.Domain.Enums;

namespace BattleTanks.Backend.Dtos.Rooms;

public sealed class UpdateRoomRequest
{
    [Required, MinLength(3), MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Range(2, 8)]
    public int MaxPlayers { get; set; } = 4;

    [Required]
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
}
