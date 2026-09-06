using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Chat;
using BattleTanks.Application.GameEvents;
using BattleTanks.Infrastructure.Authentication;
using BattleTanks.Infrastructure.Messaging;
using BattleTanks.Infrastructure.Persistence.Context;
using BattleTanks.Infrastructure.Persistence.Memory;
using BattleTanks.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace BattleTanks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? BuildConnectionStringFromEnv();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddSingleton<ISimulatedChatRepository, SimulatedChatRepository>();

        // Room-scoped event messaging and bounded Redis history.
        var mqttOptions = MqttOptions.From(configuration);
        var redisOptions = RedisHistoryOptions.From(configuration);
        services.AddSingleton(mqttOptions);
        services.AddSingleton(redisOptions);
        services.AddSingleton<MqttGameService>();
        services.AddSingleton<IGameEventPublisher>(provider =>
            provider.GetRequiredService<MqttGameService>());
        services.AddHostedService(provider => provider.GetRequiredService<MqttGameService>());
        services.AddSingleton<IRedisEventStore, StackExchangeRedisEventStore>();
        services.AddSingleton<IGameEventHistory, RedisGameEventHistory>();

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
}
