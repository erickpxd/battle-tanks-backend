namespace BattleTanks.Application.Abstractions;

public interface IJwtBlacklistService
{
    Task BlacklistAsync(string jti, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default);
}
