namespace BattleTanks.Contracts.Responses.Room;

public class RoomResponse
{
    public required Guid Id { get; set; }

    /// <summary>
    /// Short friendly code (e.g. TANK-4X9Z) that players share with friends to join.
    /// </summary>
    public required string RoomCode { get; set; }

    public required string MapSelected { get; set; }

    /// <summary>
    /// Room status: Waiting, InGame, Finished
    /// </summary>
    public required string Status { get; set; }

    public required int PlayerCount { get; set; }
    public required IEnumerable<string> Players { get; set; }
}
