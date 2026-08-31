using BattleTanks.Application.Identity;
using BattleTanks.Contracts.Requests.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RegisterPlayerUseCase _registerUseCase;
    private readonly LoginPlayerUseCase _loginUseCase;

    public AuthController(RegisterPlayerUseCase registerUseCase, LoginPlayerUseCase loginUseCase)
    {
        _registerUseCase = registerUseCase;
        _loginUseCase    = loginUseCase;
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
}
