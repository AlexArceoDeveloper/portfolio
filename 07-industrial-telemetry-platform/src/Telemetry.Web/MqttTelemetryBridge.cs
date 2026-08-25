using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Protocol;

namespace Telemetry.Web;

public sealed record MqttOptions(string Host, int Port, string TopicPrefix, string ClientId);

public sealed class MqttTelemetryBridge(
    MqttOptions options,
    ITelemetryStore store,
    ILogger<MqttTelemetryBridge> logger) : BackgroundService, ITelemetryPublisher
{
    private readonly MqttFactory _factory = new();
    private IMqttClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = _factory.CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += async eventArgs =>
        {
            try
            {
                var payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                var reading = JsonSerializer.Deserialize<TelemetryReading>(payload);
                if (reading is not null)
                {
                    await store.AppendAsync(reading, stoppingToken);
                }
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Discarded malformed MQTT telemetry payload.");
            }
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    var clientOptions = new MqttClientOptionsBuilder()
                        .WithTcpServer(options.Host, options.Port)
                        .WithClientId(options.ClientId)
                        .WithCleanSession()
                        .Build();
                    await _client.ConnectAsync(clientOptions, stoppingToken);
                    var subscription = _factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(filter => filter
                            .WithTopic($"{options.TopicPrefix}/#")
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                        .Build();
                    await _client.SubscribeAsync(subscription, stoppingToken);
                    logger.LogInformation(
                        "Subscribed to MQTT telemetry at {Host}:{Port}.",
                        options.Host,
                        options.Port);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is MqttCommunicationException or IOException)
            {
                logger.LogWarning(exception, "MQTT connection unavailable; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    public async Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken = default)
    {
        if (_client is null || !_client.IsConnected)
        {
            throw new InvalidOperationException("The MQTT bridge is not connected.");
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"{options.TopicPrefix}/{Uri.EscapeDataString(reading.DeviceId)}")
            .WithPayload(JsonSerializer.Serialize(reading))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _client.PublishAsync(message, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client?.IsConnected == true)
        {
            await _client.DisconnectAsync(cancellationToken: cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }
}
