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
    string Status = "active",
    int ShieldHealth = 0,
    DateTime? ShieldExpiresAt = null,
    DateTime? SpeedBoostExpiresAt = null,
    DateTime? DoubleDamageExpiresAt = null,
    DateTime? MachineGunExpiresAt = null,
    bool Ready = false);
