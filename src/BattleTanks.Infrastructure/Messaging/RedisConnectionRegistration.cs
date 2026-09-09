using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BattleTanks.Infrastructure.Messaging;

public static class RedisConnectionRegistration
{
    public static IServiceCollection AddRedisConnection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = RedisHistoryOptions.From(configuration);
        services.AddSingleton(redisOptions);

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = provider.GetRequiredService<RedisHistoryOptions>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("RedisConnection");
            var configurationOptions = ConfigurationOptions.Parse(options.ConnectionString);
            configurationOptions.AbortOnConnectFail = false;
            configurationOptions.ConnectTimeout = Math.Min(configurationOptions.ConnectTimeout, 2_000);

            var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);
            logger.LogInformation(
                "Redis connected to {Endpoints}",
                string.Join(',', configurationOptions.EndPoints));
            return multiplexer;
        });

        return services;
    }
}
