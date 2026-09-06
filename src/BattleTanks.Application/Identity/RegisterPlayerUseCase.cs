using BattleTanks.Application.Abstractions;
using BattleTanks.Contracts.Requests.Auth;
using BattleTanks.Contracts.Responses.Auth;
using BattleTanks.Domain.Entities;

namespace BattleTanks.Application.Identity;

public class RegisterPlayerUseCase
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public RegisterPlayerUseCase(
        IPlayerRepository playerRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _playerRepository = playerRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Registers a new player and returns a JWT token.
    /// Throws InvalidOperationException if the e-mail is already in use.
    /// </summary>
    public async Task<AuthResponse> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _playerRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("A player with this e-mail already exists.");

        var player = new Player
        {
            Name         = request.Username,
            Email        = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        await _playerRepository.AddAsync(player, cancellationToken);
        await _playerRepository.SaveChangesAsync(cancellationToken);

        var token = _jwtService.GenerateToken(player.Id, player.Email, player.Name);

        return new AuthResponse
        {
            Token    = token,
            PlayerId = player.Id,
            Username = player.Name
        };
    }
}
