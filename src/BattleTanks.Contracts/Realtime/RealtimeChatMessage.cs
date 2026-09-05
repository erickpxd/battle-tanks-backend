namespace BattleTanks.Contracts.Realtime;

public sealed record RealtimeChatMessage(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    string Message,
    DateTime Timestamp,
    string EventId,
    long ClientSentAtUnixMs,
    DateTime ServerReceivedAt);
