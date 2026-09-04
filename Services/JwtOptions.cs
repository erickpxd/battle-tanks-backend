namespace BattleTanks.Backend.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "BattleTanks";
    public string Audience { get; set; } = "BattleTanksClients";
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 120;
}
