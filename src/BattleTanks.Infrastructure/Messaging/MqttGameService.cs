using System.Text.Json;
using BattleTanks.Application.GameEvents;
using BattleTanks.Contracts.GameEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace BattleTanks.Infrastructure.Messaging;

public sealed class MqttGameService : BackgroundService, IGameEventPublisher
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttGameService> _logger;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _clientOptions;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);

    public MqttGameService(MqttOptions options, ILogger<MqttGameService> logger)
    {
        _options = options;
        _logger = logger;
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
        _clientOptions = BuildClientOptions(options);

        _client.ConnectedAsync += _ =>
        {
            // EVIDÊNCIA LAB 06: tirar screenshot deste log junto ao dashboard do EMQX.
            _logger.LogInformation("MQTT connected to {Host}:{Port} as {ClientId}",
                options.Host, options.Port, options.ClientId);
            return Task.CompletedTask;
        };
        _client.DisconnectedAsync += args =>
        {
            _logger.LogWarning(args.Exception,
                "MQTT disconnected; the background service will retry in {DelaySeconds}s",
                options.ReconnectDelaySeconds);
            return Task.CompletedTask;
        };
    }

    public async Task PublishAsync(
        GameEventEnvelope gameEvent,
        GameEventQos qos,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await EnsureConnectedAsync(cancellationToken))
            {
                _logger.LogWarning("MQTT event {EventId} was not published because the broker is unavailable",
                    gameEvent.EventId);
                return;
            }

            var payload = JsonSerializer.Serialize(gameEvent, GameEventJson.SerializerOptions);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(MqttGameTopic.For(gameEvent))
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos == GameEventQos.AtLeastOnce
                    ? MqttQualityOfServiceLevel.AtLeastOnce
                    : MqttQualityOfServiceLevel.AtMostOnce)
                .WithRetainFlag(false)
                .Build();

            await _client.PublishAsync(message, cancellationToken);
            _logger.LogInformation(
                "MQTT published {EventType} ({EventId}) for room {RoomId} with QoS {Qos}",
                gameEvent.EventType, gameEvent.EventId, gameEvent.RoomId, (int)qos);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Messaging degradation must not terminate the multiplayer server.
            _logger.LogError(exception, "MQTT failed to publish event {EventId}", gameEvent.EventId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await EnsureConnectedAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(cancellationToken: cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _connectionGate.Dispose();
        _client.Dispose();
        base.Dispose();
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return true;
        }

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_client.IsConnected)
            {
                return true;
            }

            await _client.ConnectAsync(_clientOptions, cancellationToken);
            return _client.IsConnected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to connect to MQTT broker {Host}:{Port}",
                _options.Host, _options.Port);
            return false;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private static MqttClientOptions BuildClientOptions(MqttOptions options)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(options.ClientId)
            .WithTcpServer(options.Host, options.Port)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(20));

        if (options.Username is not null)
        {
            builder.WithCredentials(options.Username, options.Password ?? string.Empty);
        }

        if (options.UseTls)
        {
            builder.WithTlsOptions(_ => { });
        }

        return builder.Build();
    }
}
