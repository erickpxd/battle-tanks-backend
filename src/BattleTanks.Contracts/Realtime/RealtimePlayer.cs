namespace BattleTanks.Contracts.Realtime;

public sealed record RealtimePlayer(
    Guid Id,
    string Name,
    PlayerPosition Position,
    string? EventId = null,
    long? ClientSentAtUnixMs = null,
    DateTime? ServerReceivedAt = null,
    int Health = 100,
    int MaxHealth = 100,
    string Status = "active");
