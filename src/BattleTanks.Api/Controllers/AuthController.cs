using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Identity;
using BattleTanks.Contracts.Requests.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RegisterPlayerUseCase _registerUseCase;
    private readonly LoginPlayerUseCase _loginUseCase;
    private readonly IJwtBlacklistService _jwtBlacklistService;

    public AuthController(
        RegisterPlayerUseCase registerUseCase,
        LoginPlayerUseCase loginUseCase,
        IJwtBlacklistService jwtBlacklistService)
    {
        _registerUseCase = registerUseCase;
        _loginUseCase = loginUseCase;
        _jwtBlacklistService = jwtBlacklistService;
    }

    /// <summary>
    /// Registers a new player and returns a JWT token.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _registerUseCase.ExecuteAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Register), response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Authenticates a player and returns a JWT token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _loginUseCase.ExecuteAsync(request, cancellationToken);
        if (response is null)
            return Unauthorized(new { error = "Invalid e-mail or password." });

        return Ok(response);
    }

    /// <summary>
    /// Revokes the current JWT by storing its jti in the Redis blacklist until expiry.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrWhiteSpace(jti))
        {
            return Unauthorized(new { error = "Token is missing a jti claim." });
        }

        var expires = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var ttl = TimeSpan.FromHours(1);
        if (long.TryParse(expires, out var expUnix))
        {
            var remaining = DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                ttl = remaining;
            }
        }

        await _jwtBlacklistService.BlacklistAsync(jti, ttl, cancellationToken);
        return NoContent();
    }
}
