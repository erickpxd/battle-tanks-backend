namespace BattleTanks.Contracts.Requests.Room;

public class CreateRoomRequest
{
    public required string MapSelected { get; set; }

    /// <summary>
    /// Optional shard/region key (defaults to sa-east-1).
    /// </summary>
    public string? Region { get; set; }
}
