using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Contracts.Requests.Room;

public class CreateRoomRequest
{
    [Required]
    [RegularExpression(
        "^(battleground|fortress|swamp|ruins|default)$",
        ErrorMessage = "MapSelected must be battleground, fortress, swamp, or ruins.")]
    public required string MapSelected { get; set; }

    /// <summary>
    /// Optional shard/region key (defaults to sa-east-1).
    /// </summary>
    public string? Region { get; set; }
}
