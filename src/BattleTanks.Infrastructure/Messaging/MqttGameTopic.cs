using BattleTanks.Contracts.GameEvents;

namespace BattleTanks.Infrastructure.Messaging;

public static class MqttGameTopic
{
    public static string For(GameEventEnvelope gameEvent) => gameEvent.EventType switch
    {
        GameEventTypes.PlayerHit => ForRoom(gameEvent.RoomId, "collisions/player-hit"),
        GameEventTypes.MapTileDestroyed => ForRoom(gameEvent.RoomId, "map/tile-destroyed"),
        GameEventTypes.GameEnded => ForRoom(gameEvent.RoomId, "game/ended"),
        _ => ForRoom(gameEvent.RoomId, $"game/{NormalizeSegment(gameEvent.EventType)}")
    };

    public static string RoomSubscription(string roomId) => ForRoom(roomId, "#");

    private static string ForRoom(string roomId, string suffix) =>
        $"battle-tanks/{NormalizeRoomId(roomId)}/events/{suffix}";

    public static string NormalizeRoomId(string roomId)
    {
        var normalized = roomId.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 32 ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Room identifier is invalid for an MQTT topic.", nameof(roomId));
        }

        return normalized;
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.'
                ? character
                : '-')
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}
