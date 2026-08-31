using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BattleTanks.Application.Chat;
using BattleTanks.Contracts.Requests.Chat;
using BattleTanks.Contracts.Responses.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly ISimulatedChatRepository _chatRepository;

    public ChatController(ISimulatedChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    [HttpGet("{roomCode}")]
    [Authorize]
    public IActionResult GetMessages(string roomCode)
    {
        var messages = _chatRepository.GetMessages(roomCode);
        return Ok(messages);
    }

    [HttpPost("{roomCode}")]
    [Authorize]
    public IActionResult SendMessage(string roomCode, [FromBody] SendChatRequest request)
    {
        var playerId = GetPlayerIdFromToken();
        var playerName = GetPlayerNameFromToken();

        if (playerId is null || string.IsNullOrEmpty(playerName))
            return Unauthorized();

        var message = new ChatResponse
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId.Value,
            PlayerName = playerName,
            Message = request.Message,
            Timestamp = DateTime.UtcNow
        };

        _chatRepository.AddMessage(roomCode, message);
        return Ok(message);
    }

    private Guid? GetPlayerIdFromToken()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private string? GetPlayerNameFromToken()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Name)
            ?? User.FindFirstValue(ClaimTypes.Name);
    }
}
