namespace BattleTanks.Contracts.Realtime;

public sealed record PlayerMoveRequest(
    int X,
    int Y,
    string Direction,
    string EventId,
    long ClientSentAtUnixMs);
