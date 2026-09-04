using BattleTanks.Backend.Data;
using BattleTanks.Backend.Domain.Entities;
using BattleTanks.Backend.Domain.Enums;
using BattleTanks.Backend.Dtos.Players;
using BattleTanks.Backend.Dtos.Rooms;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Backend.Services;

public sealed class RoomService(AppDbContext db)
{
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<IReadOnlyList<RoomResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await db.Rooms
            .AsNoTracking()
            .Include(r => r.Players)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return rooms.Select(MapRoom).ToList();
    }

    public async Task<RoomResponse?> GetByIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms
            .AsNoTracking()
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        return room is null ? null : MapRoom(room);
    }

    public async Task<(RoomResponse? Response, string? Error, int StatusCode)> CreateAsync(
        Guid ownerId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var owner = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId, cancellationToken);
        if (owner is null)
        {
            return (null, "Authenticated user not found.", StatusCodes.Status401Unauthorized);
        }

        var room = new Room
        {
            Code = await GenerateUniqueCodeAsync(cancellationToken),
            Name = request.Name.Trim(),
            MaxPlayers = request.MaxPlayers,
            OwnerId = ownerId,
            Status = RoomStatus.Waiting
        };

        var nickname = string.IsNullOrWhiteSpace(request.Nickname)
            ? owner.DisplayName
            : request.Nickname.Trim();

        db.Rooms.Add(room);
        db.RoomPlayers.Add(new RoomPlayer
        {
            RoomId = room.Id,
            UserId = ownerId,
            Nickname = nickname,
            Team = PlayerTeam.Blue
        });

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(room.Id, cancellationToken), null, StatusCodes.Status201Created);
    }

    public async Task<(RoomResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid roomId,
        Guid userId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return (null, "Room not found.", StatusCodes.Status404NotFound);
        }

        if (room.OwnerId != userId)
        {
            return (null, "Only the room owner can update this room.", StatusCodes.Status403Forbidden);
        }

        var currentPlayers = await db.RoomPlayers.CountAsync(p => p.RoomId == roomId, cancellationToken);
        if (request.MaxPlayers < currentPlayers)
        {
            return (null, "MaxPlayers cannot be lower than current player count.", StatusCodes.Status400BadRequest);
        }

        room.Name = request.Name.Trim();
        room.MaxPlayers = request.MaxPlayers;
        room.Status = request.Status;
        room.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(roomId, cancellationToken), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return (false, "Room not found.", StatusCodes.Status404NotFound);
        }

        if (room.OwnerId != userId)
        {
            return (false, "Only the room owner can delete this room.", StatusCodes.Status403Forbidden);
        }

        db.Rooms.Remove(room);
        await db.SaveChangesAsync(cancellationToken);
        return (true, null, StatusCodes.Status204NoContent);
    }

    public async Task<(RoomResponse? Response, string? Error, int StatusCode)> JoinAsync(
        Guid roomId,
        Guid userId,
        JoinRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return (null, "Room not found.", StatusCodes.Status404NotFound);
        }

        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !string.Equals(room.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Invalid room code.", StatusCodes.Status400BadRequest);
        }

        if (room.Status != RoomStatus.Waiting)
        {
            return (null, "Room is not accepting new players.", StatusCodes.Status409Conflict);
        }

        var alreadyIn = await db.RoomPlayers.AnyAsync(
            p => p.RoomId == roomId && p.UserId == userId,
            cancellationToken);
        if (alreadyIn)
        {
            return (await GetByIdAsync(roomId, cancellationToken), null, StatusCodes.Status200OK);
        }

        var currentPlayers = await db.RoomPlayers.CountAsync(p => p.RoomId == roomId, cancellationToken);
        if (currentPlayers >= room.MaxPlayers)
        {
            return (null, "Room is full.", StatusCodes.Status409Conflict);
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return (null, "Authenticated user not found.", StatusCodes.Status401Unauthorized);
        }

        var nickname = string.IsNullOrWhiteSpace(request.Nickname)
            ? user.DisplayName
            : request.Nickname.Trim();

        db.RoomPlayers.Add(new RoomPlayer
        {
            RoomId = roomId,
            UserId = userId,
            Nickname = nickname,
            Team = currentPlayers % 2 == 0 ? PlayerTeam.Blue : PlayerTeam.Red
        });

        await db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(roomId, cancellationToken), null, StatusCodes.Status200OK);
    }

    public async Task<(RoomResponse? Response, string? Error, int StatusCode)> LeaveAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return (null, "Room not found.", StatusCodes.Status404NotFound);
        }

        var membership = await db.RoomPlayers.FirstOrDefaultAsync(
            p => p.RoomId == roomId && p.UserId == userId,
            cancellationToken);
        if (membership is null)
        {
            return (null, "You are not in this room.", StatusCodes.Status400BadRequest);
        }

        db.RoomPlayers.Remove(membership);

        var remaining = await db.RoomPlayers
            .Where(p => p.RoomId == roomId && p.Id != membership.Id)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync(cancellationToken);

        if (room.OwnerId == userId)
        {
            var nextOwner = remaining.FirstOrDefault();
            if (nextOwner is null)
            {
                db.Rooms.Remove(room);
                await db.SaveChangesAsync(cancellationToken);
                return (null, null, StatusCodes.Status204NoContent);
            }

            room.OwnerId = nextOwner.UserId;
        }

        room.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(roomId, cancellationToken), null, StatusCodes.Status200OK);
    }

    public async Task<IReadOnlyList<PlayerResponse>?> ListPlayersAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Rooms.AnyAsync(r => r.Id == roomId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var players = await db.RoomPlayers
            .AsNoTracking()
            .Where(p => p.RoomId == roomId)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync(cancellationToken);

        return players.Select(MapPlayer).ToList();
    }

    public async Task<(PlayerResponse? Response, string? Error, int StatusCode)> UpdatePlayerAsync(
        Guid roomId,
        Guid playerId,
        Guid requesterId,
        UpdatePlayerRequest request,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return (null, "Room not found.", StatusCodes.Status404NotFound);
        }

        var player = await db.RoomPlayers.FirstOrDefaultAsync(
            p => p.Id == playerId && p.RoomId == roomId,
            cancellationToken);
        if (player is null)
        {
            return (null, "Player not found in this room.", StatusCodes.Status404NotFound);
        }

        if (player.UserId != requesterId && room.OwnerId != requesterId)
        {
            return (null, "You can only update your own player data unless you own the room.", StatusCodes.Status403Forbidden);
        }

        player.Nickname = request.Nickname.Trim();
        player.Team = request.Team;
        player.Health = request.Health;
        player.MaxHealth = request.MaxHealth;
        player.Score = request.Score;

        await db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (MapPlayer(player), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> RemovePlayerAsync(
        Guid roomId,
        Guid playerId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return (false, "Room not found.", StatusCodes.Status404NotFound);
        }

        var player = await db.RoomPlayers.FirstOrDefaultAsync(
            p => p.Id == playerId && p.RoomId == roomId,
            cancellationToken);
        if (player is null)
        {
            return (false, "Player not found in this room.", StatusCodes.Status404NotFound);
        }

        if (player.UserId != requesterId && room.OwnerId != requesterId)
        {
            return (false, "Only the player or room owner can remove this player.", StatusCodes.Status403Forbidden);
        }

        var removedUserId = player.UserId;
        db.RoomPlayers.Remove(player);

        var remaining = await db.RoomPlayers
            .Where(p => p.RoomId == roomId && p.Id != playerId)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync(cancellationToken);

        if (room.OwnerId == removedUserId)
        {
            var nextOwner = remaining.FirstOrDefault();
            if (nextOwner is null)
            {
                db.Rooms.Remove(room);
                await db.SaveChangesAsync(cancellationToken);
                return (true, null, StatusCodes.Status204NoContent);
            }

            room.OwnerId = nextOwner.UserId;
        }

        room.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null, StatusCodes.Status204NoContent);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = new string(Enumerable.Range(0, 6)
                .Select(_ => RoomCodeAlphabet[Random.Shared.Next(RoomCodeAlphabet.Length)])
                .ToArray());

            var exists = await db.Rooms.AnyAsync(r => r.Code == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
    }

    private static RoomResponse MapRoom(Room room) =>
        new(
            room.Id,
            room.Code,
            room.Name,
            room.MaxPlayers,
            room.Players.Count,
            room.Status,
            room.OwnerId,
            room.CreatedAt,
            room.UpdatedAt,
            room.Players.OrderBy(p => p.JoinedAt).Select(MapPlayer).ToList());

    private static PlayerResponse MapPlayer(RoomPlayer player) =>
        new(
            player.Id,
            player.RoomId,
            player.UserId,
            player.Nickname,
            player.Team,
            player.Health,
            player.MaxHealth,
            player.Score,
            player.JoinedAt);
}
