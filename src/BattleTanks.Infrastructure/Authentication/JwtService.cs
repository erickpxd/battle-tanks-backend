using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BattleTanks.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BattleTanks.Infrastructure.Authentication;

public class JwtService : IJwtService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtService(IConfiguration configuration)
    {
        _key               = configuration["Jwt:Key"]               ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        _issuer            = configuration["Jwt:Issuer"]            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _audience          = configuration["Jwt:Audience"]          ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        _expirationMinutes = int.Parse(configuration["Jwt:ExpirationInMinutes"] ?? "60");
    }

    public string GenerateToken(Guid playerId, string email, string username)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   playerId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name,  username),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
