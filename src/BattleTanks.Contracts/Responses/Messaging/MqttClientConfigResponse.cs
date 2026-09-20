namespace BattleTanks.Contracts.Responses.Messaging;

public sealed record MqttClientConfigResponse(
    string WebSocketUrl,
    string Username,
    string Password);
