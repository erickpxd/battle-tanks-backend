using System.Text.Json;

namespace BattleTanks.Contracts.GameEvents;

public sealed record GameEventEnvelope(
    string EventId,
    string EventType,
    string RoomId,
    Guid? PlayerId,
    long SentAtUnixMs,
    long PublishedAtUnixMs,
    JsonElement Data)
{
    public static GameEventEnvelope Create<TData>(
        string eventId,
        string eventType,
        string roomId,
        Guid? playerId,
        long sentAtUnixMs,
        DateTimeOffset publishedAt,
        TData data) => new(
            eventId,
            eventType,
            roomId,
            playerId,
            sentAtUnixMs,
            publishedAt.ToUnixTimeMilliseconds(),
            JsonSerializer.SerializeToElement(data, GameEventJson.SerializerOptions));
}

public static class GameEventTypes
{
    public const string PlayerHit = "collision.player-hit";
    public const string MapTileDestroyed = "map.tile-destroyed";
    public const string GameEnded = "game.ended";
}

public sealed record PlayerHitEventData(
    Guid TargetPlayerId,
    int Damage,
    int RemainingHealth,
    string Status);

public sealed record MapTileDestroyedEventData(int Column, int Row);

public sealed record GameEndedEventData(
    Guid DefeatedPlayerId,
    Guid? WinnerPlayerId,
    string TriggerEventId,
    string? DefeatedPlayerName = null,
    string? WinnerPlayerName = null,
    int WinnerPointsAwarded = 0,
    int? WinnerTotalScore = null);

public static class GameEventJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}
