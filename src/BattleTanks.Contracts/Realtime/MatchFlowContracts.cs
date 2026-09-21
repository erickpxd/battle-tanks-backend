namespace BattleTanks.Contracts.Realtime;

public sealed record PlayerReadyRequest(
    bool Ready,
    string EventId,
    long ClientSentAtUnixMs);

public sealed record FireShotRequest(
    string Direction,
    string EventId,
    long ClientSentAtUnixMs);

public sealed record RealtimeShot(
    Guid PlayerId,
    PlayerPosition Position,
    string EventId,
    long ClientSentAtUnixMs,
    DateTime ServerReceivedAt);

public sealed record SurrenderRequest(
    string EventId,
    long ClientSentAtUnixMs);
