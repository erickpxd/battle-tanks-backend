using Microsoft.Extensions.Configuration;

namespace BattleTanks.Infrastructure.Messaging;

public sealed record MqttOptions(
    string Host,
    int Port,
    string ClientId,
    string? Username,
    string? Password,
    bool UseTls,
    int ReconnectDelaySeconds)
{
    public static MqttOptions From(IConfiguration configuration) => new(
        configuration["Mqtt:Host"] ?? "localhost",
        ReadInt(configuration["Mqtt:Port"], 1883),
        configuration["Mqtt:ClientId"] ?? $"battle-tanks-backend-{Environment.MachineName}",
        NullIfBlank(configuration["Mqtt:Username"]),
        NullIfBlank(configuration["Mqtt:Password"]
            ?? Environment.GetEnvironmentVariable("MQTT_BACKEND_PASSWORD")),
        ReadBool(configuration["Mqtt:UseTls"], false),
        Math.Max(1, ReadInt(configuration["Mqtt:ReconnectDelaySeconds"], 5)));

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static int ReadInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool ReadBool(string? value, bool fallback) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;
}
