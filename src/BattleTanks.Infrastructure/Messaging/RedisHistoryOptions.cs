using Microsoft.Extensions.Configuration;

namespace BattleTanks.Infrastructure.Messaging;

public sealed record RedisHistoryOptions(
    string ConnectionString,
    int MaxEventsPerRoom,
    TimeSpan TimeToLive)
{
    public static RedisHistoryOptions From(IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"]
            ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
            ?? "localhost:6379,abortConnect=false";
        var maxEvents = int.TryParse(configuration["Redis:MaxEventsPerRoom"], out var parsedMax)
            ? Math.Clamp(parsedMax, 1, 500)
            : 100;
        var ttlMinutes = int.TryParse(configuration["Redis:HistoryTtlMinutes"], out var parsedTtl)
            ? Math.Clamp(parsedTtl, 1, 10_080)
            : 60;

        return new RedisHistoryOptions(connectionString, maxEvents, TimeSpan.FromMinutes(ttlMinutes));
    }
}
