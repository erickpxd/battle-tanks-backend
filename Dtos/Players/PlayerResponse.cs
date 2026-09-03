using BattleTanks.Backend.Domain.Enums;

namespace BattleTanks.Backend.Dtos.Players;

public sealed record PlayerResponse(
    Guid Id,
    Guid RoomId,
    Guid UserId,
    string Nickname,
    PlayerTeam Team,
    int Health,
    int MaxHealth,
    int Score,
    DateTime JoinedAt);
