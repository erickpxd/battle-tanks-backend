using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Chat;
using BattleTanks.Application.GameEvents;
using BattleTanks.Infrastructure.Authentication;
using BattleTanks.Infrastructure.Caching;
using BattleTanks.Infrastructure.Messaging;
using BattleTanks.Infrastructure.Persistence.Context;
using BattleTanks.Infrastructure.Persistence.Memory;
using BattleTanks.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BattleTanks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database (write + optional read replica). Pooling is explicit in the connection string.
        var writeConnectionString = AppendPooling(
            configuration.GetConnectionString("PostgreSQL") ?? BuildConnectionStringFromEnv());
        var readConnectionString = AppendPooling(
            configuration.GetConnectionString("PostgreSQLRead") ?? writeConnectionString);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(writeConnectionString));
        services.AddDbContext<ApplicationReadDbContext>(options =>
            options.UseNpgsql(readConnectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // Repositories.
        // PlayerService (Infrastructure/Caching) is a Redis-backed Decorator over PlayerRepository.
        // Use cases depend only on IPlayerRepository and receive the decorator transparently via DI.
        services.AddScoped<PlayerRepository>();
        services.AddScoped<IPlayerRepository>(sp =>
            new PlayerService(
                sp.GetRequiredService<PlayerRepository>(),
                sp.GetRequiredService<IConnectionMultiplexer>()));
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IScoreRepository, ScoreRepository>();
        services.AddSingleton<ISimulatedChatRepository, SimulatedChatRepository>();

        // Shared Redis multiplexer (LB-6 history + LB-7 cache/session).
        services.AddRedisConnection(configuration);
        var mqttOptions = MqttOptions.From(configuration);
        services.AddSingleton(mqttOptions);
        services.AddSingleton<MqttGameService>();
        services.AddSingleton<IGameEventPublisher>(provider =>
            provider.GetRequiredService<MqttGameService>());
        services.AddHostedService(provider => provider.GetRequiredService<MqttGameService>());
        services.AddSingleton<IRedisEventStore, StackExchangeRedisEventStore>();
        services.AddSingleton<IGameEventHistory, RedisGameEventHistory>();
        services.AddSingleton<IOnlinePresenceService, RedisOnlinePresenceService>();
        services.AddSingleton<IRankingCache, RedisRankingCache>();
        services.AddSingleton<IJwtBlacklistService, RedisJwtBlacklistService>();

        // Security services
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();

        // JWT Authentication
        var jwtKey     = configuration["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key is required.");
        var jwtIssuer  = configuration["Jwt:Issuer"]   ?? throw new InvalidOperationException("Jwt:Issuer is required.");
        var jwtAudience= configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtIssuer,
                    ValidAudience            = jwtAudience,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs/game"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                        if (string.IsNullOrWhiteSpace(jti))
                        {
                            return;
                        }

                        var blacklist = context.HttpContext.RequestServices
                            .GetRequiredService<IJwtBlacklistService>();
                        if (await blacklist.IsBlacklistedAsync(jti, context.HttpContext.RequestAborted))
                        {
                            context.Fail("The token has been revoked.");
                        }
                    }
                };
            });

        return services;
    }

    private static string BuildConnectionStringFromEnv()
    {
        var host     = Environment.GetEnvironmentVariable("POSTGRES_HOST")     ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("POSTGRES_PORT")     ?? "5433";
        var db       = Environment.GetEnvironmentVariable("POSTGRES_DB")       ?? "battletanks";
        var user     = Environment.GetEnvironmentVariable("POSTGRES_USER")     ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "";
        return $"Host={host};Port={port};Database={db};Username={user};Password={password}";
    }

    private static string AppendPooling(string connectionString)
    {
        if (connectionString.Contains("Pooling=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return $"{connectionString.TrimEnd(';')};Pooling=true;Minimum Pool Size=1;Maximum Pool Size=50;Timeout=15";
    }
}
