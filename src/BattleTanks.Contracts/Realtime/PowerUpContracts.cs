namespace BattleTanks.Contracts.Realtime;

public static class PowerUpTypes
{
    public const string MachineGun = "machine-gun";
    public const string DoubleDamage = "double-damage";
    public const string Shield = "shield";
    public const string SpeedBoost = "speed-boost";
    public const string RepairKit = "repair-kit";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        MachineGun,
        DoubleDamage,
        Shield,
        SpeedBoost,
        RepairKit
    };
}

public sealed record RealtimePowerUp(
    Guid Id,
    string Type,
    int X,
    int Y,
    DateTime ExpiresAt);

public sealed record CollectPowerUpRequest(
    Guid PowerUpId,
    string EventId,
    long ClientSentAtUnixMs);

public sealed record PowerUpCollectionResult(
    RealtimePlayer Player,
    IReadOnlyList<RealtimePowerUp> ActivePowerUps);
