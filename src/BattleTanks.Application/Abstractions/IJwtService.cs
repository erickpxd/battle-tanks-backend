using BattleTanks.Contracts.Responses.Auth;

namespace BattleTanks.Application.Abstractions;

public interface IJwtService
{
    /// <summary>
    /// Generates a signed JWT token for the given player.
    /// </summary>
    string GenerateToken(Guid playerId, string email, string username);
}
