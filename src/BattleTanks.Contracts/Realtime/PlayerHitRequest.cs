namespace BattleTanks.Contracts.Realtime;

public sealed record PlayerHitRequest(
    Guid TargetPlayerId,
    int Damage,
    string EventId,
    long ClientSentAtUnixMs);
