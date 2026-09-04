using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Backend.Dtos.Rooms;

public sealed class JoinRoomRequest
{
    [MaxLength(12)]
    public string? Code { get; set; }

    [MaxLength(50)]
    public string? Nickname { get; set; }
}
