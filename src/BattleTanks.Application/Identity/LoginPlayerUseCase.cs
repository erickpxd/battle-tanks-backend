using BattleTanks.Application.Abstractions;
using BattleTanks.Contracts.Requests.Auth;
using BattleTanks.Contracts.Responses.Auth;

namespace BattleTanks.Application.Identity;

public class LoginPlayerUseCase
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginPlayerUseCase(
        IPlayerRepository playerRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _playerRepository = playerRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Authenticates a player and returns a JWT token.
    /// Returns null if credentials are invalid (caller maps this to 401).
    /// </summary>
    public async Task<AuthResponse?> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var player = await _playerRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (player is null)
            return null;

        if (!_passwordHasher.Verify(request.Password, player.PasswordHash))
            return null;

        var token = _jwtService.GenerateToken(player.Id, player.Email, player.Name);

        return new AuthResponse
        {
            Token    = token,
            PlayerId = player.Id,
            Username = player.Name
        };
    }
}
