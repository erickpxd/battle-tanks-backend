namespace BattleTanks.Backend.Dtos.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string Email,
    string AccessToken,
    DateTime ExpiresAt);
