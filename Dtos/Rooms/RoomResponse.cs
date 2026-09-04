using BattleTanks.Backend.Domain.Enums;
using BattleTanks.Backend.Dtos.Players;

namespace BattleTanks.Backend.Dtos.Rooms;

public sealed record RoomResponse(
    Guid Id,
    string Code,
    string Name,
    int MaxPlayers,
    int CurrentPlayers,
    RoomStatus Status,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<PlayerResponse> Players);
