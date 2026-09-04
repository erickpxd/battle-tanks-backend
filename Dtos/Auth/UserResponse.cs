namespace BattleTanks.Backend.Dtos.Auth;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    DateTime CreatedAt);
