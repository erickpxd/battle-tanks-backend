using BattleTanks.Backend.Dtos;
using BattleTanks.Backend.Dtos.Auth;
using BattleTanks.Backend.Extensions;
using BattleTanks.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Backend.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
                [FromBody] RegisterRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest(new ErrorResponse("Username, email and password are required."));
                }

                var (response, error) = await authService.RegisterAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Conflict(new ErrorResponse(error, "USER_EXISTS"));
                }

                return Results.Created("/api/auth/me", response);
            })
            .WithName("Register")
            .WithSummary("Register a new user")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapPost("/login", async (
                [FromBody] LoginRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var (response, error) = await authService.LoginAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(
                        new ErrorResponse(error, "INVALID_CREDENTIALS"),
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Ok(response);
            })
            .WithName("Login")
            .WithSummary("Authenticate and receive a JWT")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();

        group.MapGet("/me", async (
                HttpContext httpContext,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null)
                {
                    return Results.Json(
                        new ErrorResponse("Missing or invalid access token.", "UNAUTHORIZED"),
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var user = await authService.GetUserAsync(userId.Value, cancellationToken);
                return user is null
                    ? Results.NotFound(new ErrorResponse("User not found."))
                    : Results.Ok(user);
            })
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated user profile")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        return group;
    }
}
