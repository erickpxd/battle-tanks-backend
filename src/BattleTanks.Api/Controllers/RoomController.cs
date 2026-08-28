using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BattleTanks.Application.Lobby;
using BattleTanks.Contracts.Requests.Room;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Api.Controllers;

[ApiController]
[Route("api/room")]
[Produces("application/json")]
public class RoomController : ControllerBase
{
    private readonly ListRoomsUseCase _listRoomsUseCase;
    private readonly CreateRoomUseCase _createRoomUseCase;
    private readonly JoinRoomUseCase _joinRoomUseCase;

    public RoomController(
        ListRoomsUseCase listRoomsUseCase,
        CreateRoomUseCase createRoomUseCase,
        JoinRoomUseCase joinRoomUseCase)
    {
        _listRoomsUseCase  = listRoomsUseCase;
        _createRoomUseCase = createRoomUseCase;
        _joinRoomUseCase   = joinRoomUseCase;
    }

    /// <summary>
    /// Returns all available rooms with Waiting status. No authentication required.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRooms(CancellationToken cancellationToken)
    {
        var rooms = await _listRoomsUseCase.ExecuteAsync(cancellationToken);
        return Ok(rooms);
    }

    /// <summary>
    /// Creates a new room. The authenticated player is automatically added as the first member.
    /// Returns the room with its friendly RoomCode to share with friends.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var playerId = GetPlayerIdFromToken();
        if (playerId is null)
            return Unauthorized();

        var room = await _createRoomUseCase.ExecuteAsync(playerId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(ListRooms), new { id = room.Id }, room);
    }

    /// <summary>
    /// Joins an existing room using its friendly RoomCode (e.g. "ABCD-1234").
    /// Maximum 4 players per room. Room must be in Waiting status.
    /// </summary>
    [HttpPut("join")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomByCodeRequest request, CancellationToken cancellationToken)
    {
        var playerId = GetPlayerIdFromToken();
        if (playerId is null)
            return Unauthorized();

        var result = await _joinRoomUseCase.ExecuteAsync(playerId.Value, request, cancellationToken);

        if (!result.Found)
            return NotFound(new { error = $"Room with code '{request.RoomCode}' not found." });

        if (!result.Succeeded)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Room);
    }

    /// <summary>
    /// Extracts the PlayerId from the JWT sub claim. Returns null if missing or invalid.
    /// </summary>
    private Guid? GetPlayerIdFromToken()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
