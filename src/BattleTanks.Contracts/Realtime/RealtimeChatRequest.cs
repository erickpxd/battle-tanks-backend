namespace BattleTanks.Contracts.Realtime;

public sealed record RealtimeChatRequest(
    string Message,
    string EventId,
    long ClientSentAtUnixMs);
