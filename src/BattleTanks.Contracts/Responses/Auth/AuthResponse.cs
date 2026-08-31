namespace BattleTanks.Contracts.Responses.Auth;

public class AuthResponse
{
    public required string Token { get; set; }
    public required Guid PlayerId { get; set; }
    public required string Username { get; set; }
}
