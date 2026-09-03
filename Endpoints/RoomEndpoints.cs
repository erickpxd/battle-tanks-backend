using BattleTanks.Backend.Dtos;
using BattleTanks.Backend.Dtos.Players;
using BattleTanks.Backend.Dtos.Rooms;
using BattleTanks.Backend.Extensions;
using BattleTanks.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Backend.Endpoints;

public static class RoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms").WithTags("Rooms").RequireAuthorization();

        group.MapGet("/", async (RoomService roomService, CancellationToken cancellationToken) =>
            {
                var rooms = await roomService.ListAsync(cancellationToken);
                return Results.Ok(rooms);
            })
            .WithName("ListRooms")
            .WithSummary("List all game rooms")
            .Produces<IReadOnlyList<RoomResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{roomId:guid}", async (
                Guid roomId,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var room = await roomService.GetByIdAsync(roomId, cancellationToken);
                return room is null
                    ? Results.NotFound(new ErrorResponse("Room not found."))
                    : Results.Ok(room);
            })
            .WithName("GetRoom")
            .WithSummary("Get a room by id")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                [FromBody] CreateRoomRequest request,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (response, error, statusCode) = await roomService.CreateAsync(userId.Value, request, cancellationToken);
                return ToResult(response, error, statusCode);
            })
            .WithName("CreateRoom")
            .WithSummary("Create a new room and join as owner")
            .Produces<RoomResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized);

        group.MapPut("/{roomId:guid}", async (
                Guid roomId,
                [FromBody] UpdateRoomRequest request,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (response, error, statusCode) = await roomService.UpdateAsync(
                    roomId,
                    userId.Value,
                    request,
                    cancellationToken);

                return ToResult(response, error, statusCode);
            })
            .WithName("UpdateRoom")
            .WithSummary("Update room settings (owner only)")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapDelete("/{roomId:guid}", async (
                Guid roomId,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (success, error, statusCode) = await roomService.DeleteAsync(roomId, userId.Value, cancellationToken);
                if (!success)
                {
                    return Results.Json(new ErrorResponse(error ?? "Request failed."), statusCode: statusCode);
                }

                return Results.NoContent();
            })
            .WithName("DeleteRoom")
            .WithSummary("Delete a room (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{roomId:guid}/join", async (
                Guid roomId,
                [FromBody] JoinRoomRequest? request,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (response, error, statusCode) = await roomService.JoinAsync(
                    roomId,
                    userId.Value,
                    request ?? new JoinRoomRequest(),
                    cancellationToken);

                return ToResult(response, error, statusCode);
            })
            .WithName("JoinRoom")
            .WithSummary("Join an existing room")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/{roomId:guid}/leave", async (
                Guid roomId,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (response, error, statusCode) = await roomService.LeaveAsync(roomId, userId.Value, cancellationToken);
                if (statusCode == StatusCodes.Status204NoContent)
                {
                    return Results.NoContent();
                }

                return ToResult(response, error, statusCode);
            })
            .WithName("LeaveRoom")
            .WithSummary("Leave a room")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/{roomId:guid}/players", async (
                Guid roomId,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var players = await roomService.ListPlayersAsync(roomId, cancellationToken);
                return players is null
                    ? Results.NotFound(new ErrorResponse("Room not found."))
                    : Results.Ok(players);
            })
            .WithName("ListRoomPlayers")
            .WithSummary("List players in a room")
            .Produces<IReadOnlyList<PlayerResponse>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPut("/{roomId:guid}/players/{playerId:guid}", async (
                Guid roomId,
                Guid playerId,
                [FromBody] UpdatePlayerRequest request,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (response, error, statusCode) = await roomService.UpdatePlayerAsync(
                    roomId,
                    playerId,
                    userId.Value,
                    request,
                    cancellationToken);

                return response is null
                    ? Results.Json(new ErrorResponse(error ?? "Request failed."), statusCode: statusCode)
                    : Results.Ok(response);
            })
            .WithName("UpdateRoomPlayer")
            .WithSummary("Update player data in a room")
            .Produces<PlayerResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapDelete("/{roomId:guid}/players/{playerId:guid}", async (
                Guid roomId,
                Guid playerId,
                HttpContext httpContext,
                RoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Unauthorized();
                }

                var (success, error, statusCode) = await roomService.RemovePlayerAsync(
                    roomId,
                    playerId,
                    userId.Value,
                    cancellationToken);

                if (!success)
                {
                    return Results.Json(new ErrorResponse(error ?? "Request failed."), statusCode: statusCode);
                }

                return Results.NoContent();
            })
            .WithName("RemoveRoomPlayer")
            .WithSummary("Remove a player from a room")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return group;
    }

    private static IResult Unauthorized() =>
        Results.Json(
            new ErrorResponse("Missing or invalid access token.", "UNAUTHORIZED"),
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult ToResult(RoomResponse? response, string? error, int statusCode)
    {
        if (response is not null)
        {
            return statusCode == StatusCodes.Status201Created
                ? Results.Created($"/api/rooms/{response.Id}", response)
                : Results.Ok(response);
        }

        return Results.Json(new ErrorResponse(error ?? "Request failed."), statusCode: statusCode);
    }
}
