using BattleTanks.Backend.Data;
using BattleTanks.Backend.Domain.Entities;
using BattleTanks.Backend.Dtos.Auth;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Backend.Services;

public sealed class AuthService(AppDbContext db, JwtTokenService jwtTokenService)
{
    public async Task<(AuthResponse? Response, string? Error)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(
            u => u.Username == username || u.Email == email,
            cancellationToken);

        if (exists)
        {
            return (null, "Username or email already registered.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? username
                : request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return (BuildAuthResponse(user), null);
    }

    public async Task<(AuthResponse? Response, string? Error)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var login = request.UsernameOrEmail.Trim();
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Username == login || u.Email == login.ToLowerInvariant(),
            cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return (null, "Invalid username/email or password.");
        }

        return (BuildAuthResponse(user), null);
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null
            ? null
            : new UserResponse(user.Id, user.Username, user.DisplayName, user.Email, user.CreatedAt);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var (token, expiresAt) = jwtTokenService.CreateToken(user);
        return new AuthResponse(user.Id, user.Username, user.DisplayName, user.Email, token, expiresAt);
    }
}
