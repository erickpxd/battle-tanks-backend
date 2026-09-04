namespace BattleTanks.Backend.Dtos;

public sealed record ErrorResponse(string Message, string? Code = null);
