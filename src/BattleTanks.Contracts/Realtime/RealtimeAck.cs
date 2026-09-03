namespace BattleTanks.Contracts.Realtime;

public sealed record RealtimeAck(
    string EventId,
    long ClientSentAtUnixMs,
    DateTime ServerReceivedAt);
