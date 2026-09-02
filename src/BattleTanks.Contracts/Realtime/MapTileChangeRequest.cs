namespace BattleTanks.Contracts.Realtime;

public sealed record MapTileChangeRequest(
    int Column,
    int Row,
    string EventId,
    long ClientSentAtUnixMs);
